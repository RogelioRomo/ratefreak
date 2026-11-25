using Microsoft.EntityFrameworkCore;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Providers.Tmdb;

public sealed class TmdbLifecycle(RateFreakDbContext db, TmdbContentPolicy policy,
    IEnumerable<IMediaProvider> providers, TimeProvider clock)
{
    // The maintenance lock covers policy writes and every transaction that can install provider fields.
    private const long MaintenanceLock = 81432946201;
    private const long RefreshLock = 81432946202;
    private const int BatchSize = 100;

    internal static Task AcquireWriterAsync(RateFreakDbContext context, CancellationToken ct) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({MaintenanceLock})", ct);

    internal static async Task RememberPosterAsync(RateFreakDbContext context, Guid mappingId, string? reference, CancellationToken ct)
    {
        if (TmdbContentPolicy.PosterFilename(reference) is { } filename)
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tmdb_poster_identities(mapping_id, filename) VALUES ({mappingId}, {filename}) ON CONFLICT DO NOTHING
                """, ct);
    }

    public async Task<string> ReconcileAsync(CancellationToken ct)
    {
        TmdbPolicySnapshot snapshot;
        try { snapshot = policy.Read(); }
        catch (IOException)
        {
            await RemoveDueContentAsync(null, ct);
            throw;
        }
        await RemoveDueContentAsync(snapshot, ct);
        if (policy.Read().Fingerprint != snapshot.Fingerprint) throw new IOException("TMDB policy changed during reconciliation.");
        return snapshot.Fingerprint;
    }

    private async Task RemoveDueContentAsync(TmdbPolicySnapshot? snapshot, CancellationToken ct)
    {
        while (await RemoveBatchAsync(snapshot, ct) != 0) ct.ThrowIfCancellationRequested();
    }

    private async Task<int> RemoveBatchAsync(TmdbPolicySnapshot? snapshot, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var terminated = snapshot?.Terminated ?? false;
        var ids = snapshot?.BlockedIdentities.ToArray() ?? [];
        var images = snapshot?.BlockedPosters.Select(p => TmdbContentPolicy.PosterPrefix + p).ToArray() ?? [];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireWriterAsync(db, ct);
        // FOR UPDATE serializes a refresh/moderation change with capture of the pre-removal fields.
        var changed = await db.Database.SqlQuery<int>($"""
            WITH candidates AS MATERIALIZED (
                SELECT x.id, m.id AS item, m.image_reference, due.purge, validity.invalid_sync
                FROM media_external_ids x JOIN media_items m ON m.id = x.media_item_id
                LEFT JOIN tmdb_lifecycle_states s ON s.mapping_id = x.id
                CROSS JOIN LATERAL (SELECT COALESCE(x.last_synced_at, m.created_at) AS synced) t
                CROSS JOIN LATERAL (SELECT isfinite(x.last_synced_at) AND
                    (x.last_synced_at < '0001-01-01 00:00:00Z'::timestamptz
                     OR x.last_synced_at >= '10000-01-01 00:00:00Z'::timestamptz) AS invalid_sync) validity
                CROSS JOIN LATERAL (SELECT ({terminated} OR x.namespace || ':' || x.external_id = ANY({ids})
                    OR CASE WHEN NOT isfinite(t.synced) OR t.synced > {now} THEN true
                       ELSE (t.synced AT TIME ZONE 'UTC') + interval '6 months' <= ({now} AT TIME ZONE 'UTC') END) AS purge) due
                WHERE x.provider = 'tmdb' AND (
                    (due.purge AND (s.content_removed IS DISTINCT FROM true OR m.title <> 'Unavailable title' OR m.description IS NOT NULL
                       OR m.release_date IS NOT NULL OR m.image_reference IS NOT NULL OR m.metadata <> jsonb_build_object()))
                    OR validity.invalid_sync OR m.image_reference = ANY({images}))
                ORDER BY x.id LIMIT {BatchSize} FOR UPDATE OF x, m
            ), normalized AS (
                UPDATE media_external_ids x SET last_synced_at = '-infinity'::timestamptz
                FROM candidates c WHERE c.id = x.id AND c.invalid_sync
            ), remembered AS (
                INSERT INTO tmdb_poster_identities(mapping_id,filename)
                SELECT id, substring(image_reference FROM {TmdbContentPolicy.PosterPrefix.Length + 1}) FROM candidates
                WHERE starts_with(image_reference, {TmdbContentPolicy.PosterPrefix})
                  AND substring(image_reference FROM {TmdbContentPolicy.PosterPrefix.Length + 1}) ~ '^[A-Za-z0-9_-]+\.(jpg|jpeg|png|webp|avif)$'
                  AND length(image_reference) <= {TmdbContentPolicy.PosterPrefix.Length + 200}
                ON CONFLICT DO NOTHING
            ), removed AS (
                INSERT INTO tmdb_lifecycle_states(mapping_id, failures, content_removed)
                SELECT id, 0, true FROM candidates WHERE purge
                ON CONFLICT(mapping_id) DO UPDATE SET content_removed = true
            ), updated AS (
                UPDATE media_items m SET
                    title = CASE WHEN c.purge THEN 'Unavailable title' ELSE m.title END,
                    description = CASE WHEN c.purge THEN NULL ELSE m.description END,
                    release_date = CASE WHEN c.purge THEN NULL ELSE m.release_date END,
                    metadata = CASE WHEN c.purge THEN jsonb_build_object() ELSE m.metadata END,
                    image_reference = NULL, updated_at = {now}, revision = revision + 1
                FROM candidates c WHERE c.item = m.id RETURNING m.id
            )
            SELECT count(*)::int AS "Value" FROM updated
            """).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return changed.Single();
    }

    public async Task MaintainAsync(string expectedDatabase, string? identity, bool terminate, IEnumerable<string> posters, CancellationToken ct)
    {
        var filenames = posters.ToArray();
        if (string.IsNullOrWhiteSpace(expectedDatabase) || identity != null && (!TmdbContentPolicy.ValidIdentity(identity) || terminate) ||
            filenames.Any(p => !TmdbContentPolicy.ValidPoster(p)) || filenames.Length != 0 && identity == null)
            throw new ArgumentException("Invalid TMDB maintenance request.");
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var actual = await db.Database.SqlQuery<string>($"SELECT current_database() AS \"Value\"").SingleAsync(ct);
            if (!string.Equals(expectedDatabase, actual, StringComparison.Ordinal)) throw new InvalidOperationException("Database name mismatch.");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({MaintenanceLock})", ct);
            try
            {
                var current = policy.Read();
                var blocked = current.BlockedIdentities.ToHashSet();
                if (identity != null) blocked.Add(identity);
                // Replaying a different snapshot can reveal additional filenames for an already blocked identity.
                if (!current.Terminated && !terminate && blocked.Count != 0)
                {
                    var keys = blocked.ToArray();
                    var known = await db.Database.SqlQuery<string>($"""
                        SELECT DISTINCT filename AS "Value" FROM (
                            SELECT h.filename FROM tmdb_poster_identities h JOIN media_external_ids x ON x.id=h.mapping_id
                            WHERE x.provider='tmdb' AND x.namespace || ':' || x.external_id = ANY({keys})
                            UNION
                            SELECT substring(m.image_reference FROM {TmdbContentPolicy.PosterPrefix.Length + 1})
                            FROM media_items m JOIN media_external_ids x ON m.id=x.media_item_id
                            WHERE x.provider='tmdb' AND x.namespace || ':' || x.external_id = ANY({keys})
                              AND starts_with(m.image_reference, {TmdbContentPolicy.PosterPrefix})
                        ) sources LIMIT 10001
                        """).ToArrayAsync(ct);
                    filenames = filenames.Concat(known.Where(TmdbContentPolicy.ValidPoster)).ToArray();
                }
                policy.Block(current, identity, terminate, filenames);
                await ReconcileAsync(ct);
            }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({MaintenanceLock})", CancellationToken.None); }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private sealed record Candidate(Guid Id, string Namespace, string ExternalId, DateTimeOffset? LastSyncedAt, long Revision);
    public async Task RefreshAsync(CancellationToken ct)
    {
        var adapter = providers.Last(p => p.Capability.Name == "tmdb");
        if (!adapter.Capability.Enabled || policy.Read().Terminated) return;
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({RefreshLock}) AS \"Value\"").SingleAsync(ct)) return;
            try { await RefreshBatchAsync(adapter, ct); }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({RefreshLock})", CancellationToken.None); }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private async Task RefreshBatchAsync(IMediaProvider adapter, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var blocked = policy.Read().BlockedIdentities.ToArray();
        var candidates = await db.Database.SqlQuery<Candidate>($"""
            SELECT x.id AS "Id", x.namespace AS "Namespace", x.external_id AS "ExternalId",
                   x.last_synced_at AS "LastSyncedAt", m.revision AS "Revision"
            FROM media_external_ids x JOIN media_items m ON m.id = x.media_item_id
            LEFT JOIN tmdb_lifecycle_states s ON s.mapping_id = x.id
            CROSS JOIN LATERAL (SELECT COALESCE(x.last_synced_at, m.created_at) AS synced) t
            CROSS JOIN LATERAL (SELECT CASE WHEN NOT isfinite(t.synced) OR t.synced > {now} THEN '-infinity'::timestamptz
                ELSE ((t.synced AT TIME ZONE 'UTC') + interval '5 months') AT TIME ZONE 'UTC' END AS due) d
            WHERE x.provider = 'tmdb' AND x.namespace || ':' || x.external_id <> ALL({blocked})
              AND (x.last_synced_at IS NULL OR NOT isfinite(x.last_synced_at)
                   OR x.last_synced_at >= '0001-01-01 00:00:00Z'::timestamptz
                      AND x.last_synced_at < '10000-01-01 00:00:00Z'::timestamptz)
              AND d.due <= {now} AND (s.next_attempt_at IS NULL OR s.next_attempt_at <= {now})
            ORDER BY COALESCE(s.next_attempt_at, d.due), x.id LIMIT {BatchSize}
            """).ToListAsync(ct);
        if (candidates.Count == 0) return;
        // The session lock excludes overlapping batches; the persisted reservation also excludes sequential replicas in this minute.
        var reserved = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE tmdb_refresh_schedule SET last_batch_at = {now}
            WHERE id = 1 AND (last_batch_at IS NULL OR last_batch_at <= {now.AddMinutes(-1)} OR last_batch_at > {now})
              AND (next_allowed_at IS NULL OR next_allowed_at <= {now})
            """, ct);
        if (reserved == 0) return;
        foreach (var candidate in candidates)
        {
            var before = policy.Read();
            if (before.Terminated) break;
            if (before.Blocks(candidate.Namespace, candidate.ExternalId)) continue;
            ExternalMediaResult? result = null;
            MediaProviderException? failure = null;
            var type = candidate.Namespace == "movie" ? MediaTypes.Movie : MediaTypes.TvShow;
            try
            {
                if (!TmdbContentPolicy.ValidIdentity(candidate.Namespace + ":" + candidate.ExternalId)) throw new MediaProviderException(ProviderFailure.InvalidResponse);
                result = await adapter.GetDetailsAsync(candidate.ExternalId, type, ct);
                if (result != null && (result.Provider != "tmdb" || result.Type != type || result.ExternalId != candidate.ExternalId))
                    throw new MediaProviderException(ProviderFailure.InvalidResponse);
            }
            catch (MediaProviderException e) { failure = e; }
            var retryAfter = failure?.RetryAfterSeconds;
            if (failure?.Failure == ProviderFailure.RateLimited) retryAfter ??= 60;
            if (retryAfter is int seconds)
            {
                var nextAllowedAt = clock.GetUtcNow().AddSeconds(Math.Clamp(seconds, 1, 86400));
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE tmdb_refresh_schedule SET next_allowed_at = {nextAllowedAt} WHERE id = 1
                    """, ct);
            }
            await ApplyAsync(candidate, result, failure, ct);
            if (retryAfter.HasValue) break;
        }
    }

    private async Task ApplyAsync(Candidate candidate, ExternalMediaResult? result, MediaProviderException? failure, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireWriterAsync(db, ct);
        var current = policy.Read();
        if (current.Blocks(candidate.Namespace, candidate.ExternalId)) return;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT x.id FROM media_external_ids x JOIN media_items m ON m.id = x.media_item_id
            WHERE x.id = {candidate.Id} FOR UPDATE OF x, m
            """, ct);
        var mapping = await db.MediaExternalIds.FindAsync([candidate.Id], ct);
        if (mapping == null || mapping.LastSyncedAt != candidate.LastSyncedAt) return;
        var item = await db.MediaItems.SingleAsync(m => m.Id == mapping.MediaItemId, ct);
        if (item.Revision != candidate.Revision) return;
        var state = await db.Set<TmdbLifecycleState>().FindAsync([candidate.Id], ct);
        if (state == null) { state = new() { MappingId = candidate.Id }; db.Add(state); }
        var now = clock.GetUtcNow();
        state.LastAttemptAt = now;
        if (failure == null)
        {
            var filtered = current.Filter(result);
            if (filtered == null)
            {
                if (!state.ContentRemoved)
                {
                    await RememberPosterAsync(db, mapping.Id, item.ImageReference, ct);
                    item.RefreshProviderFields("Unavailable title", null, null, null, "{}", now);
                }
                state.ContentRemoved = true;
                state.NextAttemptAt = now.AddDays(1);
                state.LastOutcome = "removed";
            }
            else
            {
                await RememberPosterAsync(db, mapping.Id, item.ImageReference, ct);
                item.RefreshProviderFields(filtered.Title, filtered.Description, filtered.ReleaseDate, filtered.ImageReference, filtered.Metadata.GetRawText(), now);
                await RememberPosterAsync(db, mapping.Id, result!.ImageReference, ct);
                mapping.MarkSynchronized(now);
                state.ContentRemoved = false;
                state.NextAttemptAt = null;
                state.LastOutcome = "synchronized";
            }
            state.Failures = 0;
        }
        else
        {
            state.Failures = Math.Min(state.Failures + 1, 12);
            var seconds = Math.Max(60 * (1 << (state.Failures - 1)), failure.RetryAfterSeconds ?? 0);
            state.NextAttemptAt = now.AddSeconds(Math.Min(seconds, 86400));
            state.LastOutcome = failure.Failure.ToString();
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}
