using RateFreak.Application.MetadataImport;

namespace RateFreak.Infrastructure.Providers.Tmdb;

// Enforce the current policy outside the response cache, including cache hits and in-flight requests.
internal sealed class PolicyCheckedTmdbProvider(IMediaProvider inner, TmdbContentPolicy policy) : IMediaProvider
{
    public ProviderCapability Capability
    {
        get
        {
            var capability = inner.Capability;
            if (!capability.Enabled) return capability;
            try { return capability with { Enabled = !policy.Read().Terminated }; }
            catch (IOException) { return capability with { Enabled = false }; }
        }
    }
    public async Task<ExternalSearchPage> SearchAsync(string query, string type, int page, CancellationToken ct)
    {
        policy.ForOperation();
        var result = await inner.SearchAsync(query, type, page, ct);
        var current = policy.ForOperation();
        return result with { Items = result.Items.Select(current.Filter).OfType<ExternalMediaResult>().ToArray() };
    }

    public async Task<ExternalMediaResult?> GetDetailsAsync(string externalId, string type, CancellationToken ct)
    {
        if (!MediaProviderInput.TryIdentity("tmdb", type, externalId, out var identity)) throw new ArgumentException("Invalid TMDB identity.");
        if (policy.ForOperation().Blocks(identity.Namespace, identity.ExternalId)) return null;
        var details = await inner.GetDetailsAsync(identity.ExternalId, type, ct);
        return policy.ForOperation().Filter(details);
    }
}
