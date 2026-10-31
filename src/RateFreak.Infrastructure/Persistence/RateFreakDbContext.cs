using RateFreak.Domain.Catalog;
using RateFreak.Domain.Collections;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Authentication;

namespace RateFreak.Infrastructure.Persistence;

public sealed class RateFreakDbContext(DbContextOptions<RateFreakDbContext> options)
    : IdentityDbContext<AccountUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RateFreak.Domain.Social.UserFollow> UserFollows => Set<RateFreak.Domain.Social.UserFollow>();

    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionField> CollectionFields => Set<CollectionField>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<MediaExternalId> MediaExternalIds => Set<MediaExternalId>();

    public DbSet<LibraryEntry> LibraryEntries => Set<LibraryEntry>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RateFreak.Domain.Social.ReviewComment> ReviewComments => Set<RateFreak.Domain.Social.ReviewComment>();

    public DbSet<RateFreak.Domain.Moderation.ContentReport> ContentReports => Set<RateFreak.Domain.Moderation.ContentReport>();
    public DbSet<RateFreak.Domain.Moderation.ModerationEvent> ModerationEvents => Set<RateFreak.Domain.Moderation.ModerationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureCatalog();
        modelBuilder.ConfigureModeration();
        modelBuilder.ConfigureSocial();
        modelBuilder.ConfigureReviewComments();
        modelBuilder.ConfigureCollections();
        modelBuilder.ConfigurePersonalMedia();
        modelBuilder.Entity<ExternalAuthenticationChallenge>(challenge =>
        {
            challenge.ToTable("external_authentication_challenges");
            challenge.HasKey(c => c.Id);
            challenge.HasIndex(c => c.ExpiresAt);
        });
        modelBuilder.Entity<AccountUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.UserName).HasMaxLength(30).IsRequired();
            user.Property(u => u.NormalizedUserName).HasMaxLength(30).IsRequired();
            user.Property(u => u.Email).HasMaxLength(254).IsRequired();
            user.Property(u => u.NormalizedEmail).HasMaxLength(254).IsRequired();
            user.Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_handle").IsUnique();
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ux_users_normalized_email").IsUnique();
        });
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>(login =>
        {
            login.ToTable("user_logins");
            login.HasIndex(l => new { l.UserId, l.LoginProvider }).IsUnique()
                .HasDatabaseName("ux_user_logins_user_provider");
        });
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(string.Concat(property.Name.Select((c, i) =>
                    char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
    }
}
