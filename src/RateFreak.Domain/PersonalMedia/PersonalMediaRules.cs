namespace RateFreak.Domain.PersonalMedia;

public static class PersonalMediaRules
{
    public static bool ValidStatus(string? status) => status is "planned" or "in_progress" or "completed" or "paused" or "dropped";
    public static bool ValidScore(decimal? score) => score is >= 0m and <= 5m && score.Value % 0.5m == 0m;
    public static bool ValidDates(DateOnly? startedAt, DateOnly? completedAt) =>
        startedAt is null || completedAt is null || completedAt >= startedAt;
    public static bool ValidBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 10000) return false;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (char.IsControl(c) && c is not '\n' and not '\r' and not '\t') return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i >= body.Length || !char.IsLowSurrogate(body[i])) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        return true;
    }
}
