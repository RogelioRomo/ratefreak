using System.Globalization;
using System.Text.Json;

namespace RateFreak.Application.Collections;

internal static class CollectionNumbers
{
    public static bool TryGetExactDecimal(JsonElement value, out decimal number)
    {
        number = 0m;
        if (value.ValueKind != JsonValueKind.Number || !Normalize(value.GetRawText(), out var digits, out var power)) return false;
        if (digits == "0") return true;
        if (!value.TryGetDecimal(out number)) return false;
        // TryGetDecimal can round a 29+ digit significand or underflow to zero.
        // Compare exact coefficient/exponent pairs without expanding large exponents.
        return Normalize(number.ToString("G29", CultureInfo.InvariantCulture), out var parsedDigits, out var parsedPower)
            && digits == parsedDigits && power == parsedPower;
    }

    private static bool Normalize(string raw, out string digits, out long power)
    {
        digits = ""; power = 0;
        var exponentAt = raw.IndexOfAny(['e', 'E']);
        var mantissa = exponentAt < 0 ? raw : raw[..exponentAt];
        var negative = mantissa.StartsWith('-');
        if (negative) mantissa = mantissa[1..];
        var pointAt = mantissa.IndexOf('.');
        var fractionLength = pointAt < 0 ? 0 : mantissa.Length - pointAt - 1;
        var coefficient = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (coefficient.Length == 0) { digits = "0"; return true; }
        long exponent = 0;
        if (exponentAt >= 0 && !long.TryParse(raw.AsSpan(exponentAt + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
        var significant = coefficient.TrimEnd('0');
        try { power = checked(exponent - fractionLength + (coefficient.Length - significant.Length)); }
        catch (OverflowException) { return false; }
        digits = negative ? "-" + significant : significant;
        return true;
    }
}
