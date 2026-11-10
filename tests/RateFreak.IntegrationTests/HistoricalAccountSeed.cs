using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

internal static class HistoricalAccountSeed
{
    // Historical migration fixtures must not INSERT columns added by today's model.
    public static Task<int> InsertAsync(RateFreakDbContext db, AccountUser user, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (id, user_name, normalized_user_name, email, normalized_email,
                display_name, created_at, updated_at, email_confirmed, phone_number_confirmed,
                two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES ({user.Id}, {user.UserName}, {user.NormalizedUserName}, {user.Email}, {user.NormalizedEmail},
                {user.DisplayName}, {user.CreatedAt}, {user.UpdatedAt}, false, false, false, false, 0)
            """, ct);
}
