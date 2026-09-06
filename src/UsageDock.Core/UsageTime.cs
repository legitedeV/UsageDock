using System;
using System.Globalization;

namespace UsageDock.Core;

public static class UsageTime
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string Relative(DateTimeOffset? target, DateTimeOffset now)
    {
        if (target == null) return "Brak terminu";
        var remaining = target.Value - now;
        if (remaining <= TimeSpan.Zero) return "Termin minął — odśwież odczyt";
        if (remaining < TimeSpan.FromMinutes(1)) return "za <1 min";
        var minutes = (long)Math.Floor(remaining.TotalMinutes);
        var days = minutes / 1440;
        var hours = minutes % 1440 / 60;
        var rest = minutes % 60;
        if (days > 0)
        {
            var date = $"za {days} " + (days == 1 ? "dzień" : "dni");
            if (hours > 0) return date + $" {hours} godz.";
            return rest > 0 ? date + $" {rest} min" : date;
        }
        if (hours > 0) return $"za {hours} godz." + (rest > 0 ? $" {rest} min" : "");
        return $"za {minutes} min";
    }

    public static string Absolute(DateTimeOffset? target, TimeZoneInfo zone)
    {
        if (target == null) return "Brak terminu";
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(target.Value, zone);
        return local.ToString("d MMM yyyy, HH:mm", Polish) + " (UTC" + local.ToString("zzz", CultureInfo.InvariantCulture) + ")";
    }

    public static string Full(DateTimeOffset? target, DateTimeOffset now, TimeZoneInfo zone)
        => target == null ? "Brak terminu" : Absolute(target, zone) + " · " + Relative(target, now);
}
