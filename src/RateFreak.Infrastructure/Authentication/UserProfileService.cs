using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.Authentication;

internal sealed class UserProfileService(RateFreakDbContext db) : IUserProfileService
{
    private IQueryable<PublicProfile> PublicProfiles(IQueryable<AccountUser> users) => users.AsNoTracking()
        .Select(user => new PublicProfile(user.Id, user.UserName!, user.DisplayName, user.Bio, user.CreatedAt));

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
        var changed = await db.Users.Where(user => user.Id == actorId).ExecuteUpdateAsync(set => set
            .SetProperty(user => user.DisplayName, user => changes.HasDisplayName ? changes.DisplayName! : user.DisplayName)
            .SetProperty(user => user.Bio, user => changes.HasBio ? changes.Bio : user.Bio)
            .SetProperty(user => user.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed == 0) return new();

        // Project while the update holds the row lock; unrelated Identity/security fields are untouched.
        var profile = await db.Users.AsNoTracking().Where(user => user.Id == actorId)
            .Select(user => new AccountProfile(user.Id, user.UserName!, user.DisplayName, user.Email!, user.CreatedAt, user.Bio))
            .SingleAsync(ct);
        await transaction.CommitAsync(ct);
        return new(profile);
    }
}
