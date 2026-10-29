using Npgsql;

namespace RateFreak.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public string Default { get; set; } = string.Empty;

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Default)) return false;
        try
        {
            var parsed = new NpgsqlConnectionStringBuilder(Default);
            return !string.IsNullOrWhiteSpace(parsed.Host)
                && !string.IsNullOrWhiteSpace(parsed.Database)
                && !string.IsNullOrWhiteSpace(parsed.Username);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
