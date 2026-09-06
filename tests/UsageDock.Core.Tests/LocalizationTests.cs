using System.Globalization;
using System.Text.RegularExpressions;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class LocalizationTests
{
    [Theory]
    [InlineData("auto", "pl-PL", "pl")]
    [InlineData("auto", "en-GB", "en")]
    [InlineData("auto", "de-AT", "de")]
    [InlineData("auto", "fr-CA", "fr")]
    [InlineData("auto", "es-MX", "es")]
    [InlineData("auto", "it-IT", "en")]
    [InlineData("de", "pl-PL", "de")]
    [InlineData("DE", "pl-PL", "de")]
    [InlineData(null, "fr-FR", "fr")]
    [InlineData("", "es-ES", "es")]
    [InlineData("unsupported", "pl-PL", "en")]
    public void ResolvesPreferenceAgainstWindowsUi(string? preference, string windows, string expected)
        => Assert.Equal(expected, Localization.ResolveLanguage(preference, CultureInfo.GetCultureInfo(windows)));

    [Fact] public void CatalogsHaveIdenticalKeysAndValidPlaceholders()
    {
        var english = Localization.Catalog("en");
        Assert.Equal(new[] { "pl", "en", "de", "fr", "es" }, Localization.Languages.Select(x => x.Code));
        foreach (var language in Localization.Languages)
        {
            var catalog = Localization.Catalog(language.Code);
            Assert.Equal(english.Keys.Order(), catalog.Keys.Order());
            foreach (var pair in english)
            {
                var translated = catalog[pair.Key];
                Assert.False(string.IsNullOrWhiteSpace(translated));
                Assert.Equal(Placeholders(pair.Value), Placeholders(translated));
                _ = string.Format(Localization.GetCulture(language.Code), translated, 1, 2, 3, 4);
            }
        }
    }
    private static string[] Placeholders(string value) => Regex.Matches(value, @"(?<!\{)\{(\d+)(?:[^}]*)\}(?!\})").Select(x => x.Groups[1].Value).Order().ToArray();

    [Fact] public void SwitchingNotifiesOnlyWhenEffectiveLanguageChanges()
    {
        var previous = Localization.SelectedLanguage;
        var count = 0;
        void Changed() => count++;
        Localization.SetLanguage("pl");
        Localization.Changed += Changed;
        try
        {
            Localization.SetLanguage("de");
            Assert.Equal("de", Localization.CurrentLanguage);
            Assert.Equal("de-DE", Localization.Culture.Name);
            Assert.Equal("Sprache", Localization.Text("settings.language"));
            Localization.SetLanguage("de");
            Assert.Equal(1, count);
            Localization.SetLanguage("auto", CultureInfo.GetCultureInfo("fr-CA"));
            Assert.Equal("auto", Localization.SelectedLanguage);
            Assert.Equal("fr", Localization.CurrentLanguage);
            Assert.Equal(2, count);
        }
        finally { Localization.Changed -= Changed; Localization.SetLanguage(previous); }
    }
    [Fact] public void MissingKeysRemainDiscoverableAndUnsupportedCatalogFallsBackToEnglish()
    {
        Assert.Equal("missing.key", Localization.TextFor("en", "missing.key"));
        Assert.Equal("Language", Localization.TextFor("xx", "settings.language"));
    }
    [Theory]
    [InlineData("pl", "24,86", true, "24.86")]
    [InlineData("de", "24,86", true, "24.86")]
    [InlineData("fr", "24,86", true, "24.86")]
    [InlineData("es", "24,86", true, "24.86")]
    [InlineData("en", "24.86", true, "24.86")]
    [InlineData("en", "24,86", false, null)]
    [InlineData("pl", "24.86", false, null)]
    [InlineData("en", "2,486", false, null)]
    [InlineData("de", "2.486", false, null)]
    [InlineData("fr", "2 486", false, null)]
    [InlineData("en", "1e2", false, null)]
    [InlineData("en", "-1", false, null)]
    [InlineData("en", "0", false, null)]
    [InlineData("pl", "", true, null)]
    [InlineData("en", " 24.86 ", true, "24.86")]
    public void BudgetUsesLocaleWithoutThousandsAmbiguity(string language, string input, bool expected, string? amount)
    {
        Assert.Equal(expected, Localization.TryParseBudget(input, language, out var value));
        Assert.Equal(amount == null ? null : decimal.Parse(amount, CultureInfo.InvariantCulture), value);
    }
    [Theory]
    [InlineData("pl", "24,86", "12,5", "248")]
    [InlineData("en", "24.86", "12.5", "248")]
    [InlineData("de", "24,86", "12,5", "248")]
    [InlineData("fr", "24,86", "12,5", "248")]
    [InlineData("es", "24,86", "12,5", "248")]
    public void NumericPresentationUsesSelectedCulture(string language, string money, string percent, string tokens)
    {
        Assert.Equal(money + " USD", Localization.Money(24.86m, language));
        Assert.Equal(percent + "%", Localization.Percent(12.5, language));
        Assert.StartsWith(tokens, Localization.Tokens(248320, language));
        Assert.Equal("—", Localization.Money(null, language));
        Assert.Equal("—", Localization.Percent(null, language));
        Assert.Equal("—", Localization.Tokens(null, language));
    }
    [Theory]
    [InlineData("pl", "za 6 dni 3 godz.", "wrz")]
    [InlineData("en", "in 6 days 3 hr", "Sep")]
    [InlineData("de", "in 6 Tagen 3 Std.", "Sept")]
    [InlineData("fr", "dans 6 jours 3 h", "sept")]
    [InlineData("es", "en 6 días 3 h", "sept")]
    public void TimeKeepsPrecisionInEveryLanguage(string language, string expected, string month)
    {
        var now = DateTimeOffset.Parse("2026-09-06T10:00:00Z");
        Assert.Equal(expected, UsageTime.Relative(now.AddDays(6).AddHours(3), now, language));
        Assert.Contains(month, UsageTime.Absolute(now, TimeZoneInfo.Utc, language), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC+00:00", UsageTime.Full(now.AddDays(6).AddHours(3), now, TimeZoneInfo.Utc, language));
        foreach (var minutes in new[] { 1, 45, 60, 134, 1440, 8640, 8641 })
            Assert.False(string.IsNullOrWhiteSpace(UsageTime.Relative(now.AddMinutes(minutes), now, language)));
        Assert.Equal(Localization.TextFor(language, "time.unknown"), UsageTime.Full(null, now, TimeZoneInfo.Utc, language));
        Assert.Equal(Localization.TextFor(language, "time.unknown"), UsageTime.Absolute(null, TimeZoneInfo.Utc, language));
        Assert.Equal(Localization.TextFor(language, "time.expired"), UsageTime.Relative(now, now, language));
        Assert.Equal(Localization.TextFor(language, "time.soon"), UsageTime.Relative(now.AddSeconds(59), now, language));
        Assert.Equal(UsageTime.Relative(now.AddSeconds(60), now, language), UsageTime.Relative(now.AddSeconds(119), now, language));
    }
    [Fact] public void AutoUsesWindowsDisplayCultureRatherThanInstallationCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
            Assert.Equal("fr", Localization.ResolveLanguage("auto"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }    [Fact] public void LanguageOptionsRenderNativeNames() => Assert.All(Localization.Languages, option=>Assert.Equal(option.NativeName,option.ToString()));
}
