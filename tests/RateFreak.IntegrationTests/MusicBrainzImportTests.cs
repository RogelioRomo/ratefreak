using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Providers.MusicBrainz;

namespace RateFreak.IntegrationTests;

public sealed class MusicBrainzImportTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls; public string PrimaryType="Album"; public HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); var id=request.RequestUri!.AbsolutePath.Split('/').Last();
            return Task.FromResult(new HttpResponseMessage(Status) { Content=new StringContent($$"""{"id":"{{id}}","title":"Authoritative album","primary-type":"{{PrimaryType}}","secondary-types":["Live"],"first-release-date":"1999","artist-credit":[]}""",Encoding.UTF8,"application/json") });
        }
    }
    private ApiFactory Factory(Transport transport)=>new(connectionString:database.ConnectionString,configureServices:services=>
    {
        services.PostConfigure<MusicBrainzOptions>(options=> { options.Enabled=true;options.Contact="https://example.test/contact"; });
        services.AddHttpClient<IMediaProvider,MusicBrainzMediaProvider>().ConfigurePrimaryHttpMessageHandler(()=>transport);
    });
    private RateFreakDbContext Db()=>new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private static HttpClient Client(ApiFactory f)=>f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false });
    private static async Task Token(HttpClient c)
    { var j=await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf",Ct);c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",j.GetProperty("requestToken").GetString()); }
    private static async Task SignIn(HttpClient c)
    {
        var tag=Guid.NewGuid().ToString("N")[..12];await Token(c);
        Assert.Equal(HttpStatusCode.Created,(await c.PostAsJsonAsync("/api/v1/auth/register",new { email=tag+"@example.test",password="Album-password-72",handle="album_"+tag,displayName="Listener" },Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/v1/auth/login",new { email=tag+"@example.test",password="Album-password-72" },Ct)).StatusCode);await Token(c);
    }
    private static Task<HttpResponseMessage> Import(HttpClient c,string id,string type="album",string provider="musicbrainz")=>c.PostAsJsonAsync("/api/v1/media/import",new { provider,externalId=id,mediaType=type },Ct);
    private static async Task<JsonElement> Json(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    { Assert.Equal(expected,response.StatusCode);return await response.Content.ReadFromJsonAsync<JsonElement>(Ct); }

    [Fact]
    public async Task Album_import_is_authoritative_and_supports_existing_personal_and_collection_features()
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);var id=Guid.NewGuid().ToString("D");
        var response=await Import(client,id.ToUpperInvariant());var created=await Json(response,HttpStatusCode.Created);var mediaId=created.GetProperty("mediaId").GetGuid();
        var detail=await Json(await client.GetAsync(response.Headers.Location,Ct));Assert.Equal("album",detail.GetProperty("type").GetString());Assert.Equal("Authoritative album",detail.GetProperty("title").GetString());Assert.Equal(JsonValueKind.Null,detail.GetProperty("releaseDate").ValueKind);
        Assert.Equal("release-group",detail.GetProperty("externalIds")[0].GetProperty("namespace").GetString());Assert.Equal(id,detail.GetProperty("externalIds")[0].GetProperty("externalId").GetString());
        await using var db=Db();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_items SET title='Curated album' WHERE id={mediaId}",Ct);
        factory.Services.GetRequiredService<IOptions<MusicBrainzOptions>>().Value.Enabled=false;
        var replay=await Json(await Import(client,id));Assert.Equal(mediaId,replay.GetProperty("mediaId").GetGuid());Assert.False(replay.GetProperty("created").GetBoolean());Assert.Equal(1,transport.Calls);
        Assert.Equal("Curated album",(await Json(await client.GetAsync(response.Headers.Location,Ct))).GetProperty("title").GetString());
        var catalog=await Json(await client.GetAsync("/api/v1/media?type=album",Ct));Assert.Contains(catalog.GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==mediaId);
        await Json(await client.PutAsJsonAsync($"/api/v1/users/me/library/{mediaId}",new { status="completed" },Ct));
        Assert.Single((await Json(await client.GetAsync("/api/v1/users/me/library?type=album",Ct))).GetProperty("items").EnumerateArray());
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{mediaId}/rating",new { score=4.5 },Ct));
        Assert.Equal(4.5m,(await Json(await client.GetAsync($"/api/v1/media/{mediaId}/ratings",Ct))).GetProperty("average").GetDecimal());
        await Json(await client.PostAsJsonAsync($"/api/v1/media/{mediaId}/reviews",new { body="A fine album" },Ct),HttpStatusCode.Created);
        var collection=await Json(await client.PostAsJsonAsync("/api/v1/collections",new { name="Albums" },Ct),HttpStatusCode.Created);var collectionId=collection.GetProperty("collection").GetProperty("id").GetGuid();
        await Json(await client.PostAsJsonAsync($"/api/v1/collections/{collectionId}/items",new { expectedRevision=1,mediaItemId=mediaId,data=new {} },Ct),HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/v1/collections/{collectionId}/items",new { expectedRevision=2,mediaItemId=mediaId,data=new {} },Ct)).StatusCode);
    }
    [Theory]
    [InlineData("bad","album","musicbrainz")]
    [InlineData("00000000-0000-0000-0000-000000000000","album","musicbrainz")]
    [InlineData("{3bd76d40-7f0e-36b7-9348-91a33afee20e}","album","musicbrainz")]
    [InlineData("3bd76d40-7f0e-36b7-9348-91a33afee20e","movie","musicbrainz")]
    [InlineData("11","album","tmdb")]
    [InlineData("11","movie","unknown")]
    public async Task Invalid_identity_is_rejected_before_http(string id,string type,string provider)
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest,(await Import(client,id,type,provider)).StatusCode);Assert.Equal(0,transport.Calls);
    }
    [Theory]
    [InlineData("EP")] [InlineData("Single")]
    public async Task Non_album_upstream_result_does_not_create_catalog_record(string type)
    {
        using var transport=new Transport { PrimaryType=type };await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);var id=Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.NotFound,(await Import(client,id)).StatusCode);await using var db=Db();Assert.False(await db.MediaExternalIds.AnyAsync(x=>x.Provider=="musicbrainz"&&x.ExternalId==id,Ct));
    }
    [Fact]
    public async Task Album_import_requires_authentication_and_csrf()
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);var id=Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.Unauthorized,(await Import(client,id)).StatusCode);await SignIn(client);client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await Import(client,id)).StatusCode);Assert.Equal(0,transport.Calls);
    }
    private sealed class RacingProvider : IMediaProvider
    {
        private int calls;private readonly TaskCompletionSource pair=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderCapability Capability=>new("musicbrainz",true,["album"],"Test","");
        public Task<ExternalSearchPage> SearchAsync(string q,string t,int p,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<ExternalMediaResult?> GetDetailsAsync(string id,string type,CancellationToken ct)
        { if(Interlocked.Increment(ref calls)==2) pair.TrySetResult();await pair.Task.WaitAsync(ct);return new("musicbrainz",id,"album","Concurrent album",null,null,null,JsonSerializer.SerializeToElement(new {})); }
    }
    [Fact]
    public async Task Concurrent_album_imports_return_one_winner_without_orphans()
    {
        var provider=new RacingProvider();await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:s=>s.AddSingleton<IMediaProvider>(provider));using var client=Client(factory);await SignIn(client);
        var id=Guid.NewGuid().ToString("D");await using var db=Db();var before=await db.MediaItems.CountAsync(Ct);
        var results=await Task.WhenAll(Import(client,id),Import(client,id));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);
        var first=await results[0].Content.ReadFromJsonAsync<ImportMediaResult>(Ct);var second=await results[1].Content.ReadFromJsonAsync<ImportMediaResult>(Ct);Assert.Equal(first!.MediaId,second!.MediaId);
        Assert.Equal(before+1,await db.MediaItems.CountAsync(Ct));Assert.Equal(1,await db.MediaExternalIds.CountAsync(x=>x.Provider=="musicbrainz"&&x.ExternalId==id,Ct));
    }
    [Fact]
    public async Task Downgrade_with_albums_is_refused_without_changing_history_or_records()
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        var imported=await Json(await Import(client,Guid.NewGuid().ToString("D")),HttpStatusCode.Created);var id=imported.GetProperty("mediaId").GetGuid();await using var db=Db();
        var history=(await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray();
        var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync("20261030160901_AddCollections",Ct));
        Assert.Contains("albums exist",error.Message,StringComparison.OrdinalIgnoreCase);Assert.Equal(history,(await db.Database.GetAppliedMigrationsAsync(Ct)).ToArray());Assert.True(await db.MediaItems.AnyAsync(x=>x.Id==id,Ct));
    }
    private sealed class HostHandler(string host,string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(host,request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(body,Encoding.UTF8,"application/json") });
        }
    }
    [Fact]
    public async Task Provider_http_clients_have_isolated_transport_configuration()
    {
        const string id="3bd76d40-7f0e-36b7-9348-91a33afee20e";
        await using var factory=new ApiFactory(configureServices:services=>
        {
            services.PostConfigure<MusicBrainzOptions>(o=>{o.Enabled=true;o.Contact="https://example.test/contact";});
            services.PostConfigure<RateFreak.Infrastructure.Providers.Tmdb.TmdbOptions>(o=>{o.Enabled=true;o.AccessToken="fake-token";});
            services.AddHttpClient("IMediaProvider").ConfigurePrimaryHttpMessageHandler(()=>new HostHandler("unnamed-client-must-not-be-used","{}"));
            services.AddHttpClient("tmdb").ConfigurePrimaryHttpMessageHandler(()=>new HostHandler("api.themoviedb.org","{\"id\":11,\"title\":\"A Movie\"}"));
            services.AddHttpClient("musicbrainz").ConfigurePrimaryHttpMessageHandler(()=>new HostHandler("musicbrainz.org","{\"id\":\""+id+"\",\"title\":\"Album\",\"primary-type\":\"Album\",\"artist-credit\":[]}"));
        });
        using var client=Client(factory);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/media/external/tmdb/movie/11",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/media/external/musicbrainz/album/"+id,Ct)).StatusCode);
    }

}
