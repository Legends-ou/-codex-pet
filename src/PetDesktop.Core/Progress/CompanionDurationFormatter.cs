namespace PetDesktop.Core.Progress;

public static class CompanionDurationFormatter
{
    private const long MinuteSeconds = 60;
    private const long HourSeconds = 60 * MinuteSeconds;
    private const long DaySeconds = 24 * HourSeconds;
    private const long MonthSeconds = 30 * DaySeconds;
    private const long YearSeconds = 365 * DaySeconds;

    public static string Format(long totalSeconds)
    {
        var remaining = Math.Max(0, totalSeconds);
        var parts = new List<string>();
        AppendPart(parts, ref remaining, YearSeconds, "年");
        AppendPart(parts, ref remaining, MonthSeconds, "个月");
        AppendPart(parts, ref remaining, DaySeconds, "天");
        AppendPart(parts, ref remaining, HourSeconds, "小时");

        var minutes = remaining / MinuteSeconds;
        if (minutes > 0 || parts.Count == 0) parts.Add($"{minutes} 分钟");
        return string.Join(" ", parts);
    }

    private static void AppendPart(List<string> parts, ref long remaining, long unitSeconds, string label)
    {
        var value = remaining / unitSeconds;
        if (value == 0) return;
        parts.Add($"{value} {label}");
        remaining %= unitSeconds;
    }
}
