using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using System.Text.Json;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class MediaImportService(RateFreakDbContext context, IMediaProviderRegistry providers) : IMediaImportService
{
    public async Task<ImportMediaResult?> ImportAsync(string providerName, string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryIdentity(providerName, type, externalId, out var identity))
            throw new ArgumentException("Invalid provider identity.");
        var provider = providers.Find(providerName) ?? throw new ArgumentException("Unknown provider.");
        var existing = await FindAsync(identity, cancellationToken);
        if (existing is not null) return new ImportMediaResult(existing.Value, false);
        var details = await provider.GetDetailsAsync(identity.ExternalId, type, cancellationToken);
        if (details is null) return null;
        if (details.Provider != providerName || details.ExternalId != identity.ExternalId || details.Type != type)
            throw new MediaProviderException(ProviderFailure.InvalidResponse);
        MediaItem item;
        try { item = new MediaItem(type, details.Title, details.Description, details.ReleaseDate, details.ImageReference, details.Metadata.GetRawText()); }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
        { throw new MediaProviderException(ProviderFailure.InvalidResponse); }
        var mapping = new MediaExternalId(item.Id, identity.Provider, identity.Namespace, identity.ExternalId, identity.ExternalUrl, DateTimeOffset.UtcNow);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Add(item);
            context.Add(mapping);
            await context.SaveChangesAsync(cancellationToken);
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
            return new ImportMediaResult(winner.Value, false);
        }
    }

    private Task<Guid?> FindAsync(ProviderIdentity identity, CancellationToken cancellationToken) =>
        context.MediaExternalIds.AsNoTracking().Where(x => x.Provider == identity.Provider && x.Namespace == identity.Namespace && x.ExternalId == identity.ExternalId)
            .Select(x => (Guid?)x.MediaItemId).SingleOrDefaultAsync(cancellationToken);
}
