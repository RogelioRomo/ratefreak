using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class SocialConfiguration
{
    public static void ConfigureSocial(this ModelBuilder model)
    {
        model.Entity<UserFollow>(follow =>
        {
            follow.ToTable("user_follows", table =>
                table.HasCheckConstraint("ck_user_follows_no_self", "follower_id <> followee_id"));
            follow.HasKey(x => new { x.FollowerId, x.FolloweeId });
            follow.HasIndex(x => new { x.FollowerId, x.CreatedAt, x.FolloweeId });
            follow.HasIndex(x => new { x.FolloweeId, x.CreatedAt, x.FollowerId });
            follow.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Cascade);
            follow.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
