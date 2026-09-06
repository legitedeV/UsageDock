using System.Net;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class TransactionTests
{
    [Fact]
    public void FailedCommitPreservesIdentityAndCredential()
    {
        var path = Path.Combine(Path.GetTempPath(), "UsageDock-test-" + Guid.NewGuid()); var store = new LocalStore(path); var id = Guid.NewGuid();
        try
        {
            var old = new StoredState(new[] { new ConnectionProfile(id, "Old", ProviderKind.Codex) }, new());
            store.SaveConnection(old, id, "old-secret");
            using (var locked = new FileStream(Path.Combine(path, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<InvalidDataException>(() => store.SaveConnection(new(new[] { new ConnectionProfile(id, "New", ProviderKind.Codex) }, new()), id, "new-secret"));
            Assert.Equal("Old", store.Load().Connections[0].Name); Assert.Equal("old-secret", store.ReadSecret(id));
            store.SaveConnection(old with { Connections = new[] { old.Connections[0] with { Name = "Edited" } } }, id, null);
            Assert.Equal("old-secret", store.ReadSecret(id));
            store.SaveConnection(old, id, "reconnected"); Assert.Equal("reconnected", store.ReadSecret(id));
            store.DeleteSecret(id); Assert.Null(store.ReadSecret(id));
            Assert.Empty(Directory.EnumerateFiles(path,"*.secret"));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    private sealed class Handler(Func<HttpRequestMessage, string> response) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request)) }); }
    [Fact]
    public async Task ObjectModelScopeHasReadableLabel()
    {
        using var provider = new ProviderService(new HttpClient(new Handler(_ => "{\"limits\":[{\"kind\":\"weekly_scoped\",\"percent\":2,\"scope\":{\"model\":{\"id\":\"m\",\"display_name\":\"Sonnet\"}}}]}")));
        Assert.Equal("Sonnet - weekly_scoped", (await provider.FetchAsync(new(Guid.NewGuid(), "A", ProviderKind.ClaudeOAuth), "s")).Snapshot!.Windows[0].Name);
    }
    [Fact]
    public async Task ThousandsNumericFormatRejected()
    {
        using var provider = new ProviderService(new HttpClient(new Handler(_ => "{\"five_hour\":{\"utilization\":\"1,5\"}}")));
        Assert.NotNull((await provider.FetchAsync(new(Guid.NewGuid(), "A", ProviderKind.ClaudeOAuth), "s")).Error);
    }
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0.5, 0.5)]
    public async Task InvalidTokenFieldsCannotCancel(double input, double output)
    {
        using var provider = new ProviderService(new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath.EndsWith("costs") ? "{\"data\":[]}" : "{\"data\":[{\"results\":[{\"input_tokens\":" + input.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"output_tokens\":" + output.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}]}")));
        Assert.Null((await provider.FetchAsync(new(Guid.NewGuid(), "A", ProviderKind.OpenAiApi), "s")).Snapshot!.Tokens);
    }
}
