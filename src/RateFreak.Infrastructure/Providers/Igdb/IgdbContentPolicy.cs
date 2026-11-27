using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.Igdb;

public sealed record IgdbPolicySnapshot(bool Terminated, IReadOnlySet<string> BlockedIdentities,
    IReadOnlySet<string> BlockedPosters, string Fingerprint)
{
    public bool Blocks(string ns, string id) => Terminated || BlockedIdentities.Contains(ns + ":" + id);

    public ExternalMediaResult? Filter(ExternalMediaResult? result)
    {
        if (result is null || Blocks("game", result.ExternalId)) return null;
        var filename = IgdbContentPolicy.PosterFilename(result.ImageReference);
        return filename != null && BlockedPosters.Contains(filename) ? result with { ImageReference = null } : result;
    }
}

public sealed class IgdbContentPolicy(IOptions<IgdbOptions> options)
{
    private const int Limit = 1024 * 1024;
    internal const string PosterPrefix = "https://images.igdb.com/igdb/image/upload/t_cover_big/";
    public static bool ValidPoster(string value) => value.Length <= 200 &&
        Regex.IsMatch(value, @"\A[A-Za-z0-9_-]+\.(?:jpg)\z", RegexOptions.CultureInvariant);
    public static bool ValidIdentity(string value)
    {
        var parts = value.Split(':');
        return parts.Length == 2 && parts[0] == "game" &&
            MediaProviderInput.TryId(parts[1], out var canonical) && canonical == parts[1];
    }
    internal static string? PosterFilename(string? reference) =>
        reference != null && reference.StartsWith(PosterPrefix, StringComparison.Ordinal) &&
        ValidPoster(reference[PosterPrefix.Length..]) ? reference[PosterPrefix.Length..] : null;

    public IgdbPolicySnapshot Read()
    {
        using var directory = OpenDirectory(out var filename);
        using var handle = Unix.OpenAt(directory, filename, Unix.NoFollow | Unix.NonBlock);
        var stat = Unix.Status(handle);
        if (stat.Mode != 0x8180 || stat.Uid != Unix.EffectiveUid() || stat.Size > Limit || stat.Links != 1) throw Invalid();
        using var stream = new FileStream(handle, FileAccess.Read);
        var bytes = new byte[(int)stat.Size];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 ||
                !root.TryGetProperty("format", out var format) || format.GetRawText() != "1" ||
                !root.TryGetProperty("terminated", out var terminated) || terminated.ValueKind is not (JsonValueKind.False or JsonValueKind.True))
                throw Invalid();
            return new(terminated.GetBoolean(), ReadArray(root, "blockedIdentities", ValidIdentity),
                ReadArray(root, "blockedPosters", ValidPoster), Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException) { throw Invalid(); }
    }

    internal IgdbPolicySnapshot ForOperation()
    {
        IgdbPolicySnapshot snapshot;
        try { snapshot = Read(); }
        catch (IOException) { throw new MediaProviderException(ProviderFailure.Unavailable); }
        if (snapshot.Terminated) throw new MediaProviderException(ProviderFailure.Disabled);
        return snapshot;
    }

    private static HashSet<string> ReadArray(JsonElement root, string property, Func<string, bool> valid)
    {
        if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() > 10000) throw Invalid();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in values.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } value || !valid(value) || !result.Add(value)) throw Invalid();
        }
        return result;
    }

    // Caller holds the database's exclusive maintenance lock for this read/merge/replace.
    internal void Block(IgdbPolicySnapshot previous, string? identity, bool terminate, IEnumerable<string> posters)
    {
        var ids = previous.BlockedIdentities.ToHashSet(StringComparer.Ordinal);
        var images = previous.BlockedPosters.ToHashSet(StringComparer.Ordinal);
        if (identity != null) ids.Add(identity);
        images.UnionWith(posters);
        if (ids.Count > 10000 || images.Count > 10000 || ids.Any(id => !ValidIdentity(id)) || images.Any(p => !ValidPoster(p))) throw Invalid();
        if ((!terminate || previous.Terminated) && ids.Count == previous.BlockedIdentities.Count && images.Count == previous.BlockedPosters.Count) return;
        var data = JsonSerializer.SerializeToUtf8Bytes(new { format = 1, terminated = terminate || previous.Terminated,
            blockedIdentities = ids.Order(StringComparer.Ordinal), blockedPosters = images.Order(StringComparer.Ordinal) });
        if (data.Length > Limit) throw Invalid();
        using var directory = OpenDirectory(out var filename);
        var temp = ".igdb-policy-" + Guid.NewGuid().ToString("N");
        using var handle = Unix.OpenAt(directory, temp, Unix.NoFollow | 1 | 0x40 | 0x80, 0x180);
        try
        {
            using var stream = new FileStream(handle, FileAccess.Write);
            stream.Write(data);
            stream.Flush(true);
            if (Unix.RenameAt(directory, temp, directory, filename) != 0 || Unix.Fsync(directory) != 0) throw Invalid();
        }
        finally { Unix.UnlinkAt(directory, temp, 0); }
    }

    private SafeFileHandle OpenDirectory(out string filename)
    {
        var path = options.Value.PolicyPath;
        if (!OperatingSystem.IsLinux() || string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) ||
            path.Contains('\0') || path != Path.GetFullPath(path)) throw Invalid();
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || path.EndsWith('/')) throw Invalid();
        filename = parts[^1];
        var current = Unix.OpenRoot();
        try
        {
            foreach (var part in parts[..^1])
            {
                var next = Unix.OpenAt(current, part, Unix.Directory | Unix.NoFollow);
                current.Dispose();
                current = next;
                var stat = Unix.Status(current);
                if (stat.Uid != 0 && stat.Uid != Unix.EffectiveUid() ||
                    (stat.Mode & 0x12) != 0 && !(stat.Uid == 0 && (stat.Mode & 0x200) != 0)) throw Invalid();
            }
            var parent = Unix.Status(current);
            if (parent.Uid != Unix.EffectiveUid() || (parent.Mode & 0x3f) != 0) throw Invalid();
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    private static IOException Invalid() => new("A valid, private IGDB policy is required.");

    // Linux statx has a fixed-width ABI on all reference container architectures.
    // openat anchors every operation to a checked directory and O_NONBLOCK avoids hanging on a FIFO.
    private static class Unix
    {
        internal const int Directory = 0x10000, NoFollow = 0x20000, NonBlock = 0x800, CloseOnExec = 0x80000;
        [StructLayout(LayoutKind.Explicit, Size = 256)]
        internal struct Stat
        {
            [FieldOffset(16)] public uint Links;
            [FieldOffset(20)] public uint Uid;
            [FieldOffset(28)] public ushort Mode;
            [FieldOffset(40)] public ulong Size;
        }
        [DllImport("libc", EntryPoint = "geteuid")] internal static extern uint EffectiveUid();
        [DllImport("libc", EntryPoint = "open")] private static extern int Open(string path, int flags);
        [DllImport("libc", EntryPoint = "openat")] private static extern int Open(SafeFileHandle dir, string name, int flags, int mode);
        [DllImport("libc", EntryPoint = "statx")] private static extern int Statx(SafeFileHandle fd, string path, int flags, uint mask, out Stat stat);
        [DllImport("libc", EntryPoint = "renameat")] internal static extern int RenameAt(SafeFileHandle d1, string from, SafeFileHandle d2, string to);
        [DllImport("libc", EntryPoint = "unlinkat")] internal static extern int UnlinkAt(SafeFileHandle dir, string name, int flags);
        [DllImport("libc", EntryPoint = "fsync")] internal static extern int Fsync(SafeFileHandle dir);
        private static SafeFileHandle Handle(int fd) => fd < 0 ? throw Invalid() : new SafeFileHandle((IntPtr)fd, true);
        internal static SafeFileHandle OpenRoot() => Handle(Open("/", Directory | CloseOnExec));
        internal static SafeFileHandle OpenAt(SafeFileHandle dir, string name, int flags, int mode = 0) => Handle(Open(dir, name, flags | CloseOnExec, mode));
        internal static Stat Status(SafeFileHandle file) => Statx(file, "", 0x1000, 0x7ff, out var stat) == 0 ? stat : throw Invalid();
    }
}
