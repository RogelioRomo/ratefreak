using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class ArtworkConfiguration
{
    public static void ConfigureArtwork(this ModelBuilder model) => model.Entity<ArtworkAsset>(asset =>
    {
        asset.ToTable("artwork_assets", table => table.HasCheckConstraint("ck_artwork_dimensions", "width BETWEEN 1 AND 2048 AND height BETWEEN 1 AND 2048 AND byte_length > 0 AND content_type = 'image/webp'"));
        asset.HasKey(a => a.ItemId); asset.HasIndex(a => a.AssetId).IsUnique();
        asset.Property(a => a.StorageKey).HasMaxLength(64).IsRequired(); asset.Property(a => a.ContentType).HasMaxLength(32).IsRequired();
        asset.HasOne<MediaItem>().WithOne().HasForeignKey<ArtworkAsset>(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
    });
}
