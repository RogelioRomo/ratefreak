using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.PersonalMedia;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class LibraryWorkspaceTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>()
        .UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task<Guid> SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var email = tag + "@example.test";
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
            { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Reader" }, Ct);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Local-password-72" }, Ct)).StatusCode);
        await Token(client);
        return account.GetProperty("id").GetGuid();
    }
    private async Task<Guid> Media(string type = "movie", string title = "Phase four test")
    {
        await using var db = Context();
        var item = new MediaItem(type, title);
        db.Add(item); await db.SaveChangesAsync(Ct); return item.Id;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }


    [Fact]
    public async Task Search_sort_and_own_ratings_are_applied_before_paging()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory);
        await SignIn(owner); await SignIn(other);
        var a = await Media("movie", "alpha 100%_\\ literal");
        var b = await Media("book", "Beta"); var c = await Media("album", "Gamma");
        foreach (var id in new[] {a,b,c})
            await Json(await owner.PutAsJsonAsync($"/api/v1/users/me/library/{id}", new {status="completed", completedAt=id==a?"2026-01-01":id==b?"2026-02-01":(string?)null}, Ct));
        await Json(await owner.PutAsJsonAsync($"/api/v1/media/{a}/rating",new {score=0},Ct));
        await Json(await owner.PutAsJsonAsync($"/api/v1/media/{b}/rating",new {score=4.5},Ct));
        await Json(await other.PutAsJsonAsync($"/api/v1/media/{c}/rating",new {score=5},Ct));
        async Task<JsonElement> List(string query) => await Json(await owner.GetAsync("/api/v1/users/me/library?"+query,Ct));
        foreach(var query in new[]{"q=ALPHA", "q=%25", "q=_", "q=%5C", "q=%20alpha%20&status=completed&type=movie"})
        {
            var found=await List(query); Assert.Equal(1,found.GetProperty("totalCount").GetInt32());
            Assert.Equal(a,found.GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
        }
        foreach(var sort in new[]{"recently_added","recently_completed","title","highest_rated","lowest_rated"})
        {
            var expected=sort switch {"recently_added"=>new[]{c,b,a}, "recently_completed"=>new[]{b,a,c}, "title"=>new[]{a,b,c}, "highest_rated"=>new[]{b,a,c}, _=>new[]{a,b,c}};
            for(var page=1;page<=3;page++)
            {
                var result=await List($"sort={sort}&pageSize=1&page={page}");
                Assert.Equal(3,result.GetProperty("totalCount").GetInt32());
                var row=result.GetProperty("items")[0];
                Assert.Equal(expected[page-1],row.GetProperty("media").GetProperty("id").GetGuid());
                var rating=row.GetProperty("ownRating");
                if(expected[page-1]==c) Assert.Equal(JsonValueKind.Null,rating.ValueKind);
                else Assert.Equal(expected[page-1]==a?0m:4.5m,rating.GetDecimal());
            }
        }
        Assert.Empty((await Json(await other.GetAsync("/api/v1/users/me/library",Ct))).GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("q=x&q=y")]
    [InlineData("q=a%00b")]
    [InlineData("sort=unknown")]
    [InlineData("sort=title&sort=title")]
    [InlineData("type=movie&type=book")]
    [InlineData("unknown=x")]
    public async Task Invalid_query_is_rejected(string query)
    {
        await using var factory=Factory(); using var client=Client(factory); await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/users/me/library?"+query,Ct)).StatusCode);
    }

    [Fact]
    public async Task Search_length_and_blank_are_validated()
    {
        await using var factory=Factory(); using var client=Client(factory); await SignIn(client);
        await Json(await client.GetAsync("/api/v1/users/me/library?q="+new string('a',200),Ct));
        await Json(await client.GetAsync("/api/v1/users/me/library?q=%20%20",Ct));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/users/me/library?q="+new string('a',201),Ct)).StatusCode);
    }

    [Fact]
    public async Task Status_updates_preserve_current_dates_and_reject_stale_status_or_account()
    {
        await using var factory=Factory(); using var owner=Client(factory);
        var user=await SignIn(owner); var id=await Media(); var path=$"/api/v1/users/me/library/{id}";
        await Json(await owner.PutAsJsonAsync(path,new {status="planned", startedAt="2026-01-01"},Ct));
        async Task<HttpResponseMessage> Save(string status,string expected,string? account=null) {
            using var request=new HttpRequestMessage(HttpMethod.Patch,path+"/status") {Content=JsonContent.Create(new {status,expectedStatus=expected})};
            request.Headers.Add("X-RateFreak-Account-Id",account??user.ToString());
            return await owner.SendAsync(request,Ct);
        }
        // Date-only changes from a different tab must survive the older row's status save.
        await Json(await owner.PutAsJsonAsync(path,new {status="planned", startedAt="2026-02-01",completedAt="2026-02-03"},Ct));
        var updated=await Json(await Save("completed","planned"));
        Assert.Equal("2026-02-01",updated.GetProperty("startedAt").GetString());
        Assert.Equal("2026-02-03",updated.GetProperty("completedAt").GetString());
        Assert.Equal(HttpStatusCode.Conflict,(await Save("paused","planned")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Save("paused","completed",Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Save("bad","completed")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Save("planned","bad")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await owner.DeleteAsync(path,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Save("planned","completed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync(path,Ct)).StatusCode);
    }

    [Fact]
    public async Task Status_mutation_requires_authentication_account_and_csrf()
    {
        await using var factory=Factory(); using var client=Client(factory); var id=await Media();
        var path=$"/api/v1/users/me/library/{id}/status";
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PatchAsJsonAsync(path,new {status="planned",expectedStatus="planned"},Ct)).StatusCode);
        var user=await SignIn(client);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PatchAsJsonAsync(path,new {status="planned",expectedStatus="planned"},Ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-RateFreak-Account-Id",user.ToString());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PatchAsJsonAsync(path,new {status="planned",expectedStatus="planned"},Ct)).StatusCode);
    }

    [Fact]
    public async Task Equal_sort_keys_have_stable_pages_and_hidden_items_are_excluded()
    {
        await using var factory=Factory();using var owner=Client(factory);var user=await SignIn(owner);
        var ids=new[]{await Media("movie","Same"),await Media("movie","same"),await Media("movie","SAME")};
        foreach(var id in ids) {
            await Json(await owner.PutAsJsonAsync($"/api/v1/users/me/library/{id}",new {status="completed",completedAt="2026-02-01"},Ct));
            await Json(await owner.PutAsJsonAsync($"/api/v1/media/{id}/rating",new {score=0},Ct));
        }
        await using var db=Context();
        await db.LibraryEntries.Where(x=>x.UserId==user).ExecuteUpdateAsync(x=>x.SetProperty(e=>e.CreatedAt,new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero)),Ct);
        var expected=ids.OrderBy(x=>x.ToString(),StringComparer.Ordinal).ToArray();
        foreach(var sort in new[]{"recently_added","recently_completed","title","highest_rated","lowest_rated"})
            for(var page=1;page<=3;page++) {
                var result=await Json(await owner.GetAsync($"/api/v1/users/me/library?sort={sort}&pageSize=1&page={page}",Ct));
                Assert.Equal(expected[page-1],result.GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
            }
        await db.MediaItems.Where(x=>x.Id==ids[0]).ExecuteUpdateAsync(x=>x.SetProperty(m=>m.IsHidden,true),Ct);
        var filtered=await Json(await owner.GetAsync("/api/v1/users/me/library?q=same&sort=highest_rated",Ct));
        Assert.Equal(2,filtered.GetProperty("totalCount").GetInt32());
        using var request=new HttpRequestMessage(HttpMethod.Patch,$"/api/v1/users/me/library/{ids[0]}/status") {Content=JsonContent.Create(new {status="paused",expectedStatus="completed"})};
        request.Headers.Add("X-RateFreak-Account-Id",user.ToString());
        Assert.Equal(HttpStatusCode.NotFound,(await owner.SendAsync(request,Ct)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_status_writes_allow_only_one_stale_editor_to_win()
    {
        await using var factory=Factory();using var owner=Client(factory);var user=await SignIn(owner);var id=await Media();
        await Json(await owner.PutAsJsonAsync($"/api/v1/users/me/library/{id}",new {status="planned"},Ct));
        async Task<HttpResponseMessage> Save(string status) {
            using var request=new HttpRequestMessage(HttpMethod.Patch,$"/api/v1/users/me/library/{id}/status") {Content=JsonContent.Create(new {status,expectedStatus="planned"})};
            request.Headers.Add("X-RateFreak-Account-Id",user.ToString());return await owner.SendAsync(request,Ct);
        }
        var responses=await Task.WhenAll(Save("completed"),Save("paused"));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Status_body_and_duplicate_account_header_are_rejected()
    {
        await using var factory=Factory();using var owner=Client(factory);var user=await SignIn(owner);var id=await Media();
        await Json(await owner.PutAsJsonAsync($"/api/v1/users/me/library/{id}",new {status="planned"},Ct));
        owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id",user.ToString());
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PatchAsJsonAsync($"/api/v1/users/me/library/{id}/status",new {status="completed",expectedStatus="planned",startedAt="2026-01-01"},Ct)).StatusCode);
        owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id",user.ToString());
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PatchAsJsonAsync($"/api/v1/users/me/library/{id}/status",new {status="completed",expectedStatus="planned"},Ct)).StatusCode);
    }
}
