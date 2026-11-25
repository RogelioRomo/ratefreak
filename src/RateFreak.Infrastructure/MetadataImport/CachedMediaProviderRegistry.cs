using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Tmdb;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class CachedMediaProviderRegistry(IEnumerable<IMediaProvider> providers, ProviderMetadataCache cache, TmdbContentPolicy policy) : IMediaProviderRegistry
{
    private readonly MediaProviderRegistry registry = new(providers.Select(provider => Wrap(provider, cache, policy)));
    private static IMediaProvider Wrap(IMediaProvider provider, ProviderMetadataCache cache, TmdbContentPolicy policy)
    {
        var cached = new CachedMediaProvider(provider, cache);
        return provider.Capability.Name == "tmdb" ? new PolicyCheckedTmdbProvider(cached, policy) : cached;
    }
    public IReadOnlyList<ProviderCapability> Capabilities => registry.Capabilities;
    public IMediaProvider? Find(string name) => registry.Find(name);
}
