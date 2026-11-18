using System.Text.Json;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Collections;

namespace RateFreak.Infrastructure.MetadataImport;

public sealed class CachedMediaProvider(IMediaProvider inner, ProviderMetadataCache cache) : IMediaProvider
{
    public ProviderCapability Capability => inner.Capability;

    public async Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (!CollectionRules.ValidText(query, 200) || !Capability.SupportedTypes.Contains(type) || page is < 1 or > 500)
            throw new ArgumentException("Invalid provider search input.");
        EnsureEnabled(cancellationToken);
        var key = JsonSerializer.Serialize(new[] { Capability.Name, "search", type, query, page.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return (await cache.GetAsync(key, TimeSpan.FromMinutes(5), async ct => await inner.SearchAsync(query, type, page, ct), cancellationToken))!;
    }

    public Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity(Capability.Name, type, externalId, out var identity) || !Capability.SupportedTypes.Contains(type))
            throw new ArgumentException("Invalid provider identity.");
        EnsureEnabled(cancellationToken);
        var key = JsonSerializer.Serialize(new[] { identity.Provider, "details", type, identity.ExternalId });
        return cache.GetAsync(key, TimeSpan.FromHours(1), ct => inner.GetDetailsAsync(identity.ExternalId, type, ct), cancellationToken);
    }

    private void EnsureEnabled(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Capability.Enabled) throw new MediaProviderException(ProviderFailure.Disabled);
    }
}
