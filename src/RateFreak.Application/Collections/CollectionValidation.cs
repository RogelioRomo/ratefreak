using RateFreak.Domain.Collections;

namespace RateFreak.Application.Collections;

public static class CollectionValidation
{
    public static Dictionary<string, string[]> Metadata(string? name, string? description, string? visibility)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CollectionRules.ValidText(name?.Trim(), 120)) errors["name"] = ["Name must contain 1–120 valid characters."];
        if (description is not null && !CollectionRules.ValidText(description, 2000, true, true)) errors["description"] = ["Description must contain at most 2000 valid characters."];
        if (visibility is not ("private" or "public")) errors["visibility"] = ["Visibility must be private or public."];
        return errors;
    }

}
