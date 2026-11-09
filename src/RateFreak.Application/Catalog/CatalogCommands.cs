namespace RateFreak.Application.Catalog;

public enum CatalogStatus { Success, NotFound, Conflict, Invalid, Unavailable, Forbidden }
public sealed record CatalogResult<T>(CatalogStatus Status, T? Value = default, Dictionary<string, string[]>? Errors = null);
