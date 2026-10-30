namespace RateFreak.Infrastructure.Authentication;

// Technical OAuth replay state; contains no provider identity, email, token or secret.
public sealed class ExternalAuthenticationChallenge
{
    public Guid Id { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
