using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class StorageTests
{
    [Fact]
    public void RoundtripAndDelete()
    {
        var path = Path.Combine(Path.GetTempPath(), "UsageDock-test-" + Guid.NewGuid()); var store = new LocalStore(path); var id = Guid.NewGuid();
        try
        {
            Assert.Empty(store.Load().Connections); Assert.Null(store.ReadSecret(id));
            store.Save(new(new[] { new ConnectionProfile(id, "A", ProviderKind.Codex) }, new()));
            Assert.Equal("A", Assert.Single(store.Load().Connections).Name);
            store.WriteSecret(id, "test-secret-only"); Assert.Equal("test-secret-only", store.ReadSecret(id));
            Assert.DoesNotContain("test-secret-only", File.ReadAllText(Path.Combine(path, id.ToString("N") + ".secret")));
            store.WriteSecret(id, "second-test"); Assert.Equal("second-test", store.ReadSecret(id));
            store.DeleteSecret(id); Assert.Null(store.ReadSecret(id));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact]
    public void CorruptSettingsCannotOverwrite()
    {
        var path = Path.Combine(Path.GetTempPath(), "UsageDock-test-" + Guid.NewGuid()); Directory.CreateDirectory(path);
        try { File.WriteAllText(Path.Combine(path, "settings.json"), "bad"); var s = new LocalStore(path); Assert.Throws<InvalidDataException>(() => s.Load()); Assert.Throws<InvalidDataException>(() => s.Save(new(Array.Empty<ConnectionProfile>(), new()))); Assert.Equal("bad", File.ReadAllText(Path.Combine(path, "settings.json"))); }
        finally { Directory.Delete(path, true); }
    }
    [Fact]
    public void CorruptSecretCannotOverwrite()
    {
        var path = Path.Combine(Path.GetTempPath(), "UsageDock-test-" + Guid.NewGuid()); Directory.CreateDirectory(path); var id = Guid.NewGuid(); var file = Path.Combine(path, id.ToString("N") + ".secret");
        try { File.WriteAllText(file, "bad"); var s = new LocalStore(path); Assert.Throws<InvalidDataException>(() => s.ReadSecret(id)); s.WriteSecret(id, "replacement"); Assert.Equal("replacement", s.ReadSecret(id)); s.DeleteSecret(id); Assert.Null(s.ReadSecret(id)); }
        finally { Directory.Delete(path, true); }
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"Connections\":[],\"Settings\":{\"RefreshSeconds\":1}}")]
    public void InvalidStateSafe(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "UsageDock-test-" + Guid.NewGuid()); Directory.CreateDirectory(path);
        try { File.WriteAllText(Path.Combine(path, "settings.json"), json); Assert.Throws<InvalidDataException>(() => new LocalStore(path).Load()); } finally { Directory.Delete(path, true); }
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("bad")]
    [InlineData("{\"claudeAiOauth\":{\"accessToken\":null}}")]
    public void InvalidImportSafe(string json) => Assert.Throws<InvalidDataException>(() => CredentialImporter.Parse(json, ProviderKind.ClaudeOAuth));
    [Fact] public void ClaudeImport() => Assert.Equal("fake", CredentialImporter.Parse("{\"claudeAiOauth\":{\"accessToken\":\"fake\"}}", ProviderKind.ClaudeOAuth).Secret);
    [Fact] public void OversizedImport() => Assert.Throws<InvalidDataException>(() => CredentialImporter.Parse(new string('a', 65537), ProviderKind.Codex));
    [Fact] public void UnsupportedImport() => Assert.Throws<InvalidDataException>(() => CredentialImporter.Parse("{}", ProviderKind.OpenAiApi));
    [Theory]
    [InlineData("abc\r\nx", "a", null)]
    [InlineData("abc", "../a", null)]
    [InlineData("abc", "a", "bad scope")]
    public void Validation(string secret, string account, string? scope) => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", ProviderKind.Codex, account, scope), secret));
    [Fact] public void BudgetValidation() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", ProviderKind.OpenAiApi, MonthlyBudget: 0), "secret"));
}
