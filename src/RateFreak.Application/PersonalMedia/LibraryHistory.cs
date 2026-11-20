namespace RateFreak.Application.PersonalMedia;

public sealed record LibraryHistoryQuery(int? Year, int? Month, int Page, int PageSize);
public sealed record LibraryMonthTotal(int Month, int Count);
public sealed record LibraryHistoryPage(int Year, int? Month, IReadOnlyList<int> AvailableYears,
    IReadOnlyList<LibraryMonthTotal> Months, int AnnualTotal, int TotalCount, int MissingDateCount,
    IReadOnlyList<LibraryItem> Items, int Page, int PageSize);
public interface ILibraryHistoryService
{
    Task<LibraryHistoryPage> GetAsync(Guid userId, LibraryHistoryQuery query, CancellationToken ct);
}
