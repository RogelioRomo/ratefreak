using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RateFreak.Application.Identity;
using RateFreak.Application.Installation;
using RateFreak.Domain.Moderation;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Moderation;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Installation;

internal sealed class InstallationService(RateFreakDbContext db, LocalAccountRegistration registration,
    IOptions<InstallationOptions> options) : IInstallationService
{
    public Task<InstallationStatus?> StatusAsync(CancellationToken ct)
    {
        var configured = InstallationOptions.ValidSecret(options.Value.SetupSecret);
        return db.Set<InstallationState>().AsNoTracking().Where(s => s.Id == 1)
            .Select(s => new InstallationStatus(s.CompletedAt == null, s.CompletedAt == null && configured)).SingleOrDefaultAsync(ct);
    }

    public async Task<SetupResult> CompleteAsync(SetupRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ModerationLock.AcquireAsync(db, ct);
        var state = await db.Set<InstallationState>().FromSqlRaw("SELECT * FROM installation_state WHERE id = 1 FOR UPDATE").SingleOrDefaultAsync(ct);
        if (state is null) return new(SetupOutcome.Unavailable);
        await db.Entry(state).ReloadAsync(ct);
        if (state.CompletedAt is not null) return new(SetupOutcome.Completed);
        if (!InstallationOptions.ValidSecret(options.Value.SetupSecret)) return new(SetupOutcome.Unavailable);
        if (!InstallationOptions.ValidSecret(request.SetupSecret) || !CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(options.Value.SetupSecret!), Convert.FromHexString(request.SetupSecret!))) return new(SetupOutcome.Invalid);
        // Pending state with existing accounts is inconsistent. Never create/elevate another account.
        if (await db.Users.AnyAsync(ct)) return new(SetupOutcome.Unavailable);
        var created = await registration.CreateAsync(new(request.Email, request.Password, request.Handle, request.DisplayName), ct);
        if (created.Status != AccountStatus.Success) return new(SetupOutcome.Invalid, Errors: created.Errors);
        var id = created.Profile!.Id;
        var role = await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == "ADMIN", ct);
        if (role is null)
        {
            role = new IdentityRole<Guid>("Admin") { Id = Guid.NewGuid(), NormalizedName = "ADMIN" };
            db.Roles.Add(role);
        }
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = role.Id });
        db.ModerationEvents.Add(new ModerationEvent { ActorKind = "operator", Action = "grant_admin", TargetType = "account", TargetId = id,
            Reason = "Initial installation setup authorized by deployment secret.", Before = false, After = true });
        state.CompletedAt = DateTimeOffset.UtcNow; state.InitialAdminId = id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var p = created.Profile;
        return new(SetupOutcome.Success, new(p.Id,p.Handle,p.DisplayName,p.Email,p.CreatedAt,p.Bio,created.RecoveryCode!));
    }
}
