using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace UsageDock.Core;
public sealed partial class ProviderService : IUsageProvider, ICodexResetService, IDisposable
{
    private readonly HttpClient client;
    private readonly bool owns;
    public ProviderService() : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) }) { owns = true; }
    public ProviderService(HttpClient client) { this.client = client; }
    public void Dispose() { if (owns) client.Dispose(); }
    public async Task<FetchResult> FetchAsync(ConnectionProfile profile, string secret, CancellationToken cancellationToken = default)
    {
        var invalid = ProfileValidator.Validate(profile, secret); if (invalid != null) return new(null, invalid);
        try
        {
            if (profile.Provider is ProviderKind.AnthropicApi or ProviderKind.OpenAiApi) return await ApiAsync(profile, secret, cancellationToken);
            var url = profile.Provider switch
            {
                ProviderKind.ClaudeOAuth => "https://api.anthropic.com/api/oauth/usage",
                ProviderKind.ClaudeSession => $"https://claude.ai/api/organizations/{Uri.EscapeDataString(profile.AccountId!)}/usage",
                _ => "https://chatgpt.com/backend-api/wham/usage"
            };
            using var doc = await GetAsync(url, profile, secret, cancellationToken);
            var snapshot = new UsageSnapshot(profile.Id, DateTimeOffset.UtcNow, UsageParser.Subscription(doc.RootElement, profile.Provider), Note: profile.Provider == ProviderKind.Codex ? "Experimental Codex limits; ChatGPT conversation quota is unavailable." : "Experimental subscription endpoint; reconnect when the session expires.");
            if (profile.Provider == ProviderKind.Codex)
            {
                try { snapshot = snapshot with { ResetCredits = await GetResetCreditsAsync(profile, secret, cancellationToken) }; }
                catch (InvalidDataException) { snapshot = snapshot with { ResetCredits = ResetSummary(doc.RootElement), ResetCreditsError = "Zapisane resety są chwilowo niedostępne. Limity odczytano poprawnie." }; }
            }
            return new(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProviderException ex) { return new(null, ex.Message, ex.RetryAfter); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or InvalidOperationException or OverflowException or ArgumentOutOfRangeException)
        { return new(null, ex is HttpRequestException or OperationCanceledException ? "Connection failed or timed out. Try again." : "The provider returned unsupported or incomplete data."); }
    }
    private async Task<FetchResult> ApiAsync(ConnectionProfile profile, string secret, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var open = profile.Provider == ProviderKind.OpenAiApi;
        var prefix = open ? "https://api.openai.com/v1/organization/" : "https://api.anthropic.com/v1/organizations/";
        var dates = open ? $"start_time={start.ToUnixTimeSeconds()}&end_time={now.ToUnixTimeSeconds()}" : $"starting_at={Uri.EscapeDataString(start.ToString("O"))}&ending_at={Uri.EscapeDataString(now.ToString("O"))}";
        var defaultWorkspace = !open && profile.ScopeId == "default";
        var scope = string.IsNullOrEmpty(profile.ScopeId) || defaultWorkspace ? "" : $"&{(open ? "project_ids" : "workspace_ids")}%5B%5D={Uri.EscapeDataString(profile.ScopeId)}";
        var costs = prefix + (open ? "costs" : "cost_report") + "?" + dates + "&bucket_width=1d&limit=31" + (open ? scope : "&group_by%5B%5D=workspace_id");
        var costRows = await PagesAsync(costs, profile, secret, token);
        if (!open) ValidateGroupedWorkspaces(costRows);
        decimal cost = 0;
        foreach (var row in costRows)
            if (open || string.IsNullOrEmpty(profile.ScopeId) || UsageParser.Text(row, "workspace_id") == profile.ScopeId || (profile.ScopeId == "default" && row.TryGetProperty("workspace_id", out var workspace) && workspace.ValueKind == JsonValueKind.Null)) cost += UsageParser.Cost(row, profile.Provider);
        long? tokens = null;
        var note = open ? "Month to date (UTC). Tokens: completions only." : "Month to date (UTC). Tokens: messages input, cache and output.";
        try
        {
            var usage = prefix + (open ? "usage/completions" : "usage_report/messages") + "?" + dates + "&bucket_width=1d&limit=31" + (defaultWorkspace ? "&group_by%5B%5D=workspace_id" : scope);
            var rows = await PagesAsync(usage, profile, secret, token);
            if (defaultWorkspace) ValidateGroupedWorkspaces(rows);
            long sum = 0; foreach (var row in rows) if (!defaultWorkspace || (row.TryGetProperty("workspace_id", out var workspaceId) && workspaceId.ValueKind == JsonValueKind.Null)) sum = checked(sum + UsageParser.Tokens(row, profile.Provider)); tokens = sum;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or InvalidOperationException or OverflowException)
        { note += " Token usage unavailable; costs were retrieved successfully."; }
        return new(new(profile.Id, DateTimeOffset.UtcNow, Array.Empty<UsageWindow>(), cost, tokens, note));
    }
    private static void ValidateGroupedWorkspaces(IReadOnlyList<JsonElement> rows)
    {
        foreach (var row in rows)
            if (!row.TryGetProperty("workspace_id", out var workspace) || workspace.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                throw new InvalidDataException("Unsupported workspace grouping.");
    }
    private async Task<IReadOnlyList<JsonElement>> PagesAsync(string url, ConnectionProfile profile, string secret, CancellationToken token)
    {
        var rows = new List<JsonElement>(); var visited = new HashSet<string>(StringComparer.Ordinal); string? cursor = null;
        for (var page = 0; page < 100; page++)
        {
            using var doc = await GetAsync(url + (cursor == null ? "" : "&page=" + Uri.EscapeDataString(cursor)), profile, secret, token);
            var root = doc.RootElement; var data = UsageParser.Required(root, "data");
            if (data.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
            foreach (var bucket in data.EnumerateArray())
            {
                var results = UsageParser.Required(bucket, "results"); if (results.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
                foreach (var row in results.EnumerateArray()) { if (rows.Count >= 100000) throw new InvalidDataException(); rows.Add(row.Clone()); }
            }
            var next = UsageParser.Text(root, "next_page");
            var hasMore = root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True;
            if (string.IsNullOrEmpty(next)) { if (hasMore) throw new InvalidDataException(); return rows.AsReadOnly(); }
            if (!visited.Add(next)) throw new InvalidDataException(); cursor = next;
        }
        throw new InvalidDataException();
    }
    public async Task<IReadOnlyList<WorkspaceOption>> DiscoverAsync(ConnectionProfile profile, string secret, CancellationToken cancellationToken = default)
    {
        if (profile.Provider is not (ProviderKind.ClaudeSession or ProviderKind.Codex)) return Array.Empty<WorkspaceOption>();
        if (ProfileValidator.Validate(profile with { AccountId = profile.AccountId ?? "discovery" }, secret) != null) throw new InvalidDataException("Invalid connection credentials.");
        try
        {
            using var doc = await GetAsync(profile.Provider == ProviderKind.Codex ? "https://chatgpt.com/backend-api/accounts" : "https://claude.ai/api/organizations", profile, secret, cancellationToken);
            var items = profile.Provider == ProviderKind.Codex ? UsageParser.Required(doc.RootElement, "items") : doc.RootElement;
            if (items.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
            return Array.AsReadOnly(items.EnumerateArray().Select(item => new WorkspaceOption(UsageParser.Text(item, profile.Provider == ProviderKind.Codex ? "id" : "uuid") ?? throw new InvalidDataException(), UsageParser.Text(item, "name") ?? "Unnamed organization")).Take(1000).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is ProviderException or HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or InvalidOperationException) { throw new InvalidDataException("Could not discover organizations. Check credentials or enter an ID manually."); }
    }
    private async Task<JsonDocument> GetAsync(string url, ConnectionProfile profile, string secret, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(client.Timeout > TimeSpan.Zero && client.Timeout < TimeSpan.FromSeconds(20) ? client.Timeout : TimeSpan.FromSeconds(20));
        token = deadline.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (profile.Provider == ProviderKind.ClaudeSession) request.Headers.Add("Cookie", "sessionKey=" + secret);
        else if (profile.Provider == ProviderKind.AnthropicApi) request.Headers.Add("x-api-key", secret);
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        if (profile.Provider is ProviderKind.ClaudeOAuth) request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        if (profile.Provider == ProviderKind.AnthropicApi) request.Headers.Add("anthropic-version", "2023-06-01");
        if (!string.IsNullOrEmpty(profile.AccountId))
        {
            if (profile.Provider == ProviderKind.Codex) request.Headers.Add("ChatGPT-Account-Id", profile.AccountId);
            if (profile.Provider == ProviderKind.OpenAiApi) request.Headers.Add("OpenAI-Organization", profile.AccountId);
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            var retry = response.Headers.RetryAfter;
            var at = retry?.Date ?? (retry?.Delta is TimeSpan delay ? DateTimeOffset.UtcNow + delay : null);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && (at is null || at < DateTimeOffset.UtcNow)) at = DateTimeOffset.UtcNow.AddMinutes(1);
            throw new ProviderException(response.StatusCode switch { HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Access denied. Reconnect and verify account permissions.", HttpStatusCode.TooManyRequests => "Rate limited. Wait before refreshing.", _ => "The provider request failed. Try again later." }, at);
        }
        const int maximum = 4 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidDataException();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes, token)) > 0) { if (buffer.Length + read > maximum) throw new InvalidDataException(); buffer.Write(bytes, 0, read); }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    }
    private sealed class ProviderException(string message, DateTimeOffset? retryAfter) : Exception(message) { public DateTimeOffset? RetryAfter { get; } = retryAfter; }
}
