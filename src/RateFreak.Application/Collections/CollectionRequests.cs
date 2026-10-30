using System.Text.Json;
using System.Text.Json.Serialization;

namespace RateFreak.Application.Collections;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldDefinition(string? Key, string? Label, string? FieldType, bool Required, JsonElement Configuration, int? Position = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectionCreate(string? Name, string? Description, string? Visibility, IReadOnlyList<FieldDefinition>? Fields);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectionUpdate(long? ExpectedRevision, string? Name, string? Description, string? Visibility);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldCreate(long? ExpectedRevision, FieldDefinition? Field);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FieldUpdate(long? ExpectedRevision, string? Label, bool? Required, JsonElement Configuration, int? Position);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemCreate(long? ExpectedRevision, Guid? MediaItemId, string? Title, JsonElement Data, int? Position = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ItemUpdate(long? ExpectedRevision, string? Title, JsonElement Data, int? Position);
