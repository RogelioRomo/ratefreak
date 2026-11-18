using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed partial class NotificationTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient c) {
        var j = await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", j.GetProperty("requestToken").GetString());
    }
    private static async Task<Guid> SignIn(HttpClient c) {
        var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test";
        await Token(c);
        var r = await c.PostAsJsonAsync("/api/v1/auth/register", new {email, password="Local-password-72", handle="user_"+tag, displayName="Reader"}, Ct);
        Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        var id = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/v1/auth/login",new {email,password="Local-password-72"},Ct)).StatusCode);
        await Token(c); return id;
    }
    private static async Task<JsonElement> Inbox(HttpClient c, string query="") {
        var r = await c.GetAsync("/api/v1/notifications"+query,Ct);
        Assert.Equal(HttpStatusCode.OK,r.StatusCode); Assert.True(r.Headers.CacheControl?.NoStore);
        return await r.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
    private static async Task<int> Count(HttpClient c) => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread-count",Ct)).GetProperty("unreadCount").GetInt32();
    private static void NoContent(HttpResponseMessage r) => Assert.Equal(HttpStatusCode.NoContent,r.StatusCode);
    private async Task<(Guid media,Guid review)> Review(HttpClient owner) {
        await using var db=Context(); var m=new MediaItem("movie","Notification test"); db.Add(m); await db.SaveChangesAsync(Ct);
        var r=await owner.PostAsJsonAsync($"/api/v1/media/{m.Id}/reviews",new {body="Private text must not be copied"},Ct);
        Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        return (m.Id,(await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid());
    }
    private static async Task<Guid> Comment(HttpClient c,Guid review) {
        var r=await c.PostAsJsonAsync($"/api/v1/reviews/{review}/comments",new {body="Spoiler text",isSpoiler=true},Ct);
        Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Follow_inbox_is_private_repeat_safe_readable_and_dismissal_does_not_recreate() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        var actorId=await SignIn(actor); var ownerId=await SignIn(owner); var path=$"/api/v1/users/{ownerId}/follow";
        foreach(var r in await Task.WhenAll(Enumerable.Range(0,5).Select(_=>actor.PutAsync(path,null,Ct)))) NoContent(r);
        var box=await Inbox(owner); Assert.Equal(1,box.GetProperty("totalCount").GetInt32()); Assert.Equal(1,await Count(owner));
        var item=Assert.Single(box.GetProperty("items").EnumerateArray()); var id=item.GetProperty("id").GetGuid();
        Assert.Equal("follow",item.GetProperty("kind").GetString()); Assert.Equal(actorId,item.GetProperty("actor").GetProperty("id").GetGuid());
        Assert.Equal(0,(await Inbox(actor)).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,(await actor.DeleteAsync($"/api/v1/notifications/{id}",Ct)).StatusCode);
        NoContent(await owner.PutAsync($"/api/v1/notifications/{id}/read",null,Ct)); Assert.Equal(0,await Count(owner));
        NoContent(await owner.PutAsync($"/api/v1/notifications/{id}/read",null,Ct));
        NoContent(await owner.DeleteAsync($"/api/v1/notifications/{id}/read",Ct)); Assert.Equal(1,await Count(owner));
        NoContent(await owner.DeleteAsync($"/api/v1/notifications/{id}",Ct));
        NoContent(await actor.PutAsync(path,null,Ct)); Assert.Equal(0,(await Inbox(owner)).GetProperty("totalCount").GetInt32());
        await using var db=Context(); Assert.True(await db.UserFollows.AnyAsync(x=>x.FollowerId==actorId&&x.FolloweeId==ownerId,Ct));
        NoContent(await actor.DeleteAsync(path,Ct)); NoContent(await actor.PutAsync(path,null,Ct)); Assert.Equal(1,await Count(owner));
        NoContent(await actor.DeleteAsync(path,Ct)); Assert.Equal(0,await Count(owner));
    }
    [Fact]
    public async Task Comments_notify_review_owner_once_without_self_events_or_copied_text() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        await SignIn(actor); await SignIn(owner); var (media,review)=await Review(owner);
        await Comment(owner,review); Assert.Equal(0,await Count(owner));
        var comment=await Comment(actor,review); var box=await Inbox(owner); var item=Assert.Single(box.GetProperty("items").EnumerateArray());
        Assert.Equal("comment",item.GetProperty("kind").GetString()); Assert.Equal(review,item.GetProperty("reviewId").GetGuid()); Assert.Equal(media,item.GetProperty("mediaItemId").GetGuid());
        Assert.DoesNotContain("Spoiler text",box.GetRawText()); Assert.DoesNotContain("Private text",box.GetRawText());
        var id=item.GetProperty("id").GetGuid(); NoContent(await owner.PutAsync($"/api/v1/notifications/{id}/read",null,Ct));
        Assert.Equal(HttpStatusCode.OK,(await actor.PatchAsJsonAsync($"/api/v1/comments/{comment}",new {body="Edited"},Ct)).StatusCode); Assert.Equal(0,await Count(owner));
        await Comment(actor,review); Assert.Equal(2,(await Inbox(owner)).GetProperty("totalCount").GetInt32());
        NoContent(await owner.PostAsync("/api/v1/notifications/read-all",null,Ct)); Assert.Equal(0,await Count(owner));
        NoContent(await actor.DeleteAsync($"/api/v1/comments/{comment}",Ct)); Assert.Equal(1,(await Inbox(owner)).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Hidden_content_uses_live_access_checks_and_clear_includes_hidden_rows() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        var actorId=await SignIn(actor); await SignIn(owner); var (media,review)=await Review(owner); var comment=await Comment(actor,review);
        await using var db=Context();
        await db.Users.Where(x=>x.Id==actorId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsProfileHidden,true),Ct);
        var item=Assert.Single((await Inbox(owner)).GetProperty("items").EnumerateArray());
        Assert.Equal(item.GetProperty("actor").GetProperty("handle").GetString(),item.GetProperty("actor").GetProperty("displayName").GetString());
        var id=item.GetProperty("id").GetGuid();
        await db.ReviewComments.Where(x=>x.Id==comment).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,true),Ct);
        Assert.Equal(0,await Count(owner)); Assert.Empty((await Inbox(owner)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,(await owner.PutAsync($"/api/v1/notifications/{id}/read",null,Ct)).StatusCode);
        await db.ReviewComments.Where(x=>x.Id==comment).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,false),Ct); Assert.Equal(1,await Count(owner));
        await db.Reviews.Where(x=>x.Id==review).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,true),Ct); Assert.Equal(0,await Count(owner));
        await db.Reviews.Where(x=>x.Id==review).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsHidden,false),Ct);
        await db.MediaItems.Where(x=>x.Id==media).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsArchived,true),Ct); Assert.Equal(0,await Count(owner));
        NoContent(await owner.DeleteAsync("/api/v1/notifications",Ct));
        await db.MediaItems.Where(x=>x.Id==media).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsArchived,false),Ct); Assert.Equal(0,await Count(owner));
        Assert.True(await db.ReviewComments.AnyAsync(x=>x.Id==comment,Ct));
        await Comment(actor,review); Assert.Equal(1,await Count(owner));
    }
    [Fact]
    public async Task Every_route_requires_auth_and_mutations_require_csrf() {
        await using var f=Factory(); using var c=Client(f); var id=Guid.NewGuid();
        var writes=new[]{("PUT",$"/{id}/read"),("DELETE",$"/{id}/read"),("DELETE",$"/{id}"),("DELETE",""),("POST","/read-all")};
        foreach(var path in new[]{"","/unread-count"}) Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/v1/notifications"+path,Ct)).StatusCode);
        foreach(var (method,path) in writes) Assert.Equal(HttpStatusCode.Unauthorized,(await c.SendAsync(new(new HttpMethod(method),"/api/v1/notifications"+path),Ct)).StatusCode);
        await SignIn(c); c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        foreach(var (method,path) in writes) Assert.Equal(HttpStatusCode.BadRequest,(await c.SendAsync(new(new HttpMethod(method),"/api/v1/notifications"+path),Ct)).StatusCode);
        await Token(c);
        foreach(var query in new[]{"?page=0","?page=10001","?pageSize=101","?page=bad"}) Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/v1/notifications"+query,Ct)).StatusCode);
        NoContent(await c.DeleteAsync("/api/v1/notifications",Ct));
    }
}
