using System.Globalization;
using System.Text.Json;
namespace UsageDock.Core;

public sealed record CodexResetCredit(string Id, string ResetType, string Status,
    DateTimeOffset? GrantedAt, DateTimeOffset? ExpiresAt, bool ExpiryKnown,
    string? Title = null, string? Description = null)
{
    public bool CanRedeem(DateTimeOffset now) => !string.IsNullOrWhiteSpace(Id)
        && ResetType == "codex_rate_limits" && Status == "available" && ExpiryKnown
        && (ExpiresAt == null || ExpiresAt > now);
}
public sealed record CodexResetInventory(long AvailableCount, IReadOnlyList<CodexResetCredit>? Credits);
public enum CodexResetOutcome { Reset, AlreadyRedeemed, NothingToReset, NoCredit, Uncertain, Denied, InvalidRequest, RateLimited }
public sealed record CodexResetResult(CodexResetOutcome Outcome, string? Error = null,
    DateTimeOffset? RetryAfter = null, long WindowsReset = 0);
public interface ICodexResetService
{
    Task<CodexResetInventory> GetResetCreditsAsync(ConnectionProfile profile, string secret, CancellationToken cancellationToken = default);
    Task<CodexResetResult> ConsumeAsync(ConnectionProfile profile, string secret, string requestId,
        string? creditId = null, CancellationToken cancellationToken = default);
}

/// <summary>Source-observed Codex reset inventory. Missing details or expiry never imply availability.</summary>
public static class CodexResetParser
{
    public static CodexResetInventory Parse(JsonElement root)
    {
        try
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("available_count", out var count)
                || !count.TryGetInt64(out var available) || available < 0) throw new InvalidDataException();
            if (!root.TryGetProperty("credits", out var rows) || rows.ValueKind == JsonValueKind.Null)
                return new(available, null);
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 10000) throw new InvalidDataException();
            var credits = new List<CodexResetCredit>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.EnumerateArray())
            {
                var id = RequiredText(row, "id", 512);
                if (!ids.Add(id)) throw new InvalidDataException();
                var expiryKnown = row.TryGetProperty("expires_at", out _);
                credits.Add(new(id, RequiredText(row, "reset_type", 128), RequiredText(row, "status", 128),
                    Date(row, "granted_at"), Date(row, "expires_at"), expiryKnown,
                    OptionalText(row, "title", 1024), OptionalText(row, "description", 4096)));
            }
            // The provider may cap details; available_count is not derived from this list.
            return new(available, credits.AsReadOnly());
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { throw new InvalidDataException("Nieobsługiwany format zapisanych resetów."); }
    }
    private static string RequiredText(JsonElement row, string key, int maximum)
    {
        var value = OptionalText(row, key, maximum);
        return !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl) ? value : throw new InvalidDataException();
    }
    private static string? OptionalText(JsonElement row, string key, int maximum)
    {
        if (!row.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind != JsonValueKind.String) throw new InvalidDataException();
        var value = field.GetString()!;
        if (value.Length > maximum || value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new InvalidDataException();
        return value;
    }
    private static DateTimeOffset? Date(JsonElement row, string key)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(value.GetString()!, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$")
            || !DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)) throw new InvalidDataException();
        return parsed;
    }
}
