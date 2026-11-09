using System.Text.Json;
using Microsoft.Extensions.Options;
using RateFreak.Application.Identity;

namespace RateFreak.Infrastructure.Authentication;

public sealed class AccountErasureJournal(IOptions<AccountDataOptions> options)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private string PathName()
    {
        var path = options.Value.ErasureJournalPath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw Unavailable();
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)!); dir != null; dir = dir.Parent)
            if (dir.LinkTarget != null) throw new IOException("Erasure journal must not traverse symbolic links.");
        if (!File.Exists(path) || new FileInfo(path).LinkTarget != null || Directory.Exists(path))
            throw Unavailable();
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != (UnixFileMode.UserRead | UnixFileMode.UserWrite))
            throw Unavailable();
        return path;
    }

    private static AccountDataException Unavailable() => new(503,
        "Account deletion is unavailable until the operator provisions a private, durable erasure journal.");

    private FileStream Open()
    {
        // Open never creates a file: its directory entry must already have been durably provisioned.
        // FileShare.None also excludes another journal writer while repairing a torn final append.
        var file = new FileStream(PathName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
        if (file.CanSeek) return file;
        file.Dispose();
        throw Unavailable();
    }

    private static void DiscardIncompleteTail(FileStream file)
    {
        var position = file.Length;
        var buffer = new byte[4096];
        while (position > 0)
        {
            var count = (int)Math.Min(buffer.Length, position);
            position -= count;
            file.Position = position;
            file.ReadExactly(buffer, 0, count);
            for (var i = count - 1; i >= 0; i--)
            {
                if (buffer[i] != (byte)'\n') continue;
                var boundary = position + i + 1;
                if (boundary != file.Length) file.SetLength(boundary);
                file.Position = boundary;
                return;
            }
        }
        file.SetLength(0);
        file.Position = 0;
    }

    internal async Task AppendAsync(Guid accountId, bool completed, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await using var file = Open();
            DiscardIncompleteTail(file);
            // One write contains the entire JSON record and newline. Deletion cannot start before fsync succeeds.
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Record(accountId, DateTimeOffset.UtcNow, completed)) + "\n");
            await file.WriteAsync(bytes, ct);
            file.Flush(true);
        }
        finally { Gate.Release(); }
    }

    internal async Task<Guid[]> AccountsAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await using var file = Open();
            var ids = new HashSet<Guid>();
            var buffer = new byte[4096];
            var line = new byte[1024];
            var length = 0;
            int count;
            while ((count = await file.ReadAsync(buffer, ct)) > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    if (buffer[i] != (byte)'\n')
                    {
                        if (length < line.Length) line[length] = buffer[i];
                        if (length <= line.Length) length++;
                        continue;
                    }
                    if (length == 0 || length > line.Length) throw new IOException("Invalid erasure journal record.");
                    Record record;
                    try { record = JsonSerializer.Deserialize<Record>(line.AsSpan(0, length)) ?? throw new JsonException(); }
                    catch (JsonException) { throw new IOException("Invalid erasure journal record."); }
                    if (record.AccountId == Guid.Empty || record.Timestamp == default) throw new IOException("Invalid erasure journal record.");
                    ids.Add(record.AccountId);
                    length = 0;
                }
            }
            // A suffix without a newline was never acknowledged as a durable intent. Keep complete records only.
            return ids.ToArray();
        }
        finally { Gate.Release(); }
    }

    private sealed record Record([property: System.Text.Json.Serialization.JsonRequired] Guid AccountId,
        [property: System.Text.Json.Serialization.JsonRequired] DateTimeOffset Timestamp,
        [property: System.Text.Json.Serialization.JsonRequired] bool Completed);
}
