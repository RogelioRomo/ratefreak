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
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.IntegrationTests;

public sealed class IgdbImportTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Transport : HttpMessageHandler
    {
        public int Calls; public int GameType; public int Id = 1942;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("api.igdb.com",request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent($$$"""[{"id":{{{Id}}},"name":"Authoritative game","game_type":{{{GameType}}},"first_release_date":1431993600}]""",Encoding.UTF8,"application/json") });
        }
    }
    private sealed class Auth : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("id.twitch.tv",request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("""{"access_token":"fake-token","expires_in":3600,"token_type":"bearer"}""") });
        }
    }
    private ApiFactory Factory(Transport transport) => new(connectionString:database.ConnectionString,configureServices:services=>
    {
        services.PostConfigure<IgdbOptions>(o=> { o.Enabled=true;o.ClientId="fakeclient";o.ClientSecret="fakesecret"; });
        services.AddHttpClient("igdb").ConfigurePrimaryHttpMessageHandler(()=>transport);
        services.AddHttpClient("igdb-auth").ConfigurePrimaryHttpMessageHandler(()=>new Auth());
    });
    private RateFreakDbContext Db()=>new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private static HttpClient Client(ApiFactory f)=>f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false });
    private static async Task Token(HttpClient c)
    { var j=await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf",Ct);c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",j.GetProperty("requestToken").GetString()); }
    private static async Task SignIn(HttpClient c)
    {
        var tag=Guid.NewGuid().ToString("N")[..12];await Token(c);
        Assert.Equal(HttpStatusCode.Created,(await c.PostAsJsonAsync("/api/v1/auth/register",new { email=tag+"@example.test",password="Game-password-72",handle="game_"+tag,displayName="Listener" },Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/v1/auth/login",new { email=tag+"@example.test",password="Game-password-72" },Ct)).StatusCode);await Token(c);
    }
    private static Task<HttpResponseMessage> Import(HttpClient c,string id,string type="video_game",string provider="igdb")=>c.PostAsJsonAsync("/api/v1/media/import",new { provider,externalId=id,mediaType=type },Ct);
    private static async Task<JsonElement> Json(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    { Assert.Equal(expected,response.StatusCode);return await response.Content.ReadFromJsonAsync<JsonElement>(Ct); }


    [Fact]
    public async Task Game_import_is_authoritative_repeatable_and_usable_in_personal_media_and_collections()
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        var created=await Json(await Import(client,"0001942"),HttpStatusCode.Created);var id=created.GetProperty("mediaId").GetGuid();
        var detail=await Json(await client.GetAsync($"/api/v1/media/{id}",Ct));
        Assert.Equal("video_game",detail.GetProperty("type").GetString());Assert.Equal("Authoritative game",detail.GetProperty("title").GetString());
        Assert.Equal("game",detail.GetProperty("externalIds")[0].GetProperty("namespace").GetString());Assert.Equal("1942",detail.GetProperty("externalIds")[0].GetProperty("externalId").GetString());
        Assert.Equal(JsonValueKind.Null,detail.GetProperty("externalIds")[0].GetProperty("externalUrl").ValueKind);
        await using var db=Db();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_items SET title='Curated game' WHERE id={id}",Ct);
        factory.Services.GetRequiredService<IOptions<IgdbOptions>>().Value.Enabled=false;
        var replay=await Json(await Import(client,"1942"));Assert.Equal(id,replay.GetProperty("mediaId").GetGuid());Assert.False(replay.GetProperty("created").GetBoolean());Assert.Equal(1,transport.Calls);
        Assert.Equal("Curated game",(await Json(await client.GetAsync($"/api/v1/media/{id}",Ct))).GetProperty("title").GetString());
        Assert.Contains((await Json(await client.GetAsync("/api/v1/media?type=video_game",Ct))).GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==id);
        await Json(await client.PutAsJsonAsync($"/api/v1/users/me/library/{id}",new { status="completed" },Ct));
        Assert.Single((await Json(await client.GetAsync("/api/v1/users/me/library?type=video_game",Ct))).GetProperty("items").EnumerateArray());
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{id}/rating",new { score=4.5 },Ct));
        Assert.Equal(4.5m,(await Json(await client.GetAsync($"/api/v1/media/{id}/ratings",Ct))).GetProperty("average").GetDecimal());
        await Json(await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews",new { body="Good game" },Ct),HttpStatusCode.Created);
        var collection=await Json(await client.PostAsJsonAsync("/api/v1/collections",new { name="Games" },Ct),HttpStatusCode.Created);
        var collectionId=collection.GetProperty("collection").GetProperty("id").GetGuid();
        await Json(await client.PostAsJsonAsync($"/api/v1/collections/{collectionId}/items",new { expectedRevision=1,mediaItemId=id },Ct),HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("0","video_game")] [InlineData("2147483648","video_game")] [InlineData("1942","album")]
    public async Task Invalid_identity_is_rejected_before_http(string id,string type)
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest,(await Import(client,id,type)).StatusCode);Assert.Equal(0,transport.Calls);
    }

    [Fact]
    public async Task Import_enforces_session_and_csrf()
    {
        using var transport=new Transport();await using var factory=Factory(transport);using var client=Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Import(client,"1942")).StatusCode);await SignIn(client);client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await Import(client,"1942")).StatusCode);Assert.Equal(0,transport.Calls);
    }

    [Fact]
    public async Task Excluded_games_do_not_create_catalog_records()
    {
        using var transport=new Transport { Id=1943,GameType=1 };await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        Assert.Equal(HttpStatusCode.NotFound,(await Import(client,"1943")).StatusCode);
        await using var db=Db();Assert.False(await db.MediaExternalIds.AnyAsync(x=>x.Provider=="igdb"&&x.ExternalId=="1943",Ct));
    }

    private sealed class RacingProvider : IMediaProvider
    {
        private int calls; private readonly TaskCompletionSource pair=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderCapability Capability=>new("igdb",true,["video_game"],"Test","");
        public Task<ExternalSearchPage> SearchAsync(string q,string t,int p,CancellationToken ct)=>throw new NotSupportedException();
        public async Task<ExternalMediaResult?> GetDetailsAsync(string id,string type,CancellationToken ct)
        { if(Interlocked.Increment(ref calls)==2)pair.TrySetResult();await pair.Task.WaitAsync(ct);return new("igdb",id,"video_game","Concurrent game",null,null,null,JsonSerializer.SerializeToElement(new {})); }
    }
    [Fact]
    public async Task Concurrent_imports_have_one_winner_and_no_orphans()
    {
        await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:s=>s.AddSingleton<IMediaProvider>(new RacingProvider()));using var client=Client(factory);await SignIn(client);
        await using var db=Db();var before=await db.MediaItems.CountAsync(Ct);
        var results=await Task.WhenAll(Import(client,"7777"),Import(client,"7777"));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);
        var first=await results[0].Content.ReadFromJsonAsync<ImportMediaResult>(Ct);var second=await results[1].Content.ReadFromJsonAsync<ImportMediaResult>(Ct);Assert.Equal(first!.MediaId,second!.MediaId);
        Assert.Equal(before+1,await db.MediaItems.CountAsync(Ct));Assert.Equal(1,await db.MediaExternalIds.CountAsync(x=>x.Provider=="igdb"&&x.ExternalId=="7777",Ct));
    }
    [Fact]
    public async Task Remakes_and_remasters_with_same_title_remain_distinct_games()
    {
        using var transport=new Transport { Id=1944,GameType=8 };await using var factory=Factory(transport);using var client=Client(factory);await SignIn(client);
        var remake=await Json(await Import(client,"1944"),HttpStatusCode.Created);
        transport.Id=1945;transport.GameType=9;
        var remaster=await Json(await Import(client,"1945"),HttpStatusCode.Created);
        Assert.NotEqual(remake.GetProperty("mediaId").GetGuid(),remaster.GetProperty("mediaId").GetGuid());
        await using var db=Db();
        Assert.Equal(2,await db.MediaExternalIds.CountAsync(x=>x.Provider=="igdb"&&(x.ExternalId=="1944"||x.ExternalId=="1945"),Ct));
    }

}
