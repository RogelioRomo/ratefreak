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
                table.HasCheckConstraint("ck_collections_versions", "revision > 0 AND schema_version > 0");
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
        model.Entity<CollectionField>(entity =>
        {
            entity.ToTable("collection_fields", table =>
            {
                table.HasCheckConstraint("ck_collection_fields_type", "field_type IN ('text', 'number', 'boolean', 'date', 'select')");
                table.HasCheckConstraint("ck_collection_fields_key", "key ~ '^[a-z][a-z0-9_]{0,39}$' AND key NOT IN ('id','title','media_item_id','created_at','updated_at')");
                table.HasCheckConstraint("ck_collection_fields_label", "length(btrim(label)) > 0");
                table.HasCheckConstraint("ck_collection_fields_position", "position >= 0");
                table.HasCheckConstraint("ck_collection_fields_configuration", "jsonb_typeof(configuration) = 'object'");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FieldType).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Configuration).HasColumnType("jsonb").IsRequired();
            entity.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CollectionId, x.Key }).IsUnique().HasDatabaseName("ux_collection_fields_key");
            entity.HasIndex(x => new { x.CollectionId, x.Position, x.Id });
        });
        model.Entity<CollectionItem>(entity =>
        {
            entity.ToTable("collection_items", table =>
            {
                table.HasCheckConstraint("ck_collection_items_title", "(media_item_id IS NOT NULL AND title IS NULL) OR (media_item_id IS NULL AND title IS NOT NULL AND length(btrim(title)) > 0)");
                table.HasCheckConstraint("ck_collection_items_position", "position >= 0");
                table.HasCheckConstraint("ck_collection_items_data", "jsonb_typeof(data) = 'object'");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
            entity.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MediaItem>().WithMany().HasForeignKey(x => x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CollectionId, x.MediaItemId }).IsUnique().HasFilter("media_item_id IS NOT NULL").HasDatabaseName("ux_collection_items_media");
            entity.HasIndex(x => new { x.CollectionId, x.Position, x.Id });
        });
    }
}
