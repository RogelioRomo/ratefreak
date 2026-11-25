using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using System.Text.Json;
using RateFreak.Infrastructure.Providers.Tmdb;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class MediaImportService(RateFreakDbContext context, IMediaProviderRegistry providers, TmdbContentPolicy policy, TimeProvider clock) : IMediaImportService
{
    public async Task<ImportMediaResult?> ImportAsync(string providerName, string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity(providerName, type, externalId, out var identity))
            throw new ArgumentException("Invalid provider identity.");
        var provider = providers.Find(providerName) ?? throw new ArgumentException("Unknown provider.");
        var tmdb = identity.Provider == "tmdb";
        if (tmdb && policy.ForOperation().Blocks(identity.Namespace, identity.ExternalId)) return null;
        var existing = await FindAsync(identity, cancellationToken);
        if (existing is not null)
            return ExistingResult(identity, existing.Value);
        var details = await provider.GetDetailsAsync(identity.ExternalId, type, cancellationToken);
        if (details is null) return null;
        if (details.Provider != providerName || details.ExternalId != identity.ExternalId || details.Type != type)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var sourceImage = details.ImageReference;
        if (tmdb)
        {
            await TmdbLifecycle.AcquireWriterAsync(context, cancellationToken);
            details = policy.ForOperation().Filter(details);
            if (details == null) return null;
        }
        MediaItem item;
        try { item = new MediaItem(type, details.Title, details.Description, details.ReleaseDate, details.ImageReference, details.Metadata.GetRawText()); }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
        { throw new MediaProviderException(ProviderFailure.InvalidResponse); }
        var mapping = new MediaExternalId(item.Id, identity.Provider, identity.Namespace, identity.ExternalId, identity.ExternalUrl, clock.GetUtcNow());
        try
        {
            context.Add(item);
            context.Add(mapping);
            await context.SaveChangesAsync(cancellationToken);
            if (tmdb) await TmdbLifecycle.RememberPosterAsync(context, mapping.Id, sourceImage, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ImportMediaResult(item.Id, true);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_media_external_ids_identity" })
        {
            await transaction.RollbackAsync(cancellationToken);
            context.Entry(mapping).State = EntityState.Detached;
            context.Entry(item).State = EntityState.Detached;
            var winner = await FindAsync(identity, cancellationToken);
            if (winner is null) throw;
            return ExistingResult(identity, winner.Value);
        }
    }

    private ImportMediaResult? ExistingResult(ProviderIdentity identity, Guid id) =>
        identity.Provider == "tmdb" && policy.ForOperation().Blocks(identity.Namespace, identity.ExternalId)
            ? null
            : new ImportMediaResult(id, false);

    private Task<Guid?> FindAsync(ProviderIdentity identity, CancellationToken cancellationToken) =>
        context.MediaExternalIds.AsNoTracking().Where(x => x.Provider == identity.Provider && x.Namespace == identity.Namespace && x.ExternalId == identity.ExternalId)
            .Select(x => (Guid?)x.MediaItemId).SingleOrDefaultAsync(cancellationToken);
}
