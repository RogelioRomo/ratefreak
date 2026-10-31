using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RateFreak.IntegrationTests;

public sealed class ExternalSignInMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_preserves_users_and_logins_and_refuses_existing_duplicate_provider_pairs(bool duplicates)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync(ct);
        await using var db = new RateFreakDbContext(new DbContextOptionsBuilder<RateFreakDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261030213246_AddReviewComments";
        await migrator.MigrateAsync(previous, ct);
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = "preserved", NormalizedUserName = "PRESERVED",
            Email = "preserved@example.test", NormalizedEmail = "PRESERVED@EXAMPLE.TEST", DisplayName = "Preserved",
            PasswordHash = "preserved-password-hash", SecurityStamp = "preserved-stamp" };
        // Seed only columns present at the historical migration; the current model also has Bio.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email,
                display_name, password_hash, security_stamp, concurrency_stamp, created_at, updated_at,
                email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES ({user.Id}, {user.UserName}, {user.NormalizedUserName}, {user.Email}, {user.NormalizedEmail},
                {user.DisplayName}, {user.PasswordHash}, {user.SecurityStamp}, {user.ConcurrencyStamp}, {user.CreatedAt}, {user.UpdatedAt},
                false, false, false, false, 0)
            """, ct);
        db.Add(new IdentityUserLogin<Guid> { UserId = user.Id, LoginProvider = "github", ProviderKey = "1" });
        if (duplicates) db.Add(new IdentityUserLogin<Guid> { UserId = user.Id, LoginProvider = "github", ProviderKey = "2" });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        if (duplicates)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync(ct));
            Assert.Equal(2, await db.UserLogins.CountAsync(ct));
            Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(ct), m => m.EndsWith("_AddExternalSignIn", StringComparison.Ordinal));
        }
        else
        {
            await db.Database.MigrateAsync(ct);
            await db.Database.MigrateAsync(ct);
            db.Add(new ExternalAuthenticationChallenge { Id = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(previous, ct);
            Assert.Single(await db.UserLogins.ToListAsync(ct));
            await db.Database.MigrateAsync(ct);
            Assert.Empty(await db.Set<ExternalAuthenticationChallenge>().ToListAsync(ct));
            Assert.False(db.Database.HasPendingModelChanges());
        }
        var preserved = await db.Users.AsNoTracking()
            .Select(account => new { account.PasswordHash, account.SecurityStamp, account.EmailConfirmed }).SingleAsync(ct);
        Assert.Equal("preserved-password-hash", preserved.PasswordHash);
        Assert.Equal("preserved-stamp", preserved.SecurityStamp);
        Assert.False(preserved.EmailConfirmed);
    }
}
