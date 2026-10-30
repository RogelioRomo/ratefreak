using System.Globalization;
using System.Text.Json;
using RateFreak.Domain.Collections;

namespace RateFreak.Application.Collections;

public static class CollectionValidation
{
    public static Dictionary<string, string[]> ValidateFields(IReadOnlyList<FieldDefinition> fields)
    {
        var errors = new Dictionary<string, string[]>();
        if (fields.Count > 30) errors["fields"] = ["At most 30 fields are allowed."];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field is null) { errors[$"fields[{i}]"] = ["Field is required."]; continue; }
            if (!CollectionRules.ValidKey(field.Key) || !keys.Add(field.Key!)) errors[$"fields[{i}].key"] = ["Key is invalid, reserved or duplicated."];
            if (!CollectionRules.ValidText(field.Label?.Trim(), 80)) errors[$"fields[{i}].label"] = ["Label must contain 1–80 valid characters."];
            if (field.Position is < 0) errors[$"fields[{i}].position"] = ["Position must be nonnegative."];
            if (!ValidConfiguration(field)) errors[$"fields[{i}].configuration"] = ["Unsupported field type or invalid configuration."];
        }
        return errors;
    }

    public static Dictionary<string, string[]> ValidateData(JsonElement data, IReadOnlyList<FieldDefinition> fields)
    {
        var errors = new Dictionary<string, string[]>();
        if (data.ValueKind != JsonValueKind.Object || !ValidJson(data))
        { errors["data"] = ["Data must be an object with unique keys, valid strings and depth at most 16."]; return errors; }
        var known = fields.ToDictionary(x => x.Key!, StringComparer.Ordinal);
        foreach (var p in data.EnumerateObject())
        {
            if (!known.TryGetValue(p.Name, out var field)) { errors["data." + p.Name] = ["Unknown field."]; continue; }
            if (p.Value.ValueKind == JsonValueKind.Null && !field.Required) continue;
            if (!ValidValue(p.Value, field)) errors["data." + p.Name] = ["Value does not match the field definition."];
        }
        foreach (var field in fields.Where(x => x.Required))
            if (!data.TryGetProperty(field.Key!, out var value) || value.ValueKind == JsonValueKind.Null)
                errors["data." + field.Key] = ["A non-null value is required."];
        return errors;
    }

    public static Dictionary<string, string[]> Metadata(string? name, string? description, string? visibility)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CollectionRules.ValidText(name?.Trim(), 120)) errors["name"] = ["Name must contain 1–120 valid characters."];
        if (description is not null && !CollectionRules.ValidText(description, 2000, true, true)) errors["description"] = ["Description must contain at most 2000 valid characters."];
        if (visibility is not ("private" or "public")) errors["visibility"] = ["Visibility must be private or public."];
        return errors;
    }

    public static bool ValidJson(JsonElement value, int depth = 0)
    {
        try { return ValidJsonCore(value, depth); }
        catch (InvalidOperationException) { return false; }
    }

    private static bool ValidJsonCore(JsonElement value, int depth)
    {
        if (depth > 16) return false;
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in value.EnumerateObject())
                    if (!keys.Add(p.Name) || !CollectionRules.ValidText(p.Name, 4000, true, true) || !ValidJson(p.Value, depth + 1)) return false;
                return true;
            case JsonValueKind.Array: return value.EnumerateArray().All(x => ValidJson(x, depth + 1));
            case JsonValueKind.String:
                try { return CollectionRules.ValidText(value.GetString(), 65536, true, true); }
                catch (InvalidOperationException) { return false; }
            case JsonValueKind.Undefined: return false;
            default: return true;
        }
    }

    private static bool ValidConfiguration(FieldDefinition field)
    {
        var config = field.Configuration;
        if (config.ValueKind == JsonValueKind.Undefined) config = JsonSerializer.SerializeToElement(new { });
        if (config.ValueKind != JsonValueKind.Object || !ValidJson(config)) return false;
        var allowed = field.FieldType switch
        {
            "text" => new[] { "maxLength" }, "number" => ["min", "max"], "boolean" or "date" => [], "select" => ["options"], _ => null
        };
        if (allowed is null || config.EnumerateObject().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal))) return false;
        switch (field.FieldType)
        {
            case "text": return !config.TryGetProperty("maxLength", out var length) || length.ValueKind == JsonValueKind.Number && length.TryGetInt32(out var n) && n is >= 1 and <= 4000;
            case "number":
                decimal? min = null, max = null;
                if (config.TryGetProperty("min", out var lower)) { if (lower.ValueKind != JsonValueKind.Number || !lower.TryGetDecimal(out var lowerNumber)) return false; min = lowerNumber; }
                if (config.TryGetProperty("max", out var upper)) { if (upper.ValueKind != JsonValueKind.Number || !upper.TryGetDecimal(out var upperNumber)) return false; max = upperNumber; }
                return min is null || max is null || min <= max;
            case "select":
                if (!config.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array || options.GetArrayLength() is < 1 or > 50) return false;
                var set = new HashSet<string>(StringComparer.Ordinal);
                return options.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String && CollectionRules.ValidText(x.GetString(), 80) && set.Add(x.GetString()!));
            default: return true;
        }
    }

    private static bool ValidValue(JsonElement value, FieldDefinition field)
    {
        var config = field.Configuration;
        switch (field.FieldType)
        {
            case "text":
                var max = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("maxLength", out var length) ? length.GetInt32() : 1000;
                return value.ValueKind == JsonValueKind.String && CollectionRules.ValidText(value.GetString(), max, true, !field.Required);
            case "number":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var n)) return false;
                return config.ValueKind != JsonValueKind.Object ||
                    ((!config.TryGetProperty("min", out var min) || n >= min.GetDecimal()) && (!config.TryGetProperty("max", out var upper) || n <= upper.GetDecimal()));
            case "boolean": return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            case "date": return value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            case "select": return value.ValueKind == JsonValueKind.String && config.GetProperty("options").EnumerateArray().Any(x => x.GetString() == value.GetString());
            default: return false;
        }
    }
}
