namespace RateFreak.Domain.Collections;

public static class CollectionRules
{
    public static bool ValidText(string? value, int max, bool multiline = false, bool allowBlank = false)
    {
        if (value is null || value.Length > max || (!allowBlank && string.IsNullOrWhiteSpace(value))) return false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsControl(c) && !(multiline && c is '\n' or '\r' or '\t')) return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i >= value.Length || !char.IsLowSurrogate(value[i])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
    public static bool ValidKey(string? key) => key is { Length: >= 1 and <= 40 } && key[0] is >= 'a' and <= 'z'
        && key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
        && key is not ("id" or "title" or "media_item_id" or "created_at" or "updated_at");
}
