using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using RateFreak.Infrastructure.Persistence;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class CategoryTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Duplicate_names_are_scoped_and_public_categories_are_immediately_readable()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var a = Client(factory); using var b = Client(factory); using var anon = Client(factory);
        await SignIn(a, Ct); await SignIn(b, Ct); var name = "Flowers " + Guid.NewGuid().ToString("N");
        await Json(await a.PostAsJsonAsync("/api/v1/categories", new { name, scope = "private" }, Ct), Ct, HttpStatusCode.Created);
        await Json(await b.PostAsJsonAsync("/api/v1/categories", new { name, scope = "private" }, Ct), Ct, HttpStatusCode.Created);
        var publicCategory = await Json(await a.PostAsJsonAsync("/api/v1/categories", new { name, scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await b.PostAsJsonAsync("/api/v1/categories", new { name = "  " + name.ToUpperInvariant() + "  ", scope = "public" }, Ct)).StatusCode);
        var page = await Json(await anon.GetAsync("/api/v1/categories?q=" + Uri.EscapeDataString(name), Ct), Ct);
        Assert.Single(page.GetProperty("items").EnumerateArray()); Assert.Equal(publicCategory.GetProperty("id").GetGuid(), page.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain("ownerUserId", page.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/v1/categories", new { name, scope = "public", fields = new object[0] }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Only_admin_can_merge_public_categories_and_seeded_categories_are_protected()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var author = Client(factory); using var admin = Client(factory);
        await SignIn(author, Ct); var adminId = await SignIn(admin, Ct);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(ModerationStatus.Success, (await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(adminId, "Test administrator", Ct)).Status);
        var source = await Json(await author.PostAsJsonAsync("/api/v1/categories", new { name = "Source " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var target = await Json(await author.PostAsJsonAsync("/api/v1/categories", new { name = "Target " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var path = $"/api/v1/moderation/categories/{source.GetProperty("id").GetGuid()}/merge";
        var request = new { expectedRevision = 1, targetId = target.GetProperty("id").GetGuid(), targetExpectedRevision = 1, reason = "Combine duplicates" };
        Assert.Equal(HttpStatusCode.Forbidden, (await author.PostAsJsonAsync(path, request, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path, request, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, request, Ct)).StatusCode);
        var aliasSearch = await Json(await author.GetAsync("/api/v1/categories?q=" + Uri.EscapeDataString(source.GetProperty("name").GetString()!), Ct), Ct);
        Assert.Contains(aliasSearch.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == target.GetProperty("id").GetGuid());
        var targetId = target.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/moderation/categories/{targetId}/hide", new { expectedRevision = 2, reason = "Hold for review" }, Ct)).StatusCode);
        var held = await Json(await admin.GetAsync("/api/v1/moderation/categories", Ct), Ct);
        Assert.Contains(held.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == targetId && c.GetProperty("isHidden").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await author.GetAsync("/api/v1/moderation/categories", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/moderation/categories/{targetId}/restore", new { expectedRevision = 3, reason = "Resolved hold" }, Ct)).StatusCode);
        var seed = RateFreak.Domain.Catalog.Category.ForMediaType("movie");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/moderation/categories/{seed}/hide", new { expectedRevision = 1, reason = "Hide" }, Ct)).StatusCode);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
        Assert.True(await db.CategoryAliases.AnyAsync(x => x.SourceCategoryId == source.GetProperty("id").GetGuid(), Ct));
        Assert.True(await db.ModerationEvents.AnyAsync(x => x.TargetType == "category" && x.Action == "merge", Ct));
    }
}
