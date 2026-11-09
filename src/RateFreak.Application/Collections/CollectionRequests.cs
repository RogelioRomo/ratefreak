using System.Text.Json.Serialization;
namespace RateFreak.Application.Collections;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectionCreate(string? Name, string? Description, string? Visibility);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectionUpdate(long? ExpectedRevision, string? Name, string? Description, string? Visibility);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemCreate(long? ExpectedRevision, Guid? MediaItemId, int? Position = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemUpdate(long? ExpectedRevision, int? Position);
