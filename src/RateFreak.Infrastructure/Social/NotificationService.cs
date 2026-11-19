using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Social;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Persistence;
namespace RateFreak.Infrastructure.Social;

internal sealed class NotificationService(RateFreakDbContext db) : INotificationService
{
    internal static IQueryable<Notification> Eligible(RateFreakDbContext db, Guid recipient) =>
        db.Notifications.Where(n => n.RecipientId == recipient && db.Users.Any(u => u.Id == n.ActorId) &&
            (n.Kind == "follow" && db.UserFollows.Any(f => f.FollowerId == n.ActorId && f.FolloweeId == recipient) ||
             n.Kind == "comment" && db.ReviewComments.Any(c => c.Id == n.CommentId && !c.IsHidden &&
                db.Reviews.Any(r => r.Id == c.ReviewId && !r.IsHidden &&
                    db.MediaItems.Where(CatalogAccess.Readable(recipient)).Any(m => m.Id == r.MediaItemId)))));

    internal static IQueryable<NotificationDetails> Project(RateFreakDbContext db, Guid recipient, IQueryable<Notification>? source = null) =>
        from n in (source ?? Eligible(db, recipient)).AsNoTracking()
        join u in db.Users.AsNoTracking() on n.ActorId equals u.Id
        select new NotificationDetails(n.Id, n.Kind, new NotificationActor(u.Id, u.UserName!, u.IsProfileHidden ? u.UserName! : u.DisplayName, u.IsProfileHidden || u.AvatarId == null ? null : "/api/v1/users/" + u.Id + "/avatar/" + u.AvatarId),
            n.CommentId, db.ReviewComments.Where(c => c.Id == n.CommentId).Select(c => (Guid?)c.ReviewId).FirstOrDefault(),
            (from c in db.ReviewComments join r in db.Reviews on c.ReviewId equals r.Id where c.Id == n.CommentId select (Guid?)r.MediaItemId).FirstOrDefault(),
            n.CreatedAt, n.ReadAt);

    public async Task<NotificationPage> ListAsync(Guid recipient, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var eligible = Eligible(db, recipient);
        var total = await eligible.CountAsync(ct); var unread = await eligible.CountAsync(n => n.ReadAt == null, ct);
        var items = await Project(db, recipient, eligible.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(items, page, pageSize, total, unread);
    }
    public Task<int> UnreadCountAsync(Guid recipient, CancellationToken ct) => Eligible(db, recipient).CountAsync(n => n.ReadAt == null, ct);
    public async Task<bool> SetReadAsync(Guid recipient, Guid id, bool read, CancellationToken ct) =>
        await Eligible(db, recipient).Where(n => n.Id == id).ExecuteUpdateAsync(s =>
            s.SetProperty(n => n.ReadAt, n => read ? n.ReadAt ?? DateTimeOffset.UtcNow : (DateTimeOffset?)null), ct) != 0;
    public async Task<bool> DeleteAsync(Guid recipient, Guid id, CancellationToken ct) =>
        await Eligible(db, recipient).Where(n => n.Id == id).ExecuteDeleteAsync(ct) != 0;
    public async Task ReadAllAsync(Guid recipient, CancellationToken ct) =>
        await Eligible(db, recipient).Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
    public async Task ClearAsync(Guid recipient, CancellationToken ct) =>
        await db.Notifications.Where(n => n.RecipientId == recipient).ExecuteDeleteAsync(ct);
}
