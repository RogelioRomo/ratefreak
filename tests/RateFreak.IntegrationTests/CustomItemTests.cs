using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class CustomItemTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Lifecycle_preserves_shared_contributions_and_restore_returns_private()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var owner = Client(factory); using var other = Client(factory); using var anon = Client(factory);
        await SignIn(owner, Ct); await SignIn(other, Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Flowers " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var created = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Blue hydrangea", description = "Garden flower", categoryId = category.GetProperty("id").GetGuid() }, Ct), Ct, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid(); var path = $"/api/v1/media/{id}";
        Assert.Equal("private", created.GetProperty("visibility").GetString()); Assert.Equal("custom", created.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(path, Ct)).StatusCode);
        var published = await Json(await owner.PostAsJsonAsync(path + "/publish", new { expectedRevision = 1 }, Ct), Ct);
        Assert.Equal("public", published.GetProperty("visibility").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/unpublish", new { expectedRevision = 1 }, Ct)).StatusCode);
        await Json(await other.PutAsJsonAsync(path + "/rating", new { score = 4.5 }, Ct), Ct);
        var review = await Json(await other.PostAsJsonAsync(path + "/reviews", new { body = "A shared review" }, Ct), Ct, HttpStatusCode.Created);
        await Json(await owner.PostAsJsonAsync(path + "/unpublish", new { expectedRevision = 2 }, Ct), Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/reviews/{review.GetProperty("id").GetGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/reviews/{review.GetProperty("id").GetGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path, new { expectedRevision = 3, title = "Hijacked", categoryId = category.GetProperty("id").GetGuid() }, Ct)).StatusCode);
        await Json(await owner.PostAsJsonAsync(path + "/archive", new { expectedRevision = 3 }, Ct), Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path, Ct)).StatusCode);
        var managed = await Json(await owner.GetAsync("/api/v1/media?ownOnly=true", Ct), Ct); Assert.Contains(managed.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        var restored = await Json(await owner.PostAsJsonAsync(path + "/restore", new { expectedRevision = 4 }, Ct), Ct); Assert.Equal("private", restored.GetProperty("visibility").GetString());
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
        Assert.Equal(4.5m, (await db.Ratings.SingleAsync(x => x.MediaItemId == id, Ct)).Score); Assert.True(await db.Reviews.AnyAsync(x => x.MediaItemId == id, Ct));
    }
    [Fact]
    public async Task Publishing_private_category_requires_explicit_public_category_choice()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); await SignIn(owner, Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Private flowers " + Guid.NewGuid(), scope = "private" }, Ct), Ct, HttpStatusCode.Created);
        var payload = new { title = "Hydrangea", categoryId = category.GetProperty("id").GetGuid(), visibility = "public" };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/media", payload, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/media", new { title = new string('x', 301), categoryId = payload.categoryId }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/media", new { title = "Name", categoryId = payload.categoryId, provider = "tmdb" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/media", new { title = "Hydrangea", description = "bad\0description", categoryId = payload.categoryId }, Ct)).StatusCode);
        var created = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Hydrangea", description = new string('x', 20000), categoryId = payload.categoryId }, Ct), Ct, HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/v1/media/{created.GetProperty("id").GetGuid()}/publish", new { expectedRevision = 1 }, Ct)).StatusCode);
    }
}
