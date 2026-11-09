using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence.Configurations;

internal static class CategoryConfiguration
{
    public static void ConfigureCategories(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(category =>
        {
            category.ToTable("catalog_categories", table =>
            {
                table.HasCheckConstraint("ck_category_scope", "scope IN ('public', 'private') AND (scope <> 'private' OR owner_user_id IS NOT NULL)");
                table.HasCheckConstraint("ck_category_name", "length(btrim(name)) > 0 AND length(btrim(normalized_name)) > 0");
                table.HasCheckConstraint("ck_category_revision", "revision > 0");
            });
            category.HasKey(x => x.Id);
            category.Property(x => x.Name).HasMaxLength(100).IsRequired();
            category.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
            category.Property(x => x.Scope).HasMaxLength(10).IsRequired();
            category.HasOne<AccountUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            category.HasIndex(x => x.NormalizedName).IsUnique().HasFilter("scope = 'public'").HasDatabaseName("ux_category_public_name");
            category.HasIndex(x => new { x.OwnerUserId, x.NormalizedName }).IsUnique().HasFilter("scope = 'private'").HasDatabaseName("ux_category_private_name");
        });
        modelBuilder.Entity<CategoryAlias>(alias =>
        {
            alias.ToTable("catalog_category_aliases", table => table.HasCheckConstraint("ck_category_alias_distinct", "source_category_id <> target_category_id"));
            alias.HasKey(x => x.SourceCategoryId);
            alias.HasOne<Category>().WithMany().HasForeignKey(x => x.SourceCategoryId).OnDelete(DeleteBehavior.Restrict);
            alias.HasOne<Category>().WithMany().HasForeignKey(x => x.TargetCategoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
