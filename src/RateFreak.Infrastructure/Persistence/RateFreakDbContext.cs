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
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryAlias> CategoryAliases => Set<CategoryAlias>();
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
        modelBuilder.ConfigureArtwork();
        modelBuilder.ConfigureCategories();
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
        modelBuilder.Entity<RateFreak.Infrastructure.Installation.InstallationState>(state =>
        {
            state.ToTable("installation_state", table => table.HasCheckConstraint("ck_installation_singleton", "id = 1"));
            state.HasKey(s => s.Id);
            state.Property(s => s.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<AccountRecoveryState>(state =>
        {
            state.ToTable("account_recovery_states");
            state.HasKey(s => s.UserId);
            state.Property(s => s.Verifier).HasMaxLength(64);
            state.HasOne<AccountUser>().WithOne().HasForeignKey<AccountRecoveryState>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccountRecoveryEvent>(entry =>
        {
            entry.ToTable("account_recovery_events");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Action).HasMaxLength(32).IsRequired();
            entry.HasIndex(e => new { e.UserId, e.CreatedAt });
            entry.HasOne<AccountUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccountUser>(user =>
        {
            user.ToTable("users", table => table.HasCheckConstraint("ck_users_bio",
                "bio IS NULL OR (length(btrim(bio, ' ' || chr(9) || chr(10) || chr(13))) > 0 AND length(bio) <= 500)"));
            // Prevent stale full-row Identity updates from reverting separately committed profile edits.
            user.Property(u => u.Bio).HasMaxLength(500).IsConcurrencyToken();
            user.Property(u => u.UpdatedAt).IsConcurrencyToken();
            user.Property(u => u.UserName).HasMaxLength(30).IsRequired();
            user.Property(u => u.NormalizedUserName).HasMaxLength(30).IsRequired();
            user.Property(u => u.Email).HasMaxLength(254).IsRequired();
            user.Property(u => u.NormalizedEmail).HasMaxLength(254).IsRequired();
            user.Property(u => u.DisplayName).HasMaxLength(80).IsRequired().IsConcurrencyToken();
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
