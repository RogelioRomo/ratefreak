using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Moderation;

internal static class ModerationLock
{
    // One transaction-scoped lock; always acquire before any account/content row lock.
    public static Task AcquireAsync(RateFreakDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734188290134)", ct);
}
