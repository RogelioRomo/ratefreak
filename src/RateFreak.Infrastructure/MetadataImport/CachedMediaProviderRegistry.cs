using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Tmdb;
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class CachedMediaProviderRegistry(IEnumerable<IMediaProvider> providers, ProviderMetadataCache cache, TmdbContentPolicy policy, IgdbContentPolicy igdbPolicy) : IMediaProviderRegistry
{
    private readonly MediaProviderRegistry registry = new(providers.Select(provider => Wrap(provider, cache, policy, igdbPolicy)));
    private static IMediaProvider Wrap(IMediaProvider provider, ProviderMetadataCache cache, TmdbContentPolicy policy, IgdbContentPolicy igdbPolicy)
    {
        var cached = new CachedMediaProvider(provider, cache);
        return provider.Capability.Name == "tmdb" ? new PolicyCheckedTmdbProvider(cached, policy) : provider.Capability.Name == "igdb" ? new PolicyCheckedIgdbProvider(cached, igdbPolicy) : cached;
    }
    public IReadOnlyList<ProviderCapability> Capabilities => registry.Capabilities;
    public IMediaProvider? Find(string name) => registry.Find(name);
}
