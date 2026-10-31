using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Moderation;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Domain.Social;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class ModerationConfiguration
{
    public static void ConfigureModeration(this ModelBuilder model)
    {
        model.Entity<Review>().Property(x => x.IsHidden).HasDefaultValue(false);
        model.Entity<ReviewComment>().Property(x => x.IsHidden).HasDefaultValue(false);
        model.Entity<ContentReport>(r =>
        {
            r.ToTable("content_reports", t =>
            {
                t.HasCheckConstraint("ck_report_target", "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'review' AND comment_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)))");
                t.HasCheckConstraint("ck_report_reason", "reason IN ('spam','harassment','hate','sexual_content','violence','spoiler','other')");
                t.HasCheckConstraint("ck_report_status", "(status = 'open' AND resolved_at IS NULL AND decision IS NULL AND resolution_reason IS NULL AND resolved_by IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL AND decision IS NOT NULL AND decision IN ('hide','dismiss') AND resolution_reason IS NOT NULL AND resolved_by IS NOT NULL)");
                t.HasCheckConstraint("ck_report_details", "(details IS NULL OR length(details) <= 1000) AND (reason <> 'other' OR (details IS NOT NULL AND length(btrim(details)) > 0))");
            });
            r.HasKey(x => x.Id);
            r.Property(x => x.TargetType).HasMaxLength(16); r.Property(x => x.Reason).HasMaxLength(32); r.Property(x => x.Status).HasMaxLength(16);
            r.Property(x => x.Details).HasMaxLength(1000); r.Property(x => x.ResolutionReason).HasMaxLength(1000); r.Property(x => x.Decision).HasMaxLength(16);
            r.HasIndex(x => new { x.OriginalReporterId, x.TargetType, x.TargetId }).IsUnique();
            r.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
            r.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.SetNull);
            r.HasOne<Review>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.SetNull);
            r.HasOne<ReviewComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.SetNull);
        });
        model.Entity<ModerationEvent>(e =>
        {
            e.ToTable("moderation_events", t => t.HasCheckConstraint("ck_moderation_reason", "length(btrim(reason)) > 0 AND length(reason) <= 1000"));
            e.HasKey(x => x.Id); e.Property(x => x.Reason).HasMaxLength(1000); e.Property(x => x.ActorKind).HasMaxLength(16);
            e.Property(x => x.Action).HasMaxLength(32); e.Property(x => x.TargetType).HasMaxLength(16);
            e.HasIndex(x => new { x.CreatedAt, x.Id }); e.HasIndex(x => new { x.TargetType, x.TargetId, x.CreatedAt, x.Id });
        });
    }
}
