using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class CatalogAccessTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task<Guid> SignIn(HttpClient client)
    {
        async Task Token()
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        }
        await Token(); var tag = Guid.NewGuid().ToString("N")[..12]; var email = tag + "@example.test";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Reader" }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var account = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Local-password-72" }, Ct)).StatusCode);
        await Token(); return account.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Private_items_and_contributions_are_not_found_through_indirect_ids()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var owner = Client(factory); using var stranger = Client(factory); using var anonymous = Client(factory);
        var ownerId = await SignIn(owner); var otherId = await SignIn(stranger);
        await using var db = Context();
        var category = new Category("Secret flowers", "private", ownerId); db.Add(category);
        var item = MediaItem.CreateCustom(ownerId, "Secret hydrangea", null, category.Id); db.Add(item);
        var review = new Review(otherId, item.Id, "Previously public review", false); db.Add(review);
        var comment = new ReviewComment(otherId, review.Id, "Previously public comment", false); db.Add(comment);
        db.Add(new Rating(otherId, item.Id, 4.5m)); db.Add(new LibraryEntry(otherId, item.Id, "planned", null, null)); await db.SaveChangesAsync(Ct);
        string[] paths = [$"/api/v1/media/{item.Id}", $"/api/v1/media/{item.Id}/ratings", $"/api/v1/media/{item.Id}/reviews", $"/api/v1/reviews/{review.Id}", $"/api/v1/reviews/{review.Id}/comments", $"/api/v1/comments/{comment.Id}"];
        foreach (var client in new[] { stranger, anonymous })
            foreach (var path in paths)
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Ct)).StatusCode);
        foreach (var path in paths) Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(path, Ct)).StatusCode);
        var results = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/media?q=Secret%20hydrangea", Ct); Assert.Equal(0, results.GetProperty("totalCount").GetInt32());
        var library = await stranger.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library", Ct); Assert.Equal(0, library.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/v1/media/{item.Id}/rating", new { score = 3m }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsJsonAsync($"/api/v1/reviews/{review.Id}", new { body = "Changed" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/comments/{comment.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/media/{item.Id}/rating", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/users/me/library/{item.Id}", Ct)).StatusCode);
        Assert.True(await db.ReviewComments.AnyAsync(x => x.Id == comment.Id, Ct)); Assert.Equal(4.5m, (await db.Ratings.SingleAsync(x => x.MediaItemId == item.Id, Ct)).Score);
    }
    [Theory]
    [InlineData("private", false, false, false)]
    [InlineData("public", true, false, false)]
    [InlineData("public", false, true, false)]
    [InlineData("public", false, false, true)]
    public async Task Ordinary_admin_reads_do_not_bypass_item_or_category_policy(string visibility, bool archived, bool held, bool privateCategory)
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); using var admin = Client(factory); using var anon = Client(factory);
        var ownerId = await SignIn(owner); var adminId = await SignIn(admin);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<RateFreak.Application.Moderation.IModerationService>().AppointAdminAsync(adminId, "Access policy fixture", Ct);
        await using var db = Context(); var category = new Category("Policy category " + Guid.NewGuid(), privateCategory ? "private" : "public", ownerId); db.Add(category);
        var item = MediaItem.CreateCustom(ownerId, "Policy secret " + Guid.NewGuid(), null, category.Id); db.Add(item); await db.SaveChangesAsync(Ct);
        await db.MediaItems.Where(m => m.Id == item.Id).ExecuteUpdateAsync(set => set.SetProperty(m => m.Visibility, visibility).SetProperty(m => m.IsArchived, archived).SetProperty(m => m.IsHidden, held), Ct);
        foreach (var client in new[] { admin, anon })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/media/{item.Id}", Ct)).StatusCode);
            var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/media?q=" + Uri.EscapeDataString(item.Title), Ct); Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        }
    }

}
