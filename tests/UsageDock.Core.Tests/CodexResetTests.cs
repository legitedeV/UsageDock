using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class CodexResetTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-06T10:00:00Z");
    private const string RequestId = "de977b99-75ac-453f-8b82-2fce91d8300c";
    private static ConnectionProfile Profile(ProviderKind kind=ProviderKind.Codex)=>new(Guid.Parse("5395c99c-a20d-4d48-b253-27bc052d764d"),"Fixture",kind,"fixture-account");
    private static CodexResetInventory Parse(string value){using var doc=JsonDocument.Parse(value);return CodexResetParser.Parse(doc.RootElement);}
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}
    private static HttpResponseMessage Json(string body,HttpStatusCode code=HttpStatusCode.OK)=>new(code){Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json")};
    private static ProviderService Service(Func<HttpRequestMessage,HttpResponseMessage> send)=>new(new HttpClient(new Handler((r,_)=>Task.FromResult(send(r)))));
    [Fact] public void AvailableCountIsAuthoritativeEvenWhenCreditListIsCapped()
    {
        var result=Parse("""{"available_count":12,"credits":[{"id":"credit-1","reset_type":"codex_rate_limits","status":"available","granted_at":"2026-09-01T00:00:00Z","expires_at":null}]}""");
        Assert.Equal(12,result.AvailableCount);Assert.Single(result.Credits!);
    }
    [Theory][InlineData("{}")] [InlineData("{\"available_count\":null}")] [InlineData("{\"available_count\":-1}")] [InlineData("{\"available_count\":1.5}")] [InlineData("{\"available_count\":\"2\"}")][InlineData("{\"available_count\":9223372036854775808}")]
    public void InvalidOrMissingCountCannotBecomeZero(string body)=>Assert.Throws<InvalidDataException>(()=>Parse(body));
    [Theory][InlineData("{\"available_count\":3}")][InlineData("{\"available_count\":3,\"credits\":null}")]
    public void MissingCreditDetailsRemainUnknown(string body)=>Assert.Null(Parse(body).Credits);
    [Fact] public void ExplicitEmptyInventoryIsKnownZero(){var result=Parse("{\"available_count\":0,\"credits\":[]}");Assert.Equal(0,result.AvailableCount);Assert.Empty(result.Credits!);}
    [Fact] public void ExpiryNullIsKnownNoExpiry()
    {
        var credit=Assert.Single(Parse("""{"available_count":1,"credits":[{"id":"credit-1","reset_type":"codex_rate_limits","status":"available","granted_at":"2026-09-01T00:00:00Z","expires_at":null}]}""").Credits!);
        Assert.True(credit.ExpiryKnown);Assert.Null(credit.ExpiresAt);Assert.True(credit.CanRedeem(Now));
    }
    [Fact] public void MissingExpiryCannotBeAssumedUnlimited()
    {
        var credit=Assert.Single(Parse("""{"available_count":1,"credits":[{"id":"credit-1","reset_type":"codex_rate_limits","status":"available","granted_at":"2026-09-01T00:00:00Z"}]}""").Credits!);
        Assert.False(credit.ExpiryKnown);Assert.False(credit.CanRedeem(Now));
    }
    [Theory][InlineData("available",true)][InlineData("redeeming",false)][InlineData("redeemed",false)][InlineData("unknown",false)][InlineData("future_status",false)]
    public void StatusControlsRedemption(string status,bool expected)=>Assert.Equal(expected,new CodexResetCredit("id","codex_rate_limits",status,Now.AddDays(-1),null,true).CanRedeem(Now));
    [Fact] public void ExpiredAtBoundaryCannotRedeem()=>Assert.False(new CodexResetCredit("id","codex_rate_limits","available",Now.AddDays(-1),Now,true).CanRedeem(Now));
    [Fact] public void FutureExpiryCanRedeem()=>Assert.True(new CodexResetCredit("id","codex_rate_limits","available",Now.AddDays(-1),Now.AddTicks(1),true).CanRedeem(Now));
    [Theory][InlineData("granted_at")][InlineData("expires_at")]
    public void InvalidTimestampIsRejected(string key)=>Assert.Throws<InvalidDataException>(()=>Parse("{\"available_count\":1,\"credits\":[{\"id\":\"id\",\"reset_type\":\"codex_rate_limits\",\"status\":\"available\",\""+key+"\":\"not-a-date\"}]}"));
    [Fact] public async Task InventoryGetUsesFixedRouteAndCredentialHeaders()
    {
        using var service=Service(r=>{Assert.Equal(HttpMethod.Get,r.Method);Assert.Equal("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits",r.RequestUri!.AbsoluteUri);Assert.Equal("Bearer fixture-secret",r.Headers.Authorization!.ToString());Assert.Equal("fixture-account",r.Headers.GetValues("ChatGPT-Account-Id").Single());Assert.Null(r.Content);return Json("{\"available_count\":2,\"credits\":[]}");});
        Assert.Equal(2,(await service.GetResetCreditsAsync(Profile(),"fixture-secret")).AvailableCount);
    }
    [Fact] public async Task UsageSurvivesInventoryFailureWithoutPretendingZero()
    {
        using var service=Service(r=>r.RequestUri!.AbsolutePath.EndsWith("/usage")?Json("""{"rate_limit":{"primary_window":{"used_percent":42,"limit_window_seconds":18000}}}"""):Json("sensitive-response",HttpStatusCode.Unauthorized));
        var result=await service.FetchAsync(Profile(),"fixture-secret");Assert.Null(result.Error);Assert.Equal(42,Assert.Single(result.Snapshot!.Windows).UsedPercent);Assert.Null(result.Snapshot.ResetCredits);Assert.False(string.IsNullOrWhiteSpace(result.Snapshot.ResetCreditsError));Assert.DoesNotContain("sensitive-response",result.Snapshot.ResetCreditsError);
    }
    [Fact] public async Task UsageAttachesInventoryWhenAvailable()
    {
        using var service=Service(r=>r.RequestUri!.AbsolutePath.EndsWith("/usage")?Json("""{"rate_limit":{"primary_window":{"used_percent":42,"limit_window_seconds":18000}}}"""):Json("{\"available_count\":8,\"credits\":[]}"));
        var result=await service.FetchAsync(Profile(),"fixture-secret");Assert.Equal(8,result.Snapshot!.ResetCredits!.AvailableCount);Assert.Null(result.Snapshot.ResetCreditsError);
    }
    [Fact] public async Task ConsumeUsesCallerRequestIdAndDoesNotAutomaticallyRetry()
    {
        var requests=new List<string>();using var client=new HttpClient(new Handler(async(r,_)=>{Assert.Equal(HttpMethod.Post,r.Method);Assert.Equal("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits/consume",r.RequestUri!.AbsoluteUri);Assert.Equal("fixture-account",r.Headers.GetValues("ChatGPT-Account-Id").Single());Assert.Equal("fixture-secret",r.Headers.Authorization!.Parameter);requests.Add(await r.Content!.ReadAsStringAsync());return Json("{\"code\":\"reset\",\"windows_reset\":2}");}));using var service=new ProviderService(client);
        var result=await service.ConsumeAsync(Profile(),"fixture-secret",RequestId,"credit-1");Assert.Equal(CodexResetOutcome.Reset,result.Outcome);Assert.Equal(2,result.WindowsReset);using var body=JsonDocument.Parse(Assert.Single(requests));Assert.Equal(RequestId,body.RootElement.GetProperty("redeem_request_id").GetString());Assert.Equal("credit-1",body.RootElement.GetProperty("credit_id").GetString());
    }
    [Theory][InlineData("already_redeemed",CodexResetOutcome.AlreadyRedeemed)][InlineData("nothing_to_reset",CodexResetOutcome.NothingToReset)][InlineData("no_credit",CodexResetOutcome.NoCredit)][InlineData("future_code",CodexResetOutcome.Uncertain)]
    public async Task ConsumeHandlesServiceOutcomes(string code,CodexResetOutcome expected){using var service=Service(_=>Json("{\"code\":\""+code+"\"}"));var result=await service.ConsumeAsync(Profile(),"fixture-secret",RequestId);Assert.Equal(expected,result.Outcome);Assert.Equal(0,result.WindowsReset);}
    [Fact] public async Task ExplicitRetryReusesSameIdempotencyKey()
    {
        var keys=new List<string>();using var client=new HttpClient(new Handler(async(r,_)=>{using var body=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());keys.Add(body.RootElement.GetProperty("redeem_request_id").GetString()!);if(keys.Count==1)throw new HttpRequestException("fixture-secret");return Json("{\"code\":\"already_redeemed\"}");}));using var service=new ProviderService(client);
        Assert.Equal(CodexResetOutcome.Uncertain,(await service.ConsumeAsync(Profile(),"fixture-secret",RequestId,"credit-1")).Outcome);Assert.Single(keys);Assert.Equal(CodexResetOutcome.AlreadyRedeemed,(await service.ConsumeAsync(Profile(),"fixture-secret",RequestId,"credit-1")).Outcome);Assert.Equal(new[]{RequestId,RequestId},keys);
    }
    [Theory][InlineData(401,CodexResetOutcome.Denied)][InlineData(403,CodexResetOutcome.Denied)][InlineData(429,CodexResetOutcome.RateLimited)][InlineData(500,CodexResetOutcome.Uncertain)]
    public async Task ConsumeErrorsAreSafeAndNeverRetried(int status,CodexResetOutcome expected)
    {
        var calls=0;using var service=Service(_=>{calls++;var response=Json("fixture-secret private-response",(HttpStatusCode)status);if(status==429)response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));return response;});var result=await service.ConsumeAsync(Profile(),"fixture-secret",RequestId);Assert.Equal(expected,result.Outcome);Assert.Equal(1,calls);Assert.DoesNotContain("fixture-secret",result.Error??"");if(status==429)Assert.NotNull(result.RetryAfter);
    }
    [Fact] public async Task ConsumeTimeoutHasUnknownOutcomeAndNoRetry()
    {
        var calls=0;using var client=new HttpClient(new Handler((_,_)=>{calls++;throw new TaskCanceledException("fixture-secret");}));using var service=new ProviderService(client);var result=await service.ConsumeAsync(Profile(),"fixture-secret",RequestId);Assert.Equal(CodexResetOutcome.Uncertain,result.Outcome);Assert.Equal(1,calls);Assert.DoesNotContain("fixture-secret",result.Error??"");
    }
    [Theory][InlineData(ProviderKind.ClaudeOAuth,RequestId)][InlineData(ProviderKind.Codex,"not-a-uuid")]
    public async Task InvalidConsumeNeverSendsHttp(ProviderKind kind,string id){using var service=Service(_=>throw new Xunit.Sdk.XunitException("HTTP must not be sent"));Assert.Equal(CodexResetOutcome.InvalidRequest,(await service.ConsumeAsync(Profile(kind),"fixture-secret",id)).Outcome);}
    [Fact] public void UnknownResetTypeCannotBeRedeemed()=>Assert.False(new CodexResetCredit("id","future_type","available",Now,null,true).CanRedeem(Now));
    [Theory][InlineData("{}")][InlineData("{\"code\":\"reset\",\"windows_reset\":-1}")][InlineData("{\"code\":\"reset\",\"windows_reset\":1.5}")][InlineData("not json")]
    public async Task UnsupportedConsumeResponseIsUncertain(string body){using var service=Service(_=>Json(body));Assert.Equal(CodexResetOutcome.Uncertain,(await service.ConsumeAsync(Profile(),"fixture-secret",RequestId)).Outcome);}
    [Theory][InlineData(401)][InlineData(429)][InlineData(500)]
    public async Task InventoryHttpFailureIsSafe(int code){using var service=Service(_=>Json("fixture-secret",(HttpStatusCode)code));var error=await Assert.ThrowsAsync<InvalidDataException>(()=>service.GetResetCreditsAsync(Profile(),"fixture-secret"));Assert.DoesNotContain("fixture-secret",error.Message);}
    [Fact] public async Task InventoryCancellationPropagates(){using var cancellation=new CancellationTokenSource();cancellation.Cancel();using var service=Service(_=>throw new OperationCanceledException(cancellation.Token));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.GetResetCreditsAsync(Profile(),"fixture-secret",cancellation.Token));}
    [Fact] public async Task ConsumeCallerCancellationAfterDispatchIsUncertain()
    {
        using var cancellation=new CancellationTokenSource();var calls=0;using var client=new HttpClient(new Handler((_,_)=>{calls++;cancellation.Cancel();throw new OperationCanceledException(cancellation.Token);}));using var service=new ProviderService(client);Assert.Equal(CodexResetOutcome.Uncertain,(await service.ConsumeAsync(Profile(),"fixture-secret",RequestId,cancellationToken:cancellation.Token)).Outcome);Assert.Equal(1,calls);
    }
    [Fact] public async Task InventoryBodyLimitIsEnforced(){using var service=Service(_=>Json(new string('x',4_194_305)));await Assert.ThrowsAsync<InvalidDataException>(()=>service.GetResetCreditsAsync(Profile(),"fixture-secret"));}    [Theory][InlineData("2026-09-12")][InlineData("2026-09-12T12:30:00")]
    public void TimestampWithoutExplicitZoneIsRejected(string timestamp)=>Assert.Throws<InvalidDataException>(()=>Parse("{\"available_count\":1,\"credits\":[{\"id\":\"id\",\"reset_type\":\"codex_rate_limits\",\"status\":\"available\",\"expires_at\":\""+timestamp+"\"}]}"));
    [Fact] public async Task InventoryIoFailureDoesNotDiscardUsage()
    {
        using var service=Service(r=>r.RequestUri!.AbsolutePath.EndsWith("/usage")?Json("""{"rate_limit":{"primary_window":{"used_percent":42,"limit_window_seconds":18000}}}"""):throw new IOException("fixture-secret"));var result=await service.FetchAsync(Profile(),"fixture-secret");Assert.NotNull(result.Snapshot);Assert.Null(result.Snapshot.ResetCredits);Assert.DoesNotContain("fixture-secret",result.Snapshot.ResetCreditsError??"");
    }}
