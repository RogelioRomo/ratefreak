using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using RateFreak.Infrastructure.Persistence;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class CatalogAccessConcurrencyTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class PairBarrier : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int arrived;
        private readonly TaskCompletionSource both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
            { if (Interlocked.Increment(ref arrived) == 2) both.TrySetResult(); await both.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }
    [Theory]
    [InlineData("rating")]
    [InlineData("membership")]
    public async Task Unpublishing_races_writes_without_persisting_unauthorized_changes(string kind)
    {
        var barrier = new PairBarrier(); await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.AddDbContext<RateFreakDbContext>(o => o.AddInterceptors(barrier)));
        using var owner = Client(factory); using var other = Client(factory); using var anon = Client(factory); await SignIn(owner, Ct); await SignIn(other, Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Race " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
        var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Race subject", categoryId = category.GetProperty("id").GetGuid(), visibility = "public" }, Ct), Ct, HttpStatusCode.Created); var id = item.GetProperty("id").GetGuid();
        var list = await Json(await owner.PostAsJsonAsync("/api/v1/collections", new { name = "Race list", visibility = "public" }, Ct), Ct, HttpStatusCode.Created); var listId = list.GetProperty("collection").GetProperty("id").GetGuid();
        barrier.Enabled = true;
        var changed = owner.PostAsJsonAsync($"/api/v1/media/{id}/unpublish", new { expectedRevision = 1 }, Ct);
        var write = kind == "rating" ? other.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5 }, Ct) : owner.PostAsJsonAsync($"/api/v1/collections/{listId}/items", new { expectedRevision = 1, mediaItemId = id }, Ct);
        var responses = await Task.WhenAll(changed, write); barrier.Enabled = false;
        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        if (kind == "rating") Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }); else Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.BadRequest });
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/media/{id}", Ct)).StatusCode);
        Assert.Equal(0, (await Json(await anon.GetAsync($"/api/v1/collections/{listId}/items", Ct), Ct)).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Category_hide_races_publication_and_never_leaks_a_held_subject()
    {
        var barrier = new PairBarrier(); await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.AddDbContext<RateFreakDbContext>(o => o.AddInterceptors(barrier)));
        using var owner = Client(factory); using var admin = Client(factory); using var anon = Client(factory); await SignIn(owner, Ct); var adminId = await SignIn(admin, Ct);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(adminId, "Race admin", Ct);
        var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Held race " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created); var categoryId = category.GetProperty("id").GetGuid();
        var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Held subject", categoryId }, Ct), Ct, HttpStatusCode.Created); var id = item.GetProperty("id").GetGuid();
        barrier.Enabled = true;
        var responses = await Task.WhenAll(owner.PostAsJsonAsync($"/api/v1/media/{id}/publish", new { expectedRevision = 1 }, Ct), admin.PostAsJsonAsync($"/api/v1/moderation/categories/{categoryId}/hide", new { expectedRevision = 1, reason = "Hold" }, Ct)); barrier.Enabled = false;
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }); Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/media/{id}", Ct)).StatusCode);
    }
}
