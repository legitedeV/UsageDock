using System.Net;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class ReviewRegressionTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => Task.FromResult(f(r)); }
    private static ConnectionProfile Profile(ProviderKind kind = ProviderKind.ClaudeOAuth) => new(Guid.NewGuid(), "Test", kind, ScopeId: "default");
    [Fact] public void NotificationsAreOptIn() => Assert.False(new AppSettings().NotificationsEnabled);
    [Fact]
    public async Task RateLimitHasFallbackCooldown()
    {
        using var s = new ProviderService(new HttpClient(new Handler(_ => new(HttpStatusCode.TooManyRequests))));
        Assert.True((await s.FetchAsync(Profile(), "secret")).RetryAfter > DateTimeOffset.UtcNow);
    }
    [Fact]
    public async Task DefaultWorkspaceUsageFilteredLocally()
    {
        using var s = new ProviderService(new HttpClient(new Handler(r =>
        {
            Assert.DoesNotContain("workspace_ids", r.RequestUri!.OriginalString);
            Assert.Contains("group_by%5B%5D=workspace_id", r.RequestUri.OriginalString);
            var rows = r.RequestUri.AbsolutePath.EndsWith("cost_report") ? "{\"workspace_id\":null,\"amount\":\"100\",\"currency\":\"USD\"}" : "{\"workspace_id\":null,\"uncached_input_tokens\":1,\"cache_read_input_tokens\":2,\"output_tokens\":3,\"cache_creation\":{\"ephemeral_1h_input_tokens\":4,\"ephemeral_5m_input_tokens\":5}},{\"workspace_id\":\"other\"}";
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"results\":[" + rows + "]}]}") };
        })));
        var snapshot = (await s.FetchAsync(Profile(ProviderKind.AnthropicApi), "secret")).Snapshot!;
        Assert.Equal(1m, snapshot.CostUsd); Assert.Equal(15, snapshot.Tokens);
    }
    private sealed class SlowStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(1000, cancellationToken); return 0; }
    }
    [Fact]
    public async Task BodyReadHasTotalTimeout()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new SlowStream()) })) { Timeout = TimeSpan.FromMilliseconds(30) };
        using var service = new ProviderService(http); var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await service.FetchAsync(Profile(), "secret"); Assert.NotNull(result.Error); Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(500));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupedWorkspaceMustBePresent(bool usageFailure)
    {
        using var service = new ProviderService(new HttpClient(new Handler(request =>
        {
            var isCost = request.RequestUri!.AbsolutePath.EndsWith("cost_report");
            var row = isCost && usageFailure ? "{\"workspace_id\":null,\"amount\":\"100\",\"currency\":\"USD\"}" : "{}";
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"results\":[" + row + "]}]}") };
        })));
        var result = await service.FetchAsync(Profile(ProviderKind.AnthropicApi), "secret");
        if (usageFailure) { Assert.Equal(1m, result.Snapshot!.CostUsd); Assert.Null(result.Snapshot.Tokens); Assert.Contains("unavailable", result.Snapshot.Note); }
        else { Assert.Null(result.Snapshot); Assert.NotNull(result.Error); }
    }}
