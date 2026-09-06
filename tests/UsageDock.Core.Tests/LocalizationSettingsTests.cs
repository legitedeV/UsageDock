using System.Text.Json;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class LocalizationSettingsTests
{
    [Fact] public void NewSettingsUseAutomaticLanguage()
    {
        var json = JsonSerializer.Serialize(new AppSettings());
        Assert.Contains("\"Language\":\"auto\"", json);
    }
    [Fact] public void ExistingSettingsWithoutLanguageRetainPolish()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UsageDock-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Connections\":[],\"Settings\":{\"Theme\":\"Light\",\"RefreshSeconds\":777,\"NotificationsEnabled\":true,\"StartWithWindows\":false}}");
            var state = new LocalStore(directory).Load();
            Assert.Contains("\"Language\":\"pl\"", JsonSerializer.Serialize(state.Settings));
            Assert.Equal("Light", state.Settings.Theme);
            Assert.Equal(777, state.Settings.RefreshSeconds);
            Assert.True(state.Settings.NotificationsEnabled);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData("en", "en")]
    [InlineData("de", "de")]
    [InlineData("fr", "fr")]
    [InlineData("es", "es")]
    [InlineData("auto", "auto")]
    [InlineData("DE", "de")]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("unknown", "en")]
    public void LanguageRoundtripsWithoutChangingConnections(string? preference, string expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "UsageDock-tests-" + Guid.NewGuid());
        var store = new LocalStore(directory);
        var profile = new ConnectionProfile(Guid.NewGuid(), "User name ä 中文", ProviderKind.Codex, "unchanged-id");
        try
        {
            var settings = new AppSettings("Light", 777, true, false, preference!);
            store.Save(new StoredState(new[] { profile }, settings));
            var loaded = store.Load();
            Assert.Equal(expected, loaded.Settings.Language);
            Assert.Equal(profile, Assert.Single(loaded.Connections));
            Assert.Equal(settings.Theme, loaded.Settings.Theme);
            Assert.Equal(settings.RefreshSeconds, loaded.Settings.RefreshSeconds);
            store.Save(loaded with { Settings = loaded.Settings with { RefreshSeconds = 888 } });
            Assert.Equal(expected, store.Load().Settings.Language);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact] public void MissingStoreCreatesAutomaticSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UsageDock-tests-" + Guid.NewGuid());
        Assert.Equal("auto", new LocalStore(directory).Load().Settings.Language);
        Assert.False(Directory.Exists(directory));
    }}
