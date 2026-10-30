namespace RateFreak.Application.MetadataImport;

public interface IMediaProviderRegistry
{
    IReadOnlyList<ProviderCapability> Capabilities { get; }
    IMediaProvider? Find(string name);
}

public sealed class MediaProviderRegistry(IEnumerable<IMediaProvider> providers) : IMediaProviderRegistry
{
    // Last registration wins, preserving the host's ability to replace a typed client.
    private readonly IReadOnlyDictionary<string, IMediaProvider> adapters = providers.GroupBy(x => x.Capability.Name, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
    public IReadOnlyList<ProviderCapability> Capabilities => adapters.Values.Select(x => x.Capability).ToArray();
    public IMediaProvider? Find(string name) => adapters.GetValueOrDefault(name);
}

public sealed record ProviderIdentity(string Provider, string Namespace, string ExternalId, string ExternalUrl);
