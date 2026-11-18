using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class CachedMediaProviderRegistry(IEnumerable<IMediaProvider> providers, ProviderMetadataCache cache) : IMediaProviderRegistry
{
    private readonly MediaProviderRegistry registry = new(providers.Select(provider => new CachedMediaProvider(provider, cache)));
    public IReadOnlyList<ProviderCapability> Capabilities => registry.Capabilities;
    public IMediaProvider? Find(string name) => registry.Find(name);
}
