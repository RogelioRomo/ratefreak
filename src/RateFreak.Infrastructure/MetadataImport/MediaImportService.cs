using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Application.MetadataImport;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;
using RateFreak.Infrastructure.Providers.Tmdb;

namespace RateFreak.Infrastructure.MetadataImport;

internal sealed class MediaImportService(RateFreakDbContext context, IMediaProvider provider) : IMediaImportService
{
    public async Task<ImportMediaResult?> ImportAsync(string externalId, string type, CancellationToken cancellationToken)
    {
        if (!MediaProviderInput.TryId(externalId, out var id) || !MediaProviderInput.ValidType(type))
            throw new ArgumentException("Invalid provider identity.");
        var identityNamespace = TmdbMediaProvider.Namespace(type);
        var existing = await FindAsync(id, identityNamespace, cancellationToken);
        if (existing is not null) return new ImportMediaResult(existing.Value, false);
        var details = await provider.GetDetailsAsync(id, type, cancellationToken);
        if (details is null) return null;
        var item = new MediaItem(type, details.Title, details.Description, details.ReleaseDate,
            details.ImageReference, details.Metadata.GetRawText());
        var mapping = new MediaExternalId(item.Id, "tmdb", identityNamespace, id,
            $"https://www.themoviedb.org/{identityNamespace}/{id}", DateTimeOffset.UtcNow);
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
            var winner = await FindAsync(id, identityNamespace, cancellationToken);
            if (winner is null) throw;
            return new ImportMediaResult(winner.Value, false);
        }
    }

    private Task<Guid?> FindAsync(string id, string identityNamespace, CancellationToken cancellationToken) =>
        context.MediaExternalIds.AsNoTracking().Where(x => x.Provider == "tmdb" && x.Namespace == identityNamespace && x.ExternalId == id)
            .Select(x => (Guid?)x.MediaItemId).SingleOrDefaultAsync(cancellationToken);
}
