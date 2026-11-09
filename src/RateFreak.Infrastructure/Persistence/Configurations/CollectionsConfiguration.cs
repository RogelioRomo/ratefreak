using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.Collections;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class CollectionsConfiguration
{
    public static void ConfigureCollections(this ModelBuilder model)
    {
        model.Entity<Collection>(entity =>
        {
            entity.ToTable("collections", table =>
            {
                table.HasCheckConstraint("ck_collections_visibility", "visibility IN ('private', 'public')");
                table.HasCheckConstraint("ck_collections_versions", "revision > 0");
                table.HasCheckConstraint("ck_collections_name", "length(btrim(name)) > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Visibility).HasMaxLength(10).IsRequired();
            entity.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OwnerUserId, x.CreatedAt, x.Id });
            entity.HasIndex(x => new { x.Visibility, x.CreatedAt, x.Id });
        });
        model.Entity<CollectionItem>(entity =>
        {
            entity.ToTable("collection_items", table =>
            {
                table.HasCheckConstraint("ck_collection_items_position", "position >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CollectionId, x.MediaItemId }).IsUnique().HasDatabaseName("ux_collection_items_media");
            entity.HasIndex(x => new { x.CollectionId, x.Position, x.Id });
        });
    }
}
