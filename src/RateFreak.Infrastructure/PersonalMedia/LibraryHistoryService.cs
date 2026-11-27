using RateFreak.Application.MetadataImport;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Catalog;
using RateFreak.Application.PersonalMedia;
using RateFreak.Infrastructure.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure.PersonalMedia;

internal sealed class LibraryHistoryService(RateFreakDbContext context) : ILibraryHistoryService
{
    public async Task<LibraryHistoryPage> GetAsync(Guid userId, LibraryHistoryQuery query, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var completed = context.LibraryEntries.AsNoTracking().Where(entry => entry.UserId == userId && entry.Status == "completed" &&
            context.MediaItems.Where(CatalogAccess.Readable(userId)).Any(media => media.Id == entry.MediaItemId));
        var missingDates = await completed.CountAsync(entry => entry.CompletedAt == null, ct);
        var dated = completed.Where(entry => entry.CompletedAt != null);
        // Npgsql stores the DateOnly extrema as PostgreSQL infinities; extract
        // their calendar parts explicitly instead of casting infinite date_part.
        var years = await dated.Select(entry => entry.CompletedAt == DateOnly.MinValue ? 1 : entry.CompletedAt == DateOnly.MaxValue ? 9999 : entry.CompletedAt!.Value.Year).Distinct().OrderByDescending(year => year).ToListAsync(ct);
        var selectedYear = query.Year ?? (years.Count > 0 ? years[0] : DateTime.UtcNow.Year);
        if (!years.Contains(selectedYear)) { years.Add(selectedYear); years.Sort((a, b) => b.CompareTo(a)); }
        var start = new DateOnly(selectedYear, 1, 1);
        var end = new DateOnly(selectedYear, 12, 31);
        var annualEntries = dated.Where(entry => entry.CompletedAt >= start && entry.CompletedAt <= end);
        var counts = await annualEntries.GroupBy(entry => entry.CompletedAt == DateOnly.MinValue ? 1 : entry.CompletedAt == DateOnly.MaxValue ? 12 : entry.CompletedAt!.Value.Month)
            .Select(group => new LibraryMonthTotal(group.Key, group.Count())).ToListAsync(ct);
        var lookup = counts.ToDictionary(value => value.Month, value => value.Count);
        var months = Enumerable.Range(1, 12).Select(month => new LibraryMonthTotal(month, lookup.GetValueOrDefault(month))).ToArray();
        var annualTotal = months.Sum(value => value.Count);
        var total = query.Month is { } selectedMonth ? months[selectedMonth - 1].Count : annualTotal;
        var periodEntries = annualEntries;
        if (query.Month is { } month)
        {
            var monthStart = new DateOnly(selectedYear, month, 1);
            var monthEnd = new DateOnly(selectedYear, month, DateTime.DaysInMonth(selectedYear, month));
            periodEntries = periodEntries.Where(entry => entry.CompletedAt >= monthStart && entry.CompletedAt <= monthEnd);
        }
        var items = await (from entry in periodEntries
                           join media in context.MediaItems.AsNoTracking() on entry.MediaItemId equals media.Id
                           orderby entry.CompletedAt descending, entry.MediaItemId
                           select new LibraryItem(new MediaSummary(media.Id, media.MediaType, media.Title, media.ReleaseDate, MusicBrainzArtwork.Album(media.ImageReference,
                context.MediaExternalIds.Where(e => media.MediaType == "album" && e.MediaItemId == media.Id && e.Provider == "musicbrainz" && e.Namespace == "release-group").OrderBy(e => e.ExternalId).Select(e => e.ExternalId).FirstOrDefault())),
                               entry.Status, entry.StartedAt, entry.CompletedAt, entry.CreatedAt, entry.UpdatedAt,
                               context.Ratings.Where(rating => rating.UserId == userId && rating.MediaItemId == media.Id)
                                   .Select(rating => (decimal?)rating.Score).FirstOrDefault()))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(selectedYear, query.Month, years, months, annualTotal, total, missingDates, items, query.Page, query.PageSize);
    }
}
