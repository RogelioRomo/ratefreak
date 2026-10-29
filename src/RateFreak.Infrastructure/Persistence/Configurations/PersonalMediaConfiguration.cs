using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class PersonalMediaConfiguration
{
    public static void ConfigurePersonalMedia(this ModelBuilder model)
    {
        model.Entity<LibraryEntry>(entry =>
        {
            entry.ToTable("user_media", table =>
            {
                table.HasCheckConstraint("ck_user_media_status", "status IN ('planned', 'in_progress', 'completed', 'paused', 'dropped')");
                table.HasCheckConstraint("ck_user_media_dates", "completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at");
            });
            entry.HasKey(x => x.Id);
            entry.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entry.HasIndex(x => new { x.UserId, x.MediaItemId }).IsUnique().HasDatabaseName("ux_user_media_pair");
            entry.HasIndex(x => new { x.UserId, x.CreatedAt, x.MediaItemId });
            entry.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entry.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Rating>(rating =>
        {
            rating.ToTable("ratings", table => table.HasCheckConstraint("ck_ratings_score", "score >= 0 AND score <= 5 AND mod(score, 0.5) = 0"));
            rating.HasKey(x => x.Id);
            // Unrestricted numeric avoids rounding an invalid quarter-step before CHECK evaluates.
            rating.Property(x => x.Score).HasColumnType("numeric");
            rating.HasIndex(x => new { x.UserId, x.MediaItemId }).IsUnique().HasDatabaseName("ux_ratings_pair");
            rating.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            rating.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Review>(review =>
        {
            review.ToTable("reviews", table => table.HasCheckConstraint("ck_reviews_body", "length(btrim(body)) > 0 AND length(body) <= 10000"));
            review.HasKey(x => x.Id);
            review.Property(x => x.Body).HasMaxLength(10000).IsRequired();
            review.HasIndex(x => new { x.UserId, x.MediaItemId }).IsUnique().HasDatabaseName("ux_reviews_pair");
            review.HasIndex(x => new { x.MediaItemId, x.CreatedAt, x.Id });
            review.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            review.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
