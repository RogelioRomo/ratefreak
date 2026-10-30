using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class ReviewCommentConfiguration
{
    public static void ConfigureReviewComments(this ModelBuilder model)
    {
        model.Entity<ReviewComment>(comment =>
        {
            comment.ToTable("review_comments", table => table.HasCheckConstraint("ck_review_comments_body",
                "length(btrim(body, ' ' || chr(9) || chr(10) || chr(13))) > 0 AND length(body) <= 2000"));
            comment.HasKey(x => x.Id);
            comment.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            comment.HasIndex(x => new { x.ReviewId, x.CreatedAt, x.Id });
            comment.HasOne<Review>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
            comment.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
