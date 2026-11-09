using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class CatalogConfiguration
{
    public static void ConfigureCatalog(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaItem>(item =>
        {
            item.ToTable("media_items", table =>
            {
                table.HasCheckConstraint("ck_media_items_type", "media_type IN ('movie', 'tv_show', 'album', 'video_game', 'custom')");
                table.HasCheckConstraint("ck_media_items_visibility", "visibility IN ('public', 'private')");
                table.HasCheckConstraint("ck_media_items_provenance", "(provenance = 'system' AND creator_user_id IS NULL AND media_type <> 'custom' AND visibility = 'public') OR (provenance = 'user' AND creator_user_id IS NOT NULL AND media_type = 'custom')");
                table.HasCheckConstraint("ck_media_items_revision", "revision > 0");
                table.HasCheckConstraint("ck_media_items_title", "length(btrim(title)) > 0");
                table.HasCheckConstraint("ck_media_items_metadata", "jsonb_typeof(metadata) = 'object'");
            });
            item.HasKey(x => x.Id);
            item.Property(x => x.Visibility).HasMaxLength(10).IsRequired();
            item.Property(x => x.Provenance).HasMaxLength(10).IsRequired();
            item.HasOne<RateFreak.Infrastructure.Authentication.AccountUser>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
            item.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            item.HasIndex(x => new { x.CreatorUserId, x.Id });
            item.HasIndex(x => new { x.CategoryId, x.Title, x.Id });
            item.Property(x => x.MediaType).HasMaxLength(20).IsRequired();
            item.Property(x => x.Title).HasMaxLength(300).IsRequired();
            item.Property(x => x.Description).HasMaxLength(20000);
            item.Property(x => x.ImageReference).HasMaxLength(2048);
            item.Property(x => x.Metadata).HasColumnType("jsonb").IsRequired();
            item.HasIndex(x => new { x.Title, x.Id }).HasDatabaseName("ix_media_items_title_id");
            item.HasIndex(x => new { x.MediaType, x.Title, x.Id }).HasDatabaseName("ix_media_items_type_title_id");
        });
        modelBuilder.Entity<MediaExternalId>(mapping =>
        {
            mapping.ToTable("media_external_ids", table =>
            {
                table.HasCheckConstraint("ck_media_external_ids_provider", "provider ~ '^[a-z0-9_-]+$'");
                table.HasCheckConstraint("ck_media_external_ids_namespace", "namespace ~ '^[a-z0-9_-]+$'");
                table.HasCheckConstraint("ck_media_external_ids_id", "length(btrim(external_id)) > 0");
            });
            mapping.HasKey(x => x.Id);
            mapping.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            mapping.Property(x => x.Namespace).HasMaxLength(50).IsRequired();
            mapping.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
            mapping.Property(x => x.ExternalUrl).HasMaxLength(2048);
            mapping.HasIndex(x => new { x.Provider, x.Namespace, x.ExternalId }).IsUnique()
                .HasDatabaseName("ux_media_external_ids_identity");
            mapping.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
