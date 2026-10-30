using System.ComponentModel.DataAnnotations;

namespace RateFreak.Application.Identity;

public static class AccountInput
{
    public static Dictionary<string, string[]> Validate(RegisterRequest request)
    {
        var errors = Validate(new LoginRequest(request.Email, request.Password));
        foreach (var error in ValidateProfile(request.Handle, request.DisplayName)) errors[error.Key] = error.Value;
        if (request.Password is { Length: < 12 })
            errors["password"] = ["Password must be 12–128 characters with uppercase, lowercase and a digit."];
        return errors;
    }

    public static Dictionary<string, string[]> ValidateProfile(string? rawHandle, string? displayName)
    {
        var errors = new Dictionary<string, string[]>();
        var handle = rawHandle?.Trim();
        if (handle is null || handle.Length is < 3 or > 30 ||
            handle.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            errors["handle"] = ["Handle must be 3–30 ASCII letters, digits or underscores."];
        var name = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            errors["displayName"] = ["Display name must be 1–80 characters without control characters."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var email = request.Email?.Trim();
        if (!ValidEmail(email))
            errors["email"] = ["A valid email address up to 254 characters is required."];
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            errors["password"] = ["A password up to 128 characters is required."];
        return errors;
    }

    public static bool ValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            return false;
        try
        {
            // Match Identity's default invariant normalizer before writing its bounded index column.
            return email.Normalize().ToUpperInvariant().Length <= 254;
        }
        catch (ArgumentException) { return false; }
    }
}
