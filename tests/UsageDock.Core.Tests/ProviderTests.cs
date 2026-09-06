using System.Net;
using System.Net.Http.Headers;
using System.Text;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class ProviderTests
{
    private static ConnectionProfile Profile(ProviderKind kind = ProviderKind.ClaudeOAuth, string? scope = null) => new(Guid.NewGuid(), "Test", kind, "account", scope);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => send(r, t); }
    private static ProviderService Service(Func<HttpRequestMessage, HttpResponseMessage> send) => new(new HttpClient(new Handler((r, t) => Task.FromResult(send(r)))));
    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    [Theory]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("42.5", 42.5)]
    public async Task ClaudeWindows(string percent, double expected)
    {
        using var service = Service(r => { Assert.Equal("Bearer secret", r.Headers.Authorization!.ToString()); Assert.Contains("oauth-2025-04-20", r.Headers.GetValues("anthropic-beta")); return Json("{\"five_hour\":{\"utilization\":" + percent + ",\"resets_at\":\"2026-09-05T12:00:00Z\"}}"); });
        var result = await service.FetchAsync(Profile(), "secret"); Assert.Null(result.Error); Assert.Equal(expected, Assert.Single(result.Snapshot!.Windows).UsedPercent); Assert.Equal(TimeSpan.Zero, result.Snapshot.Windows[0].ResetsAt!.Value.Offset);
    }
    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("\"NaN\"")]
    [InlineData("true")]
    public async Task RejectBadPercentage(string n) { using var s = Service(_ => Json("{\"five_hour\":{\"utilization\":" + n + "}}")); Assert.NotNull((await s.FetchAsync(Profile(), "s")).Error); }
    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"five_hour\":{\"resets_at\":\"oops\"}}")]
    public async Task RejectUnknown(string json) { using var s = Service(_ => Json(json)); Assert.Null((await s.FetchAsync(Profile(), "s")).Snapshot); }
    [Fact] public async Task MissingPercentStaysNull() { using var s = Service(_ => Json("{\"five_hour\":{}}")); Assert.Null((await s.FetchAsync(Profile(), "s")).Snapshot!.Windows[0].UsedPercent); }
    [Fact]
    public async Task CodexDurationsAndAdditional()
    {
        using var s = Service(r => { Assert.Equal("account", r.Headers.GetValues("ChatGPT-Account-Id").Single()); return Json("{\"rate_limit\":{\"primary_window\":{\"limit_window_seconds\":7200,\"used_percent\":3,\"reset_at\":1770000000}},\"additional_rate_limits\":[{\"limit_name\":\"Review\",\"rate_limit\":{\"secondary_window\":{\"limit_window_seconds\":604800,\"used_percent\":8}}}]}"); });
        var snap = (await s.FetchAsync(Profile(ProviderKind.Codex), "s")).Snapshot!; Assert.Equal(2, snap.Windows.Count); Assert.Contains("2 hours", snap.Windows[0].Name); Assert.Contains("7 days", snap.Windows[1].Name); Assert.Contains("ChatGPT", snap.Note);
    }
    [Fact]
    public async Task ScopedClaude()
    {
        using var s = Service(r => { Assert.Equal("sessionKey=abc", r.Headers.GetValues("Cookie").Single()); Assert.Null(r.Headers.Authorization); return Json("{\"limits\":[{\"type\":\"weekly_scoped\",\"percent\":20,\"scope\":{\"model\":\"Sonnet\"}},{\"is_active\":false,\"percent\":10}]}"); });
        Assert.Equal("Sonnet", Assert.Single((await s.FetchAsync(Profile(ProviderKind.ClaudeSession), "abc")).Snapshot!.Windows).Name);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    [InlineData(302)]
    public async Task SafeErrors(int status) { using var s = Service(_ => Json("private token", (HttpStatusCode)status)); var r = await s.FetchAsync(Profile(), "secret"); Assert.Null(r.Snapshot); Assert.DoesNotContain("private", r.Error); }
    [Fact] public async Task RetryAfter() { using var s = Service(_ => { var r = Json("", HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return r; }); Assert.NotNull((await s.FetchAsync(Profile(), "s")).RetryAfter); }
    [Fact] public async Task CancellationPropagates() { using var c = new CancellationTokenSource(); c.Cancel(); using var s = new ProviderService(new HttpClient(new Handler((r, t) => Task.FromCanceled<HttpResponseMessage>(t)))); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.FetchAsync(Profile(), "s", c.Token)); }
    [Fact] public async Task NetworkSafe() { using var s = new ProviderService(new HttpClient(new Handler((r, t) => throw new HttpRequestException("secret")))); var result = await s.FetchAsync(Profile(), "s"); Assert.DoesNotContain("secret", result.Error); }
    [Fact] public async Task ResponseBounded() { using var s = Service(_ => Json(new string('x', 4 * 1024 * 1024 + 1))); Assert.NotNull((await s.FetchAsync(Profile(), "s")).Error); }
    [Fact]
    public async Task AnthropicCostCentsFilteringAndTokens()
    {
        using var s = Service(r =>
        {
            Assert.Equal("admin", r.Headers.GetValues("x-api-key").Single()); Assert.Null(r.Headers.Authorization);
            if (r.RequestUri!.AbsolutePath.EndsWith("cost_report")) { Assert.Contains("group_by%5B%5D=workspace_id", r.RequestUri.OriginalString); Assert.DoesNotContain("workspace_ids", r.RequestUri.OriginalString); return Json("{\"data\":[{\"results\":[{\"workspace_id\":\"w1\",\"currency\":\"USD\",\"amount\":\"123.45\"},{\"workspace_id\":\"w2\",\"currency\":\"USD\",\"amount\":\"900\"}]}]}"); }
            Assert.Contains("workspace_ids%5B%5D=w1", r.RequestUri.OriginalString); return Json("{\"data\":[{\"results\":[{\"uncached_input_tokens\":1,\"cache_read_input_tokens\":2,\"output_tokens\":3,\"cache_creation\":{\"ephemeral_1h_input_tokens\":4,\"ephemeral_5m_input_tokens\":5}}]}]}");
        }); var snap = (await s.FetchAsync(Profile(ProviderKind.AnthropicApi, "w1"), "admin")).Snapshot!; Assert.Equal(1.2345m, snap.CostUsd); Assert.Equal(15, snap.Tokens);
    }
    [Fact]
    public async Task OpenAiPaginationAndHeaders()
    {
        var costCalls = 0;
        using var s = Service(r =>
        {
            Assert.Equal("account", r.Headers.GetValues("OpenAI-Organization").Single()); Assert.Contains("project_ids%5B%5D=p", r.RequestUri!.OriginalString);
            if (r.RequestUri.AbsolutePath.EndsWith("costs")) { costCalls++; return Json(costCalls == 1 ? "{\"data\":[{\"results\":[{\"amount\":{\"value\":1.5,\"currency\":\"usd\"}}]}],\"next_page\":\"two\",\"has_more\":true}" : "{\"data\":[{\"results\":[{\"amount\":{\"value\":2,\"currency\":\"usd\"}}]}],\"next_page\":null}"); }
            return Json("{\"data\":[{\"results\":[{\"input_tokens\":10,\"output_tokens\":20}]}]}");
        }); var snap = (await s.FetchAsync(Profile(ProviderKind.OpenAiApi, "p"), "s")).Snapshot!; Assert.Equal(3.5m, snap.CostUsd); Assert.Equal(30, snap.Tokens); Assert.Equal(2, costCalls);
    }
    [Theory]
    [InlineData("{\"data\":[],\"next_page\":\"same\"}")]
    [InlineData("{\"data\":[],\"has_more\":true}")]
    [InlineData("{}")]
    [InlineData("{\"data\":[{}]}")]
    public async Task InvalidCostPagination(string body) { using var s = Service(_ => Json(body)); Assert.NotNull((await s.FetchAsync(Profile(ProviderKind.OpenAiApi), "s")).Error); }
    [Theory]
    [InlineData("eur")]
    [InlineData("")]
    public async Task UnsupportedCurrency(string currency) { using var s = Service(_ => Json("{\"data\":[{\"results\":[{\"amount\":{\"value\":1,\"currency\":\"" + currency + "\"}}]}]}")); Assert.Null((await s.FetchAsync(Profile(ProviderKind.OpenAiApi), "s")).Snapshot); }
    [Fact] public async Task CostSurvivesOptionalUsageFailure() { using var s = Service(r => r.RequestUri!.AbsolutePath.EndsWith("costs") ? Json("{\"data\":[]}") : Json("private", HttpStatusCode.Forbidden)); var snap = (await s.FetchAsync(Profile(ProviderKind.OpenAiApi), "s")).Snapshot!; Assert.Equal(0, snap.CostUsd); Assert.Null(snap.Tokens); Assert.Contains("unavailable", snap.Note); }
    [Theory]
    [InlineData(ProviderKind.Codex, "{\"items\":[{\"id\":\"a\",\"name\":\"A\"}]}")]
    [InlineData(ProviderKind.ClaudeSession, "[{\"uuid\":\"a\",\"name\":\"A\"}]")]
    public async Task Discovery(ProviderKind kind, string json) { using var s = Service(_ => Json(json)); Assert.Equal(new WorkspaceOption("a", "A"), Assert.Single(await s.DiscoverAsync(Profile(kind), "s"))); }
    [Fact] public async Task DiscoverySafeError() { using var s = Service(_ => Json("bad")); var ex = await Assert.ThrowsAsync<InvalidDataException>(() => s.DiscoverAsync(Profile(ProviderKind.Codex), "s")); Assert.DoesNotContain("bad", ex.Message); }
    [Fact] public async Task InvalidProfileDoesNotCallHttp() { using var s = Service(_ => throw new Exception()); Assert.NotNull((await s.FetchAsync(Profile() with { Name = "" }, "s")).Error); }
}
