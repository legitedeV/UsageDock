using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace UsageDock.Core;

public sealed partial class ProviderService
{
    private const string ResetInventoryUrl = "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits";
    public async Task<CodexResetInventory> GetResetCreditsAsync(ConnectionProfile profile, string secret,
        CancellationToken cancellationToken = default)
    {
        if (!ValidCodexProfile(profile, secret)) throw new InvalidDataException("Nieprawidłowe połączenie Codex.");
        try
        {
            using var document = await GetAsync(ResetInventoryUrl, profile, secret, cancellationToken);
            return CodexResetParser.Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException or OperationCanceledException
            or JsonException or InvalidDataException or InvalidOperationException or OverflowException or IOException)
        { throw new InvalidDataException("Nie udało się odczytać zapisanych resetów."); }
    }
    public async Task<CodexResetResult> ConsumeAsync(ConnectionProfile profile, string secret, string requestId,
        string? creditId = null, CancellationToken cancellationToken = default)
    {
        if (!ValidCodexProfile(profile, secret) || !Guid.TryParse(requestId, out var attempt) || attempt == Guid.Empty
            || (creditId != null && (string.IsNullOrWhiteSpace(creditId) || creditId.Length > 512 || creditId.Any(char.IsControl))))
            return new(CodexResetOutcome.InvalidRequest, "Nieprawidłowe żądanie wykorzystania resetu.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(client.Timeout > TimeSpan.Zero && client.Timeout < TimeSpan.FromSeconds(20)
                ? client.Timeout : TimeSpan.FromSeconds(20));
            using var request = ResetRequest(profile, secret, requestId, creditId);
            // Intentionally one POST. The caller retains the same request ID for an uncertain retry.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return ResetHttpError(response);
            using var document = await ReadResetResponseAsync(response.Content, deadline.Token);
            return ParseResetResult(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
            or InvalidDataException or InvalidOperationException or OverflowException or IOException)
        { return new(CodexResetOutcome.Uncertain, "Wynik niepotwierdzony. Odśwież stan; ponów wyłącznie tę samą próbę."); }
    }
    private static bool ValidCodexProfile(ConnectionProfile profile, string secret) => profile != null
        && profile.Provider == ProviderKind.Codex && ProfileValidator.Validate(profile, secret) == null;
    private static HttpRequestMessage ResetRequest(ConnectionProfile profile, string secret, string requestId, string? creditId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ResetInventoryUrl + "/consume");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (!string.IsNullOrEmpty(profile.AccountId)) request.Headers.Add("ChatGPT-Account-Id", profile.AccountId);
        var payload = new Dictionary<string, string> { ["redeem_request_id"] = requestId };
        if (creditId != null) payload["credit_id"] = creditId;
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return request;
    }
    private static CodexResetResult ResetHttpError(HttpResponseMessage response)
    {
        var retry = response.Headers.RetryAfter;
        var retryAt = retry?.Date ?? (retry?.Delta is TimeSpan delay ? DateTimeOffset.UtcNow + delay : null);
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new(CodexResetOutcome.Denied,
                "Brak dostępu. Połącz konto Codex ponownie i sprawdź uprawnienia."),
            HttpStatusCode.TooManyRequests => new(CodexResetOutcome.RateLimited, "Za dużo żądań. Zaczekaj przed ponowieniem.",
                retryAt > DateTimeOffset.UtcNow ? retryAt : DateTimeOffset.UtcNow.AddMinutes(1)),
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound => new(CodexResetOutcome.InvalidRequest,
                "Dostawca odrzucił żądanie resetu. Odśwież stan konta."),
            _ => new(CodexResetOutcome.Uncertain, "Wynik niepotwierdzony. Odśwież stan; ponów wyłącznie tę samą próbę.")
        };
    }
    private static async Task<JsonDocument> ReadResetResponseAsync(HttpContent content, CancellationToken token)
    {
        const int maximum = 4 * 1024 * 1024;
        if (content.Headers.ContentLength > maximum) throw new InvalidDataException();
        await using var stream = await content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes, token)) > 0)
        {
            if (buffer.Length + read > maximum) throw new InvalidDataException();
            buffer.Write(bytes, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    }
    private static CodexResetResult ParseResetResult(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        long windows = 0;
        if (root.TryGetProperty("windows_reset", out var count) && (!count.TryGetInt64(out windows) || windows < 0))
            throw new InvalidDataException();
        var code = root.TryGetProperty("code", out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        var outcome = code switch
        {
            "reset" => CodexResetOutcome.Reset,
            "already_redeemed" => CodexResetOutcome.AlreadyRedeemed,
            "nothing_to_reset" => CodexResetOutcome.NothingToReset,
            "no_credit" => CodexResetOutcome.NoCredit,
            _ => CodexResetOutcome.Uncertain
        };
        return new(outcome, outcome == CodexResetOutcome.Uncertain ? "Dostawca zwrócił nieznany wynik. Odśwież stan konta." : null,
            WindowsReset: windows);
    }
    private static CodexResetInventory? ResetSummary(JsonElement root)
    {
        if (root.TryGetProperty("rate_limit_reset_credits", out var summary) && summary.ValueKind == JsonValueKind.Object
            && summary.TryGetProperty("available_count", out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var count) && count >= 0) return new(count, null);
        return null;
    }
}
