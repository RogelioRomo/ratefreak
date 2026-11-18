using System.Net.Http.Headers;
using System.Net.Mail;

namespace RateFreak.Infrastructure.Providers.OpenLibrary;

public sealed class OpenLibraryOptions
{
    public bool Enabled { get; set; }
    public string? Contact { get; set; }
    public int TimeoutMilliseconds { get; set; } = 10000;
    public string UserAgent => $"RateFreak/0.1 ({Contact})";
    public bool IsValid()
    {
        if (TimeoutMilliseconds is < 2000 or > 15000) return false;
        if (!Enabled && string.IsNullOrEmpty(Contact)) return true;
        return Contact is { Length: >= 3 and <= 256 } && !Contact.Any(c => !char.IsAscii(c) || char.IsControl(c) || c is '(' or ')' or '\\' or ' ')
            && MailAddress.TryCreate(Contact, out var mail) && mail.Address == Contact && Contact.Contains('@')
            && ProductInfoHeaderValue.TryParse("(" + Contact + ")", out _);
    }
}
