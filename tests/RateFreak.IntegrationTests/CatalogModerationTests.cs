using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class CatalogModerationTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Private_item_reports_are_not_found_and_retained_public_reports_allow_scoped_staff_inspection()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString); using var owner = Client(factory); using var reporter = Client(factory); using var admin = Client(factory);
        await SignIn(owner, Ct); await SignIn(reporter, Ct); var adminId = await SignIn(admin, Ct);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(adminId, "Test staff", Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Reported flowers " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Flower", categoryId = category.GetProperty("id").GetGuid() }, Ct), Ct, HttpStatusCode.Created);
        var id = item.GetProperty("id").GetGuid(); var path = $"/api/v1/media/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await reporter.PostAsJsonAsync(path + "/reports", new { reason = "spam" }, Ct)).StatusCode);
        await Json(await owner.PostAsJsonAsync(path + "/publish", new { expectedRevision = 1 }, Ct), Ct);
        var report = await Json(await reporter.PostAsJsonAsync(path + "/reports", new { reason = "spam" }, Ct), Ct, HttpStatusCode.Created);
        await Json(await owner.PostAsJsonAsync(path + "/unpublish", new { expectedRevision = 2 }, Ct), Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/moderation/media/{id}", Ct)).StatusCode);
        var detail = await Json(await admin.GetAsync($"/api/v1/moderation/reports/{report.GetProperty("id").GetGuid()}", Ct), Ct);
        Assert.Equal("Flower", detail.GetProperty("target").GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path, Ct)).StatusCode);
    }
}
