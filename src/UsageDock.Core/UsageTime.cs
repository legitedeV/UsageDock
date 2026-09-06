using System;
using System.Globalization;

namespace UsageDock.Core;

public static class UsageTime
{
    // Existing callers keep their Polish presentation until explicitly migrated.
    public static string Relative(DateTimeOffset? target, DateTimeOffset now) => Relative(target, now, "pl");
    public static string Absolute(DateTimeOffset? target, TimeZoneInfo zone) => Absolute(target, zone, "pl");
    public static string Full(DateTimeOffset? target, DateTimeOffset now, TimeZoneInfo zone) => Full(target, now, zone, "pl");

    public static string Relative(DateTimeOffset? target, DateTimeOffset now, string language)
    {
        string Text(string key, params object?[] values) => Localization.TextFor(language, key, values);
        if (target == null) return Text("time.unknown");
        var remaining = target.Value - now;
        if (remaining <= TimeSpan.Zero) return Text("time.expired");
        if (remaining < TimeSpan.FromMinutes(1)) return Text("time.soon");
        var minutes = (long)Math.Floor(remaining.TotalMinutes);
        var days = minutes / 1440;
        var hours = minutes % 1440 / 60;
        var rest = minutes % 60;
        string duration;
        if (days > 0)
        {
            duration = Text(days == 1 ? "time.day" : "time.days", days);
            if (hours > 0) duration += " " + Text("time.hours", hours);
            else if (rest > 0) duration += " " + Text("time.minutes", rest);
        }
        else if (hours > 0)
        {
            duration = Text("time.hours", hours);
            if (rest > 0) duration += " " + Text("time.minutes", rest);
        }
        else duration = Text("time.minutes", minutes);
        return Text("time.in", duration);
    }
    public static string Absolute(DateTimeOffset? target, TimeZoneInfo zone, string language)
    {
        if (target == null) return Localization.TextFor(language, "time.unknown");
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(target.Value, zone);
        return local.ToString("d MMM yyyy, HH:mm", Localization.GetCulture(language))
            + " (UTC" + local.ToString("zzz", CultureInfo.InvariantCulture) + ")";
    }
    public static string Full(DateTimeOffset? target, DateTimeOffset now, TimeZoneInfo zone, string language)
        => target == null ? Localization.TextFor(language, "time.unknown")
            : Absolute(target, zone, language) + " · " + Relative(target, now, language);
}
