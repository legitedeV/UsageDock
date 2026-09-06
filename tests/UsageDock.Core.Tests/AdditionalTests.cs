using System.Net;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class AdditionalTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => Task.FromResult(f(r)); }
    [Fact]
    public async Task CredentialsAreRequestLocal()
    {
        var seen = new List<string>(); using var client = new HttpClient(new Handler(r => { seen.Add(r.Headers.Authorization!.Parameter!); return new(HttpStatusCode.OK) { Content = new StringContent("{\"five_hour\":{\"utilization\":0}}") }; })); using var p = new ProviderService(client);
        await Task.WhenAll(p.FetchAsync(new(Guid.NewGuid(), "A", ProviderKind.ClaudeOAuth), "alpha"), p.FetchAsync(new(Guid.NewGuid(), "B", ProviderKind.ClaudeOAuth), "beta"));
        Assert.Equal(new[] { "alpha", "beta" }, seen); Assert.Null(client.DefaultRequestHeaders.Authorization);
    }
    [Fact] public async Task UnsupportedDiscoveryEmpty() { using var p = new ProviderService(new HttpClient(new Handler(_ => throw new Exception()))); Assert.Empty(await p.DiscoverAsync(new(Guid.NewGuid(), "A", ProviderKind.OpenAiApi), "s")); }
    [Fact] public void MissingOrganizationRejected() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", ProviderKind.ClaudeSession), "s"));
    [Fact] public void EmptyIdRejected() => Assert.NotNull(ProfileValidator.Validate(new(Guid.Empty, "A", ProviderKind.Codex), "s"));
    [Fact] public void UnknownProviderRejected() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", (ProviderKind)999), "s"));
    [Fact] public void CookieDelimiterRejected() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", ProviderKind.ClaudeSession, "org"), "abc;other=bad"));
    [Fact] public void NullSecretRejected() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "A", ProviderKind.Codex), null));
    [Fact] public void StoreRejectsEmptyGuid() => Assert.Throws<ArgumentException>(() => new LocalStore(Path.GetTempPath()).ReadSecret(Guid.Empty));
    [Fact] public void StoreRejectsInvalidSecret() => Assert.Throws<InvalidDataException>(() => new LocalStore(Path.GetTempPath()).WriteSecret(Guid.NewGuid(), ""));
    [Fact] public void StoreRejectsBadSettings() => Assert.Throws<InvalidDataException>(() => new LocalStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).Save(new(Array.Empty<ConnectionProfile>(), new(RefreshSeconds: 1))));
}
