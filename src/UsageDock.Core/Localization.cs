using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace UsageDock.Core;

public sealed record LanguageOption(string Code, string NativeName) { public override string ToString() => NativeName; }

/// <summary>Presentation-only localization. Never use its culture for protocol or storage data.</summary>
public static class Localization
{
    public static IReadOnlyList<LanguageOption> Languages { get; } = Array.AsReadOnly(new[]
    {
        new LanguageOption("pl", "Polski"), new LanguageOption("en", "English"),
        new LanguageOption("de", "Deutsch"), new LanguageOption("fr", "Français"),
        new LanguageOption("es", "Español")
    });
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs = LoadCatalogs();
    private sealed record Selection(string Preference, string Language);
    private static Selection selection = new("pl", "pl");
    public static string SelectedLanguage => selection.Preference;
    public static string CurrentLanguage => selection.Language;
    public static CultureInfo Culture => GetCulture(CurrentLanguage);
    public static event Action? Changed;

    public static string NormalizePreference(string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference)) return "auto";
        var normalized = preference.Trim().ToLowerInvariant();
        return normalized == "auto" || Languages.Any(x => x.Code == normalized) ? normalized : "en";
    }
    public static string ResolveLanguage(string? preference, CultureInfo? windowsCulture = null)
    {
        var normalized = NormalizePreference(preference);
        if (normalized != "auto") return normalized;
        var windowsLanguage = (windowsCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return Languages.Any(x => x.Code == windowsLanguage) ? windowsLanguage : "en";
    }
    // Call from the UI dispatcher. Formatting and protocol threads do not change selection.
    public static void SetLanguage(string? preference, CultureInfo? windowsCulture = null)
    {
        var next = new Selection(NormalizePreference(preference), ResolveLanguage(preference, windowsCulture));
        var changed = selection.Language != next.Language;
        selection = next;
        if (changed) Changed?.Invoke();
    }
    public static CultureInfo GetCulture(string? language) => CultureInfo.GetCultureInfo(ResolveLanguage(language) switch
    {
        "pl" => "pl-PL", "de" => "de-DE", "fr" => "fr-FR", "es" => "es-ES", _ => "en-US"
    });
    public static IReadOnlyDictionary<string, string> Catalog(string language) => catalogs[ResolveLanguage(language)];
    public static string Text(string key, params object?[] arguments) => TextFor(CurrentLanguage, key, arguments);
    public static string TextFor(string? language, string key, params object?[] arguments)
    {
        var resolved = ResolveLanguage(language);
        var value = catalogs[resolved].GetValueOrDefault(key) ?? catalogs["en"].GetValueOrDefault(key) ?? key;
        return arguments.Length == 0 ? value : string.Format(GetCulture(resolved), value, arguments);
    }
    public static string Money(decimal? value, string? language = null) => value.HasValue
        ? value.Value.ToString("0.##", GetCulture(language ?? CurrentLanguage)) + " USD" : "—";
    public static string Tokens(long? value, string? language = null) => value.HasValue
        ? value.Value.ToString("N0", GetCulture(language ?? CurrentLanguage)) : "—";
    public static string Percent(double? value, string? language = null) => value.HasValue
        ? value.Value.ToString("0.#", GetCulture(language ?? CurrentLanguage)) + "%" : "—";
    public static bool TryParseBudget(string? input, string? language, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        const NumberStyles styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(input, styles, GetCulture(language ?? CurrentLanguage), out var amount) || amount <= 0) return false;
        value = amount;
        return true;
    }
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        var assembly = typeof(Localization).Assembly;
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            using var stream = assembly.GetManifestResourceStream($"UsageDock.Core.Localization.{language.Code}.json")
                ?? throw new InvalidDataException($"Missing language catalog: {language.Code}");
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                ?? throw new InvalidDataException($"Invalid language catalog: {language.Code}");
            result.Add(language.Code, new ReadOnlyDictionary<string, string>(values));
        }
        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(result);
    }
}
