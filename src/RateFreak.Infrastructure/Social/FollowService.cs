using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.Social;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Social;

internal sealed class FollowService(RateFreakDbContext db) : IFollowService
{
    public async Task<FollowPage?> ListAsync(Guid userId, bool followers, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) return null;
        var edges = db.UserFollows.AsNoTracking().Where(x => followers ? x.FolloweeId == userId : x.FollowerId == userId);
        var count = await edges.CountAsync(ct);
        var query = from edge in edges
                    join user in db.Users.AsNoTracking() on (followers ? edge.FollowerId : edge.FolloweeId) equals user.Id
                    orderby edge.CreatedAt, user.Id
                    select new FollowUser(user.Id, user.UserName!, user.DisplayName, edge.CreatedAt);
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), count, page, pageSize);
    }

    public async Task<FollowStatus> SetAsync(Guid actorId, Guid targetId, bool following, CancellationToken ct)
    {
        if (actorId == targetId) return FollowStatus.Self;
        if (!await db.Users.AnyAsync(x => x.Id == targetId, ct)) return FollowStatus.Missing;
        if (!following)
        {
            await db.UserFollows.Where(x => x.FollowerId == actorId && x.FolloweeId == targetId).ExecuteDeleteAsync(ct);
            return FollowStatus.Success;
        }
        try
        {
            // Parameterized insert preserves the original timestamp on concurrent/replayed PUTs.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO user_follows (follower_id, followee_id, created_at)
                VALUES ({actorId}, {targetId}, {DateTimeOffset.UtcNow})
                ON CONFLICT (follower_id, followee_id) DO NOTHING
                """, ct);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // Either account can disappear after authentication/existence checks.
            return FollowStatus.Missing;
        }
        return FollowStatus.Success;
    }
}
