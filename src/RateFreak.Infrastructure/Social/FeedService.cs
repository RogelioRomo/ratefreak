using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Social;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Social;

internal sealed class FeedService(RateFreakDbContext db) : IFeedService
{
    public async Task<FeedReadResult> ListAsync(Guid viewerId, FeedQuery query, CancellationToken ct)
    {
        if (!FeedCursor.TryRead(query.Cursor, viewerId, query, out var cursor))
            return new(null, new() { ["cursor"] = ["Invalid feed cursor. Refresh the feed to start again."] });
        var following = query.Tab == "following";
        var time = cursor?.Time ?? DateTimeOffset.MaxValue;
        var author = cursor?.Author ?? Guid.Empty;
        var mediaId = cursor?.Media ?? Guid.Empty;
        var seek = cursor is not null;
        // One SQL snapshot: public eligibility and current components are evaluated together.
        // Full join keeps review-only and rating-only pairs without multiplying entries.
        var rows = await db.Database.SqlQuery<FeedRow>($"""
            WITH opinions AS (
                SELECT COALESCE(r.user_id, t.user_id) AS author_id,
                       COALESCE(r.media_item_id, t.media_item_id) AS media_id,
                       r.id AS review_id, left(r.body, 280) AS excerpt, r.is_spoiler,
                       t.score, GREATEST(r.created_at, t.shared_at) AS published_at
                FROM (SELECT id, user_id, media_item_id, body, is_spoiler, created_at FROM reviews WHERE NOT is_hidden) r
                FULL JOIN (SELECT user_id, media_item_id, score, shared_at FROM ratings WHERE is_shared) t
                    ON r.user_id = t.user_id AND r.media_item_id = t.media_item_id
            )
            SELECT u.id AS "AuthorId", u.user_name AS "Handle",
                   CASE WHEN u.is_profile_hidden THEN u.user_name ELSE u.display_name END AS "DisplayName",
                   m.id AS "MediaId", m.media_type AS "Type", m.title AS "Title", m.release_date AS "ReleaseDate",
                   m.image_reference AS "ImageReference", o.score AS "Score", o.review_id AS "ReviewId",
                   o.excerpt AS "Excerpt", o.is_spoiler AS "IsSpoiler", o.published_at AS "PublishedAt"
            FROM opinions o
            JOIN users u ON u.id = o.author_id
            JOIN media_items m ON m.id = o.media_id
            JOIN catalog_categories c ON c.id = m.category_id
            WHERE NOT m.is_archived AND NOT m.is_hidden AND m.visibility = 'public'
                AND c.scope = 'public' AND NOT c.is_hidden
                AND ({query.Type}::text IS NULL OR m.media_type = {query.Type})
                AND (NOT {following} OR EXISTS (SELECT 1 FROM user_follows f WHERE f.follower_id = {viewerId} AND f.followee_id = o.author_id))
                AND (NOT {seek} OR o.published_at < {time}
                    OR (o.published_at = {time} AND (o.author_id > {author} OR (o.author_id = {author} AND o.media_id > {mediaId}))))
            ORDER BY o.published_at DESC, o.author_id, o.media_id
            LIMIT 21
            """).ToListAsync(ct);
        var items = rows.Take(20).Select(x => new FeedEntry(new(x.AuthorId, x.Handle, x.DisplayName),
            new(x.MediaId, x.Type, x.Title, x.ReleaseDate, x.ImageReference), x.Score,
            x.ReviewId is {} id ? new(id, x.Excerpt!, x.IsSpoiler == true) : null, x.PublishedAt)).ToList();
        var next = rows.Count > 20 && items.LastOrDefault() is {} last
            ? new FeedCursor(1, viewerId, query.Tab, query.Type, last.PublishedAt, last.Author.Id, last.Media.Id).Encode() : null;
        var hasFollowees = await db.UserFollows.AnyAsync(x => x.FollowerId == viewerId, ct);
        return new(new(items, next, hasFollowees));
    }
}

internal sealed class FeedRow
{
    public Guid AuthorId { get; set; }
    public string Handle { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public Guid MediaId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly? ReleaseDate { get; set; }
    public string? ImageReference { get; set; }
    public decimal? Score { get; set; }
    public Guid? ReviewId { get; set; }
    public string? Excerpt { get; set; }
    public bool? IsSpoiler { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}
