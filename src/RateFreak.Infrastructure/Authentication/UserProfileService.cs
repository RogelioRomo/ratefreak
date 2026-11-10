using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class UserProfileService(RateFreakDbContext db) : IUserProfileService
{
    private IQueryable<PublicProfile> PublicProfiles(IQueryable<AccountUser> users) => users.AsNoTracking()
        .Select(user => new PublicProfile(user.Id, user.UserName!, user.IsProfileHidden ? user.UserName! : user.DisplayName,
            user.IsProfileHidden ? null : user.Bio, user.CreatedAt, user.IsProfileHidden));

    public Task<PublicProfile?> FindAsync(Guid id, CancellationToken ct) =>
        PublicProfiles(db.Users.Where(user => user.Id == id)).SingleOrDefaultAsync(ct);

    public Task<PublicProfile?> FindByHandleAsync(string handle, CancellationToken ct)
    {
        if (!UserProfileRules.ValidHandle(handle)) return Task.FromResult<PublicProfile?>(null);
        var normalized = handle.ToUpperInvariant();
        return PublicProfiles(db.Users.Where(user => user.NormalizedUserName == normalized)).SingleOrDefaultAsync(ct);
    }

    public async Task<ProfileUpdateResult> UpdateAsync(Guid actorId, UpdateProfileRequest request, CancellationToken ct)
    {
        var changes = request.GetChanges();
        var errors = UserProfileRules.Validate(changes);
        if (errors.Count != 0) return new(Errors: errors);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actorId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is null) return new();
        var name = changes.HasDisplayName ? changes.DisplayName! : current.DisplayName;
        var bio = changes.HasBio ? changes.Bio : current.Bio;
        if (name != current.DisplayName || bio != current.Bio)
        {
            var now = DateTimeOffset.UtcNow;
            if (now <= current.ProfileUpdatedAt.AddTicks(10)) now = current.ProfileUpdatedAt.AddTicks(10);
            await db.Users.Where(user => user.Id == actorId).ExecuteUpdateAsync(set => set
                .SetProperty(user => user.DisplayName, name).SetProperty(user => user.Bio, bio)
                .SetProperty(user => user.ProfileUpdatedAt, now).SetProperty(user => user.UpdatedAt, now), ct);
        }

        // Project while the update holds the row lock; unrelated Identity/security fields are untouched.
        var profile = await db.Users.AsNoTracking().Where(user => user.Id == actorId)
            .Select(user => new AccountProfile(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt, user.Bio, user.IsProfileHidden))
            .SingleAsync(ct);
        await transaction.CommitAsync(ct);
        return new(profile);
    }
}
