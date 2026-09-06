using System.Text.Json;
namespace UsageDock.Core;
public enum ProviderKind { ClaudeOAuth, ClaudeSession, Codex, AnthropicApi, OpenAiApi }
public record ConnectionProfile(Guid Id, string Name, ProviderKind Provider, string? AccountId = null, string? ScopeId = null, decimal? MonthlyBudget = null, bool IsFavorite = true);
public record UsageWindow(string Name, double? UsedPercent, DateTimeOffset? ResetsAt);
public record UsageSnapshot(Guid ConnectionId, DateTimeOffset FetchedAt, IReadOnlyList<UsageWindow> Windows, decimal? CostUsd = null, long? Tokens = null, string? Note = null, CodexResetInventory? ResetCredits = null, string? ResetCreditsError = null);
public record FetchResult(UsageSnapshot? Snapshot, string? Error = null, DateTimeOffset? RetryAfter = null);
public record WorkspaceOption(string Id, string Name);
public record ImportedCredential(string Secret, string? AccountId);
public record AppSettings(string Theme = "Dark", int RefreshSeconds = 300, bool NotificationsEnabled = false, bool StartWithWindows = false, string Language = "auto");
public record StoredState(IReadOnlyList<ConnectionProfile> Connections, AppSettings Settings);
public interface IUsageProvider
{
    Task<FetchResult> FetchAsync(ConnectionProfile profile, string secret, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceOption>> DiscoverAsync(ConnectionProfile profile, string secret, CancellationToken cancellationToken = default);
}
public static class ProfileValidator
{
    public static string? Validate(ConnectionProfile profile, string? secret)
    {
        if (profile is null) return "Invalid connection.";
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 100) return "Enter a connection name (up to 100 characters).";
        if (!Enum.IsDefined(profile.Provider)) return "Unsupported provider.";
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 32768 || secret.Any(char.IsControl)) return "Enter a valid credential.";
        if (profile.Provider == ProviderKind.ClaudeSession && secret.Contains(';')) return "Invalid session credential.";
        if (profile.MonthlyBudget is <= 0) return "Budget must be greater than zero.";
        if (!SafeId(profile.AccountId) || !SafeId(profile.ScopeId)) return "Account and scope identifiers contain invalid characters.";
        if (profile.Provider == ProviderKind.ClaudeSession && string.IsNullOrWhiteSpace(profile.AccountId)) return "Select an organization or enter its ID.";
        return null;
    }
    private static bool SafeId(string? value) => value is null || (value.Length <= 200 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));
}
public static class CredentialImporter
{
    public static ImportedCredential Parse(string json, ProviderKind provider)
    {
        try
        {
            if (json.Length > 65536) throw new InvalidDataException();
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement;
            var key = provider switch { ProviderKind.Codex => "tokens", ProviderKind.ClaudeOAuth => "claudeAiOauth", _ => throw new InvalidDataException() };
            var data = root.GetProperty(key);
            var secret = data.GetProperty(provider == ProviderKind.Codex ? "access_token" : "accessToken").GetString();
            var account = data.TryGetProperty("account_id", out var id) ? id.GetString() : null;
            if (string.IsNullOrWhiteSpace(secret) || secret.Length > 32768 || secret.Any(char.IsControl)) throw new InvalidDataException();
            if (ProfileValidator.Validate(new(Guid.NewGuid(), "Import", provider, account), secret) != null) throw new InvalidDataException();
            return new(secret, account);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException)
        { throw new InvalidDataException("The file is not a supported CLI credential file."); }
    }
}
