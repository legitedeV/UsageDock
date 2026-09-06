using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UsageDock.Core;
namespace UsageDock.App;

public sealed record CodexResetAttempt(string RequestId, string? CreditId);
public sealed partial class DockController
{
    private readonly object resetGate = new();
    private readonly HashSet<Guid> resetsBusy = new();
    private readonly Dictionary<Guid, int> resetEpochs = new();
    private readonly Dictionary<Guid, DateTimeOffset> resetCooldowns = new();
    private string? resetJournalDirectory;
    public bool IsResetBusy(Guid accountId) { lock (resetGate) return resetsBusy.Contains(accountId); }
    private int ResetEpoch(Guid accountId) { lock (resetGate) return resetEpochs.GetValueOrDefault(accountId); }
    private int AdvanceResetEpoch(Guid accountId)
    { lock (resetGate) { var next = resetEpochs.GetValueOrDefault(accountId) + 1; resetEpochs[accountId] = next; return next; } }
    public CodexResetAttempt? GetPendingReset(Guid accountId)
    {
        var entry = ReadResetJournal(accountId);
        if (entry == null) return null;
        var account = Accounts.FirstOrDefault(a => a.Profile.Id == accountId);
        var secret = account == null ? null : ReadSecret(accountId);
        if (account == null || secret == null || entry.Binding != ResetBinding(account.Profile, secret))
            throw new InvalidDataException("Nierozstrzygnięta próba dotyczy innego powiązania konta.");
        return new(entry.RequestId, entry.CreditId);
    }
    public Task<CodexResetResult> ConsumeResetAsync(Guid accountId, string? creditId)
        => ConsumeResetCoreAsync(accountId, null, creditId);
    public Task<CodexResetResult> ConsumeResetAsync(Guid accountId, string requestId, string? creditId)
        => ConsumeResetCoreAsync(accountId, requestId, creditId);
    private async Task<CodexResetResult> ConsumeResetCoreAsync(Guid accountId, string? requestId, string? creditId)
    {
        if (Offline || provider is not ICodexResetService service || accountId == Guid.Empty)
            return new(CodexResetOutcome.Denied, "Reset jest niedostępny dla tego połączenia.");
        lock (resetGate)
        {
            if (resetCooldowns.TryGetValue(accountId, out var retryAt) && retryAt > DateTimeOffset.UtcNow)
                return new(CodexResetOutcome.RateLimited, "Zaczekaj przed ponownym sprawdzeniem resetu.", retryAt);
            if (!resetsBusy.Add(accountId)) return new(CodexResetOutcome.Denied, "Trwa już obsługa resetu tego konta.");
        }
        try
        {
            Changed?.Invoke();
            var original = Accounts.FirstOrDefault(a => a.Profile.Id == accountId);
            var secret = original == null ? null : ReadSecret(accountId);
            if (original?.Profile.Provider != ProviderKind.Codex || secret == null
                || ProfileValidator.Validate(original.Profile, secret) != null)
                return new(CodexResetOutcome.InvalidRequest, "Sprawdź powiązanie konta Codex.");
            if (resetJournalDirectory == null) return new(CodexResetOutcome.Denied, "Brak magazynu do bezpiecznego zapisania próby resetu.");
            Directory.CreateDirectory(resetJournalDirectory);
            // Cross-controller/process serialization complements the UI busy state.
            using var fileLock = new FileStream(Path.Combine(resetJournalDirectory, "reset-attempt-" + accountId.ToString("N") + ".lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var binding = ResetBinding(original.Profile, secret);
            var pending = ReadResetJournal(accountId);
            if (pending != null && (pending.Binding != binding || pending.CreditId != creditId
                || requestId != null && pending.RequestId != requestId))
                return new(CodexResetOutcome.Denied, "Najpierw rozstrzygnij wcześniejszą próbę dla tego samego konta i resetu.");
            if (pending == null && creditId == null) return new(CodexResetOutcome.InvalidRequest, "Wybierz konkretny dostępny reset.");
            var id = pending?.RequestId ?? requestId ?? Guid.NewGuid().ToString();
            if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty || !ValidCreditId(creditId))
                return new(CodexResetOutcome.InvalidRequest, "Nieprawidłowe dane próby resetu.");
            var inventory = await service.GetResetCreditsAsync(original.Profile, secret, shutdown.Token);
            if (!SameResetAccount(original, binding)) return new(CodexResetOutcome.Denied, "Konto zmieniono podczas sprawdzania. Żądanie nie zostało wysłane.");
            if (pending == null && (inventory.AvailableCount <= 0 || inventory.Credits == null
                || !inventory.Credits.Any(c => (creditId == null || c.Id == creditId) && c.CanRedeem(DateTimeOffset.UtcNow))))
                return new(CodexResetOutcome.NoCredit, "Wybrany reset nie jest już dostępny albo nie znamy jego terminu ważności.");
            // An uncertain retry is allowed to reach the idempotent endpoint even if the credit is now redeemed.
            var entry = pending ?? new ResetJournal(1, accountId, id, creditId, binding);
            WriteResetJournal(entry);
            if (!SameResetAccount(original, binding)) return new(CodexResetOutcome.Denied, "Powiązanie konta zmieniło się. Nie wysłano żądania.");
            var epoch = AdvanceResetEpoch(accountId);
            var result = await service.ConsumeAsync(original.Profile, secret, id, creditId, shutdown.Token);
            if (result.Outcome is CodexResetOutcome.Reset or CodexResetOutcome.AlreadyRedeemed or CodexResetOutcome.NothingToReset or CodexResetOutcome.NoCredit)
                File.Delete(ResetJournalPath(accountId));
            if (result.Outcome == CodexResetOutcome.RateLimited && result.RetryAfter is { } retryAfter)
                lock (resetGate) resetCooldowns[accountId] = retryAfter;
            await RefreshResetAccountAsync(original, secret, epoch);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or OperationCanceledException or System.Net.Http.HttpRequestException or CryptographicException)
        { return new(CodexResetOutcome.Uncertain, "Nie można potwierdzić wyniku lub bezpiecznie zapisać próby. Odśwież stan; zachowaj tę samą próbę."); }
        finally
        {
            lock (resetGate) resetsBusy.Remove(accountId);
            Changed?.Invoke();
        }
    }
    private bool SameResetAccount(AccountView original, string binding)
    {
        var current = Accounts.FirstOrDefault(a => a.Profile.Id == original.Profile.Id);
        return current != null && current.Generation == original.Generation
            && ReadSecret(current.Profile.Id) is { } secret && ResetBinding(current.Profile, secret) == binding;
    }
    private async Task RefreshResetAccountAsync(AccountView original, string secret, int epoch)
    {
        var current = Accounts.FirstOrDefault(a => a.Profile.Id == original.Profile.Id);
        if (current == null || current.Generation != original.Generation || ResetEpoch(original.Profile.Id) != epoch) return;
        FetchResult result;
        try { result = await provider.FetchAsync(current.Profile, secret, shutdown.Token); }
        catch { result = new(null, "Odczyt po próbie resetu nie powiódł się. Wyświetlane dane mogą być nieaktualne."); }
        current = Accounts.FirstOrDefault(a => a.Profile.Id == original.Profile.Id);
        if (current == null || current.Generation != original.Generation || ResetEpoch(original.Profile.Id) != epoch) return;
        var next = current with { Snapshot = result.Snapshot ?? current.Snapshot, Error = result.Error, RetryAfter = result.RetryAfter };
        Accounts = Accounts.Select(a => a.Profile.Id == next.Profile.Id ? next : a).ToArray();
        Changed?.Invoke();
    }
    private static bool ValidCreditId(string? creditId) => creditId == null
        || !string.IsNullOrWhiteSpace(creditId) && creditId.Length <= 512 && !creditId.Any(char.IsControl);
    private static string ResetBinding(ConnectionProfile profile, string secret)
    {
        // Explicit account identity survives token renewal; only unidentified accounts bind to the token.
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { profile.Id, profile.Provider, profile.AccountId, profile.ScopeId, CredentialFallback = string.IsNullOrEmpty(profile.AccountId) ? secret : null }));
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private sealed record ResetJournal(int Version, Guid AccountId, string RequestId, string? CreditId, string Binding);
    private string ResetJournalPath(Guid accountId)
    {
        if (accountId == Guid.Empty || resetJournalDirectory == null) throw new InvalidDataException("Brak magazynu prób resetu.");
        return Path.Combine(resetJournalDirectory, "reset-attempt-" + accountId.ToString("N") + ".json");
    }
    private ResetJournal? ReadResetJournal(Guid accountId)
    {
        if (resetJournalDirectory == null) return null;
        try
        {
            var path = ResetJournalPath(accountId);
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 8192) throw new InvalidDataException();
            var entry = JsonSerializer.Deserialize<ResetJournal>(File.ReadAllText(path), new JsonSerializerOptions { MaxDepth = 8 });
            if (entry == null || entry.Version != 1 || entry.AccountId != accountId
                || !Guid.TryParse(entry.RequestId, out var id) || id == Guid.Empty || !ValidCreditId(entry.CreditId)
                || entry.Binding == null || entry.Binding.Length != 64 || !entry.Binding.All(Uri.IsHexDigit)) throw new InvalidDataException();
            return entry;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { throw new InvalidDataException("Nie można odczytać zapisu wcześniejszej próby resetu."); }
    }
    private void WriteResetJournal(ResetJournal entry)
    {
        var path = ResetJournalPath(entry.AccountId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, entry);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
