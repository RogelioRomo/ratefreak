using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Tmdb;
using RateFreak.Api.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class TmdbLifecycleTests : IAsyncLifetime
{
    private readonly AccountDatabase database = new();
    public ValueTask InitializeAsync() => database.InitializeAsync();
    public ValueTask DisposeAsync() => database.DisposeAsync();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Database() => new(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Startup_expires_legacy_null_sync_at_UTC_calendar_deadline_even_when_provider_disabled()
    {
        var legacy = new MediaItem("movie", "Original", "Provider text", new DateOnly(1970, 1, 1), "https://image.tmdb.org/t/p/w500/test.jpg", """{"runtime":144}""");
        var fresh = new MediaItem("movie", "Still fresh");
        var future = new MediaItem("movie", "Forged clock");
        var other = new MediaItem("album", "Other provider");
        var created = new DateTimeOffset(2023, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var deadline = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
        await using var db = Database();
        db.AddRange(legacy, fresh, other, future,
            new MediaExternalId(future.Id, "tmdb", "movie", "46", lastSyncedAt: deadline.AddYears(3)),
            new MediaExternalId(legacy.Id, "tmdb", "movie", "44"),
            new MediaExternalId(fresh.Id, "tmdb", "movie", "45", lastSyncedAt: created.AddSeconds(1)),
            new MediaExternalId(other.Id, "musicbrainz", "release-group", Guid.NewGuid().ToString()));
        var user = new RateFreak.Infrastructure.Authentication.AccountUser { Id = Guid.NewGuid(), UserName = "reader", NormalizedUserName = "READER", Email = "reader@example.test", NormalizedEmail = "READER@EXAMPLE.TEST", DisplayName = "Reader" };
        var list = new RateFreak.Domain.Collections.Collection(user.Id, "Preserved list", null, "private");
        var rating = new RateFreak.Domain.PersonalMedia.Rating(user.Id, legacy.Id, 4.5m);
        var review = new RateFreak.Domain.PersonalMedia.Review(user.Id, legacy.Id, "User-authored words", true);
        var entry = new RateFreak.Domain.PersonalMedia.LibraryEntry(user.Id, legacy.Id, "completed", null, new DateOnly(2023,9,2));
        var membership = new RateFreak.Domain.Collections.CollectionItem(list.Id, legacy.Id, 0);
        db.AddRange(user, list, rating, review, entry, membership);
        await db.SaveChangesAsync(Ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_items SET created_at = {created}, is_hidden = true, is_archived = true WHERE id = {legacy.Id}", Ct);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.AddSingleton<TimeProvider>(new Clock(deadline)));
        using var client = factory.CreateClient();
        db.ChangeTracker.Clear();
        var expired = await db.MediaItems.SingleAsync(x => x.Id == legacy.Id, Ct);
        Assert.Equal("Unavailable title", expired.Title);
        Assert.Null(expired.ImageReference);
        Assert.Null(expired.Description);
        Assert.Null(expired.ReleaseDate);
        Assert.Equal("{}", expired.Metadata);
        Assert.True(expired.IsArchived && expired.IsHidden);
        Assert.Equal(legacy.CategoryId, expired.CategoryId);
        Assert.Equal(created, expired.CreatedAt);
        Assert.Equal(2, expired.Revision);
        Assert.Null((await db.MediaExternalIds.SingleAsync(x => x.MediaItemId == legacy.Id, Ct)).LastSyncedAt);
        Assert.Equal("Unavailable title", (await db.MediaItems.FindAsync([future.Id], Ct))!.Title);
        Assert.Equal("Still fresh", (await db.MediaItems.FindAsync([fresh.Id], Ct))!.Title);
        Assert.Equal("Other provider", (await db.MediaItems.FindAsync([other.Id], Ct))!.Title);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TmdbLifecycle>().MaintainAsync(db.Database.GetDbConnection().Database, null, true, [], Ct);
        db.ChangeTracker.Clear();
        Assert.Equal(legacy.Id, (await db.CollectionItems.SingleAsync(Ct)).MediaItemId);
        Assert.Equal(4.5m, (await db.Ratings.SingleAsync(Ct)).Score);
        Assert.Equal("User-authored words", (await db.Reviews.SingleAsync(Ct)).Body);
        Assert.Equal(entry.CompletedAt, (await db.LibraryEntries.SingleAsync(Ct)).CompletedAt);
        Assert.Equal("Other provider", (await db.MediaItems.FindAsync([other.Id], Ct))!.Title);
    }

    private sealed class Transport : HttpMessageHandler
    {
        public Func<string, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (id, _) => Task.FromResult(Response(id, "Refreshed"));
        public List<string> Ids { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var id = request.RequestUri!.Segments[^1];
            Ids.Add(id);
            return Respond(id, ct);
        }
    }

    private static HttpResponseMessage Response(string id, string title) => new(HttpStatusCode.OK)
    { Content = new StringContent($$"""{"id":{{id}},"title":"{{title}}","overview":"Updated","poster_path":"/new.jpg","runtime":125}""") };

    [Fact]
    public async Task Fresh_refresh_retains_identity_retries_failures_and_offline_block_wins_over_inflight_HTTP()
    {
        var now = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
        var sync = new DateTimeOffset(2023, 9, 30, 12, 0, 0, TimeSpan.Zero);
        await using var db = Database();
        using var transport = new Transport();
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton<TimeProvider>(new Clock(now));
            services.PostConfigure<TmdbOptions>(o => { o.Enabled = true; o.AccessToken = "test-only"; });
            services.AddHttpClient<IMediaProvider, TmdbMediaProvider>().ConfigurePrimaryHttpMessageHandler(() => transport);
        });
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IMediaProviderRegistry>().Find("tmdb")!;
        transport.Respond = (id, _) => Task.FromResult(Response(id, "Cached preview"));
        Assert.Equal("Cached preview", (await provider.GetDetailsAsync("203", "movie", Ct))!.Title);
        var items = Enumerable.Range(201, 5).Select(id => new MediaItem("movie", "Earlier " + id,
            imageReference: "https://image.tmdb.org/t/p/w500/old" + id + ".jpg")).ToArray();
        for (var i = 0; i < items.Length; i++)
            db.AddRange(items[i], new MediaExternalId(items[i].Id, "tmdb", "movie", (201 + i).ToString(), lastSyncedAt: sync.AddSeconds(i - 3)));
        await db.SaveChangesAsync(Ct);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Respond = async (id, ct) =>
        {
            if (id == "201")
                return new(HttpStatusCode.ServiceUnavailable);
            if (id == "202") return new(HttpStatusCode.NotFound);
            if (id == "204") { started.TrySetResult(); await release.Task.WaitAsync(ct); }
            return Response(id, "Fresh " + id);
        };
        var lifecycle = scope.ServiceProvider.GetRequiredService<TmdbLifecycle>();
        var refreshing = lifecycle.RefreshAsync(Ct);
        try
        {
            await Task.WhenAny(refreshing, started.Task).WaitAsync(TimeSpan.FromSeconds(10), Ct);
            if (refreshing.IsCompleted) await refreshing;
            Assert.True(started.Task.IsCompleted, $"Unexpected refresh requests: {string.Join(',', transport.Ids)}");
            await using var maintenanceScope = factory.Services.CreateAsyncScope();
            var service = maintenanceScope.ServiceProvider.GetRequiredService<TmdbLifecycle>();
            await service.MaintainAsync(db.Database.GetDbConnection().Database, "movie:204", false, ["searchOnly.jpg"], Ct);
        }
        finally { release.TrySetResult(); }
        await refreshing;
        db.ChangeTracker.Clear();
        Assert.Equal("Earlier 201", (await db.MediaItems.FindAsync([items[0].Id], Ct))!.Title);
        Assert.Equal("Unavailable title", (await db.MediaItems.FindAsync([items[1].Id], Ct))!.Title);
        Assert.Equal("Fresh 203", (await db.MediaItems.FindAsync([items[2].Id], Ct))!.Title);
        Assert.Equal("Unavailable title", (await db.MediaItems.FindAsync([items[3].Id], Ct))!.Title);
        Assert.Equal("Earlier 205", (await db.MediaItems.FindAsync([items[4].Id], Ct))!.Title);
        var retry = await db.Database.SqlQuery<DateTimeOffset>($"""
            SELECT s.next_attempt_at AS "Value" FROM tmdb_lifecycle_states s
            JOIN media_external_ids x ON x.id = s.mapping_id WHERE x.media_item_id = {items[0].Id}
            """).SingleAsync(Ct);
        Assert.Equal(now.AddMinutes(1), retry);
        Assert.Equal(sync.AddSeconds(-3), (await db.MediaExternalIds.SingleAsync(x => x.MediaItemId == items[0].Id, Ct)).LastSyncedAt);
        Assert.Equal(now, (await db.MediaExternalIds.SingleAsync(x => x.MediaItemId == items[2].Id, Ct)).LastSyncedAt);
        Assert.Null(await provider.GetDetailsAsync("204", "movie", Ct));
        var policy = scope.ServiceProvider.GetRequiredService<TmdbContentPolicy>().Read();
        Assert.Contains("old204.jpg", policy.BlockedPosters);
        Assert.Contains("searchOnly.jpg", policy.BlockedPosters);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Retry_after_stops_the_batch_and_defers_other_workers_while_expiry_continues(HttpStatusCode status)
    {
        var now = new DateTimeOffset(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);
        var clock = new Clock(now);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services =>
            services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient();
        var policy = factory.Services.GetRequiredService<TmdbContentPolicy>();
        await using var db = Database();
        await using var replicaDb = Database();
        using var limitedTransport = new Transport
        {
            Respond = (_, _) =>
            {
                var response = new HttpResponseMessage(status);
                response.Headers.RetryAfter = new(TimeSpan.FromHours(4));
                return Task.FromResult(response);
            }
        };
        using var healthyTransport = new Transport();
        using var http = new HttpClient(limitedTransport);
        using var replicaHttp = new HttpClient(healthyTransport);
        using var gate = new TmdbRequestGate();
        var options = Microsoft.Extensions.Options.Options.Create(new TmdbOptions { Enabled = true, AccessToken = "fixture" });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbMediaProvider>.Instance;
        var worker = new TmdbLifecycle(db, policy, [new TmdbMediaProvider(http, options, gate, logger)], clock);
        var replica = new TmdbLifecycle(replicaDb, policy, [new TmdbMediaProvider(replicaHttp, options, gate, logger)], clock);
        var items = Enumerable.Range(301, 3).Select(id => new MediaItem("movie", "Original " + id)).ToArray();
        for (var i = 0; i < items.Length; i++)
            db.AddRange(items[i], new MediaExternalId(items[i].Id, "tmdb", "movie", (301 + i).ToString(),
                lastSyncedAt: now.AddMonths(-6).AddSeconds(10 + i)));
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        await worker.RefreshAsync(Ct);
        Assert.Equal(["301"], limitedTransport.Ids);
        clock.Now = now.AddMinutes(2);
        await replica.RefreshAsync(Ct);
        Assert.Empty(healthyTransport.Ids);
        await replica.ReconcileAsync(Ct);
        Assert.Equal(3, await db.MediaItems.CountAsync(item => item.Title == "Unavailable title", Ct));

        clock.Now = now.AddHours(4);
        await replica.RefreshAsync(Ct);
        Assert.Equal(["302", "303", "301"], healthyTransport.Ids);
        Assert.Equal(3, await db.MediaItems.CountAsync(item => item.Title == "Refreshed", Ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_policy_withholds_catalog_but_still_expires_overdue_records(bool corruptPolicy)
    {
        await using var db = Database();
        var now = new DateTimeOffset(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);
        var fresh = new MediaItem("movie", "Fresh title");
        var expired = new MediaItem("movie", "Old title");
        db.AddRange(fresh, expired, new MediaExternalId(fresh.Id, "tmdb", "movie", "51", lastSyncedAt: now),
            new MediaExternalId(expired.Id, "tmdb", "movie", "52", lastSyncedAt: now.AddYears(-1)));
        await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton<TimeProvider>(new Clock(now));
            services.PostConfigure<TmdbOptions>(o =>
            {
                if (corruptPolicy) File.WriteAllText(o.PolicyPath!, "{");
                else File.Delete(o.PolicyPath!);
            });
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/media", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/categories", Ct)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal("Fresh title", (await db.MediaItems.FindAsync([fresh.Id], Ct))!.Title);
        Assert.Equal("Unavailable title", (await db.MediaItems.FindAsync([expired.Id], Ct))!.Title);
    }

    [Fact]
    public async Task Unrepresentable_sync_timestamp_does_not_starve_healthy_refresh_or_catalog_access()
    {
        var now = new DateTimeOffset(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);
        var clock = new Clock(now);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services =>
            services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient();
        var policy = factory.Services.GetRequiredService<TmdbContentPolicy>();
        await using var db = Database();
        using var transport = new Transport();
        using var http = new HttpClient(transport);
        using var gate = new TmdbRequestGate();
        var adapter = new TmdbMediaProvider(http, Microsoft.Extensions.Options.Options.Create(new TmdbOptions { Enabled = true, AccessToken = "fixture" }),
            gate, Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbMediaProvider>.Instance);
        var lifecycle = new TmdbLifecycle(db, policy, [adapter], clock);
        var corrupted = new MediaItem("movie", "Corrupted provider title");
        var healthy = new MediaItem("movie", "Healthy provider title");
        var corruptedMapping = new MediaExternalId(corrupted.Id, "tmdb", "movie", "401", lastSyncedAt: now);
        db.AddRange(corrupted, healthy, corruptedMapping,
            new MediaExternalId(healthy.Id, "tmdb", "movie", "402", lastSyncedAt: now.AddMonths(-5)));
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_external_ids SET last_synced_at = '12000-01-01T00:00:00Z'::timestamptz WHERE id = {corruptedMapping.Id}
            """, Ct);

        await lifecycle.RefreshAsync(Ct);
        Assert.Equal(["402"], transport.Ids);
        await lifecycle.ReconcileAsync(Ct);
        var details = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/media/{corrupted.Id}", Ct);
        Assert.Equal("Unavailable title", details.GetProperty("title").GetString());

        clock.Now = now.AddMinutes(1);
        await lifecycle.RefreshAsync(Ct);
        Assert.Equal(["402", "401"], transport.Ids);
    }

    [Fact]
    public async Task Bounded_batches_preserve_fairness_and_expiry_through_a_backlog_of_invalid_responses()
    {
        var now = new DateTimeOffset(2026, 11, 24, 1, 0, 0, TimeSpan.Zero);
        var clock = new Clock(now);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services =>
            services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var db = Database();
        using var transport = new Transport { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {Content = new StringContent("""{"id":9,"title":"Wrong identity"}""")}) };
        using var http = new HttpClient(transport);
        using var gate = new TmdbRequestGate();
        var adapter = new TmdbMediaProvider(http, Microsoft.Extensions.Options.Options.Create(new TmdbOptions { Enabled = true, AccessToken = "fixture" }),
            gate, Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbMediaProvider>.Instance);
        var lifecycle = new TmdbLifecycle(db, scope.ServiceProvider.GetRequiredService<TmdbContentPolicy>(), [adapter], clock);
        var start = now.AddMonths(-6).AddMinutes(1);
        var items = Enumerable.Range(0,101).Select(i => new MediaItem("movie", "Original " + i)).ToArray();
        for (var i = 0; i < items.Length; i++) db.AddRange(items[i],
            new MediaExternalId(items[i].Id, "tmdb", "movie", (1000 + i).ToString(), lastSyncedAt: start.AddMilliseconds(i)));
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        await lifecycle.RefreshAsync(Ct);
        Assert.Equal(100, transport.Ids.Count);
        Assert.DoesNotContain("1100", transport.Ids);
        await lifecycle.RefreshAsync(Ct);
        Assert.Equal(100, transport.Ids.Count);
        Assert.Equal("Original 0", (await db.MediaItems.FindAsync([items[0].Id], Ct))!.Title);
        clock.Now = now.AddMinutes(2);
        await lifecycle.ReconcileAsync(Ct);
        db.ChangeTracker.Clear();
        Assert.Equal(101, await db.MediaItems.CountAsync(i => i.Title == "Unavailable title", Ct));
        transport.Respond = (id, _) => Task.FromResult(Response(id, "Recovered " + id));
        await lifecycle.RefreshAsync(Ct);
        Assert.Equal("1100", transport.Ids[100]);
        db.ChangeTracker.Clear();
        Assert.Equal("Recovered 1100", (await db.MediaItems.FindAsync([items[100].Id], Ct))!.Title);
        Assert.Equal(200, transport.Ids.Count);
    }
}
