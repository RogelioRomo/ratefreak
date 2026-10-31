using System.Text.Json.Serialization;
using RateFreak.Domain.PersonalMedia;

namespace RateFreak.Application.Identity;

public sealed record PublicProfile(Guid Id, string Handle, string DisplayName, string? Bio, DateTimeOffset CreatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateProfileRequest
{
    private string? displayName;
    private string? bio;
    private bool hasDisplayName;
    private bool hasBio;

    public string? DisplayName
    {
        get => displayName;
        init { displayName = value; hasDisplayName = true; }
    }
    public string? Bio
    {
        get => bio;
        init { bio = value; hasBio = true; }
    }

    // Presence belongs to binding, not the JSON contract; unknown flag properties remain rejected.
    public ProfileChanges GetChanges() => new(hasDisplayName, displayName?.Trim(), hasBio,
        string.IsNullOrWhiteSpace(bio) ? null : bio.Trim());
}

public sealed record ProfileChanges(bool HasDisplayName, string? DisplayName, bool HasBio, string? Bio);
public sealed record ProfileUpdateResult(AccountProfile? Profile = null, Dictionary<string, string[]>? Errors = null);

public interface IUserProfileService
{
    Task<PublicProfile?> FindAsync(Guid id, CancellationToken ct);
    Task<PublicProfile?> FindByHandleAsync(string handle, CancellationToken ct);
    Task<ProfileUpdateResult> UpdateAsync(Guid actorId, UpdateProfileRequest request, CancellationToken ct);
}

public static class UserProfileRules
{
    public static bool ValidHandle(string handle) => handle.Length is >= 3 and <= 30 &&
        handle.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    public static Dictionary<string, string[]> Validate(ProfileChanges changes)
    {
        var errors = new Dictionary<string, string[]>();
        if (!changes.HasDisplayName && !changes.HasBio) errors["request"] = ["Supply displayName or bio."];
        if (changes.HasDisplayName && (changes.DisplayName is not { Length: >= 1 and <= 80 } ||
            !PersonalMediaRules.ValidBody(changes.DisplayName) || changes.DisplayName.Any(char.IsControl)))
            errors["displayName"] = ["Display name must contain 1–80 valid characters without controls."];
        if (changes.HasBio && changes.Bio is not null && (changes.Bio.Length > 500 || !PersonalMediaRules.ValidBody(changes.Bio)))
            errors["bio"] = ["Bio must contain at most 500 valid plain-text characters; only tab and newline controls are allowed."];
        return errors;
    }
}
