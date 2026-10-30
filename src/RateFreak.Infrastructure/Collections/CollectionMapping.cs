using System.Text.Json;
using RateFreak.Application.Collections;
using RateFreak.Domain.Collections;

namespace RateFreak.Infrastructure.Collections;

internal static class CollectionMapping
{
    public static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    public static string Configuration(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? "{}" : value.GetRawText();
    public static FieldDefinition Definition(CollectionField field) => new(field.Key, field.Label, field.FieldType, field.Required, Json(field.Configuration), field.Position);
    public static FieldDetails Field(CollectionField field) => new(field.Id, field.Key, field.Label, field.FieldType, field.Required, Json(field.Configuration), field.Position);
    public static ItemDetails Item(CollectionItem item, string title) => new(item.Id, item.MediaItemId, title, Json(item.Data), item.Position, item.CreatedAt, item.UpdatedAt);
}
