using System.Net.Http.Headers;
using System.Net.Mail;

namespace RateFreak.Infrastructure.Providers.MusicBrainz;

public sealed class MusicBrainzOptions
{
    public bool Enabled { get; set; }
    public string? Contact { get; set; }
    public int TimeoutMilliseconds { get; set; } = 6000;
    public string UserAgent => $"RateFreak/0.1 ({Contact})";
    public bool IsValid()
    {
        if (TimeoutMilliseconds is < 1500 or > 15000) return false;
        if (!Enabled && string.IsNullOrEmpty(Contact)) return true;
        if (Contact is not { Length: >= 3 and <= 256 } || Contact.Any(c => !char.IsAscii(c) || char.IsControl(c) || c is '(' or ')' or '\\' or ' ')) return false;
        var url = Uri.TryCreate(Contact, UriKind.Absolute, out var uri) && uri.Scheme == "https" && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
        var email = MailAddress.TryCreate(Contact, out var mail) && mail.Address == Contact && Contact.Contains('@');
        return (url || email) && ProductInfoHeaderValue.TryParse("(" + Contact + ")", out _);
    }
}
