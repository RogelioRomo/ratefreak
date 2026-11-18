using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Authentication;
namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class NotificationConfiguration
{
    public static void ConfigureNotifications(this ModelBuilder model) => model.Entity<Notification>(n =>
    {
        n.ToTable("notifications", t => {
            t.HasCheckConstraint("ck_notifications_no_self", "actor_id <> recipient_id");
            t.HasCheckConstraint("ck_notifications_source", "(kind = 'follow' AND comment_id IS NULL AND follow_actor_id IS NOT NULL AND follow_recipient_id IS NOT NULL AND follow_actor_id = actor_id AND follow_recipient_id = recipient_id) OR (kind = 'comment' AND comment_id IS NOT NULL AND follow_actor_id IS NULL AND follow_recipient_id IS NULL)");
        });
        n.HasKey(x => x.Id); n.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        n.HasIndex(x => new { x.RecipientId, x.CreatedAt, x.Id });
        n.HasIndex(x => new { x.RecipientId, x.CreatedAt }).HasFilter("read_at IS NULL");
        n.HasIndex(x => new { x.ActorId, x.RecipientId }).IsUnique().HasFilter("kind = 'follow'");
        n.HasIndex(x => x.CommentId).IsUnique();
        n.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        n.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Cascade);
        n.HasOne<ReviewComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
        n.HasOne<UserFollow>().WithMany().HasForeignKey(x => new { x.FollowActorId, x.FollowRecipientId }).OnDelete(DeleteBehavior.Cascade);
    });
}
