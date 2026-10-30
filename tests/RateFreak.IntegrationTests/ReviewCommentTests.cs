using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ReviewCommentTests(AccountDatabase database) : IClassFixture<AccountDatabase>
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


    private async Task<Guid> Review(HttpClient owner)
    {
        var mediaId=await Media();
        var review=await Json(await owner.PostAsJsonAsync($"/api/v1/media/{mediaId}/reviews",new { body="Parent review" },Ct),HttpStatusCode.Created);
        return review.GetProperty("id").GetGuid();
    }
    private static string List(Guid reviewId)=>$"/api/v1/reviews/{reviewId}/comments";
    private static string Detail(Guid commentId)=>$"/api/v1/comments/{commentId}";
    private static async Task<JsonElement> Create(HttpClient client,Guid reviewId,string body="A comment") =>
        await Json(await client.PostAsJsonAsync(List(reviewId),new { body },Ct),HttpStatusCode.Created);

    [Fact]
    public async Task Comments_are_public_literal_text_and_only_comment_author_can_change_them()
    {
        await using var factory=Factory();using var owner=Client(factory);using var author=Client(factory);using var anonymous=Client(factory);
        await SignIn(owner);var authorId=await SignIn(author);var reviewId=await Review(owner);
        var empty=await Json(await anonymous.GetAsync(List(reviewId),Ct));Assert.Equal(0,empty.GetProperty("totalCount").GetInt32());
        var response=await author.PostAsJsonAsync(List(reviewId),new { body="  <b>Literal</b>\nText  ",isSpoiler=true },Ct);
        var comment=await Json(response,HttpStatusCode.Created);var id=comment.GetProperty("id").GetGuid();
        Assert.Equal(Detail(id),response.Headers.Location!.OriginalString);Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(reviewId,comment.GetProperty("reviewId").GetGuid());Assert.Equal(authorId,comment.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal(new[] {"displayName","handle","id"},comment.GetProperty("author").EnumerateObject().Select(p=>p.Name).Order().ToArray());
        Assert.Equal("<b>Literal</b>\nText",comment.GetProperty("body").GetString());Assert.True(comment.GetProperty("isSpoiler").GetBoolean());
        var created=comment.GetProperty("createdAt").GetDateTimeOffset();
        await Json(await anonymous.GetAsync(Detail(id),Ct));
        Assert.Equal(HttpStatusCode.NotFound,(await owner.PatchAsJsonAsync(Detail(id),new { body="Hijack" },Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.DeleteAsync(Detail(id),Ct)).StatusCode);
        var updated=await Json(await author.PatchAsJsonAsync(Detail(id),new { body="Edited" },Ct));
        Assert.Equal(id,updated.GetProperty("id").GetGuid());Assert.Equal(reviewId,updated.GetProperty("reviewId").GetGuid());
        Assert.Equal(created,updated.GetProperty("createdAt").GetDateTimeOffset());Assert.True(updated.GetProperty("updatedAt").GetDateTimeOffset()>=created);
        Assert.Equal("Edited",updated.GetProperty("body").GetString());Assert.False(updated.GetProperty("isSpoiler").GetBoolean());
        await Create(author,reviewId);Assert.Equal(2,(await Json(await anonymous.GetAsync(List(reviewId),Ct))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent,(await author.DeleteAsync(Detail(id),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await author.DeleteAsync(Detail(id),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(Detail(id),Ct)).StatusCode);
    }

    [Theory]
    [InlineData("POST")] [InlineData("PATCH")] [InlineData("DELETE")]
    public async Task Every_mutation_requires_session_and_csrf(string method)
    {
        await using var factory=Factory();using var author=Client(factory);using var anonymous=Client(factory);await SignIn(author);
        var reviewId=await Review(author);var comment=await Create(author,reviewId);var id=comment.GetProperty("id").GetGuid();
        var path=method=="POST"?List(reviewId):Detail(id);
        Task<HttpResponseMessage> Send(HttpClient client)=>client.SendAsync(new HttpRequestMessage(new HttpMethod(method),path) { Content=JsonContent.Create(new { body="Changed" }) },Ct);
        var unauth=await Send(anonymous);Assert.Equal(HttpStatusCode.Unauthorized,unauth.StatusCode);Assert.True(unauth.Headers.CacheControl?.NoStore);
        author.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(author)).StatusCode);
        Assert.Equal("A comment",(await Json(await author.GetAsync(Detail(id),Ct))).GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"body\":\"fine\",\"isSpoiler\":true,\"isSpoiler\":false}")]
    [InlineData("{\"body\":\"bad\\u0007\"}")]
    [InlineData("{}") ] [InlineData("{\"body\":null}")] [InlineData("{\"body\":\"  \\n \\t\"}")]
    [InlineData("{\"body\":\"bad\\u0000\"}")] [InlineData("{\"body\":\"\\uD800\"}")]
    [InlineData("{\"body\":\"fine\",\"userId\":\"spoof\"}")]
    [InlineData("{\"body\":\"fine\",\"reviewId\":\"spoof\"}")]
    [InlineData("{\"body\":null,\"body\":\"fine\"}")] [InlineData("{\"body\":\"fine\",\"isSpoiler\":\"yes\"}")]
    public async Task Invalid_payloads_cannot_create_or_change_comments(string body)
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);var reviewId=await Review(client);
        var existing=await Create(client,reviewId);var id=existing.GetProperty("id").GetGuid();
        foreach(var path in new[] { List(reviewId),Detail(id) })
        {
            using var request=new HttpRequestMessage(path==List(reviewId)?HttpMethod.Post:HttpMethod.Patch,path) { Content=new StringContent(body,Encoding.UTF8,"application/json") };
            Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(request,Ct)).StatusCode);
        }
        var current=await Json(await client.GetAsync(Detail(id),Ct));Assert.Equal(existing.GetRawText(),current.GetRawText());
        Assert.Equal(1,(await Json(await client.GetAsync(List(reviewId),Ct))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Body_boundary_trims_input_and_accepts_valid_surrogate_pairs()
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);var reviewId=await Review(client);
        var text=string.Concat(Enumerable.Repeat("😀",1000));
        Assert.Equal(text,(await Create(client,reviewId,"  "+text+"  ")).GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(List(reviewId),new { body=text+"x" },Ct)).StatusCode);
    }

    [Fact]
    public async Task Missing_targets_and_invalid_paging_return_expected_statuses()
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);var reviewId=await Review(client);
        foreach(var query in new[] { "page=0","page=10001","pageSize=0","pageSize=101","page=bad" })
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(List(reviewId)+"?"+query,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(List(reviewId)+"?page=10000&pageSize=100",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(List(Guid.NewGuid()),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(List(Guid.NewGuid()),new { body="Missing parent" },Ct)).StatusCode);
        var path=Detail(Guid.NewGuid());Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PatchAsJsonAsync(path,new { body="Missing" },Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.DeleteAsync(path,Ct)).StatusCode);
    }
    [Fact]
    public async Task Empty_review_id_is_a_missing_target_not_a_server_error()
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(List(Guid.Empty),new { body="Comment" },Ct)).StatusCode);
    }

}
