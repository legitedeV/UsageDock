using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UsageDock.Core;

namespace UsageDock.App;
public sealed record AccountView(ConnectionProfile Profile, UsageSnapshot? Snapshot = null, string? Error = null, DateTimeOffset? RetryAfter = null, int Generation = 0);
public sealed partial class DockController : IDisposable
{
    private readonly LocalStore? store;
    private readonly IUsageProvider provider;
    private readonly Dictionary<Guid,string> ephemeral = new();
    private readonly CancellationTokenSource shutdown = new();
    private bool refreshing;
    public IReadOnlyList<AccountView> Accounts { get; private set; } = Array.Empty<AccountView>();
    public AppSettings Settings { get; private set; } = new();
    public bool Offline { get; }
    public bool IsRefreshing => refreshing;
    public event Action? Changed;
    public event Action<string>? Alert;
    public DockController(IUsageProvider provider, LocalStore? store, bool offline = false, string? resetJournalDirectory = null)
    {
        this.provider=provider; this.store=store; Offline=offline;
        this.resetJournalDirectory = resetJournalDirectory == null ? store?.DirectoryPath : System.IO.Path.GetFullPath(resetJournalDirectory);
        if(store != null) { var state=store.Load(); Settings=state.Settings; Accounts=state.Connections.Select(p=>new AccountView(p)).ToArray(); }
    }
    public void Seed(IEnumerable<AccountView> values) { Accounts=values.ToArray(); Changed?.Invoke(); }
    public void Save(ConnectionProfile profile, string? secret)
    {
        var previous=Accounts.FirstOrDefault(a=>a.Profile.Id==profile.Id);
        if(previous!=null&&previous.Profile.Provider!=profile.Provider&&string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Provide a new credential when changing provider.");
        var credential=string.IsNullOrWhiteSpace(secret)?ReadSecret(profile.Id):secret;
        var error=ProfileValidator.Validate(profile,credential);
        if(error!=null) throw new InvalidOperationException(error);
        var updated=Accounts.Where(a=>a.Profile.Id!=profile.Id).Append(new AccountView(profile,Generation:(previous?.Generation??0)+1)).ToArray();
        if(store!=null) store.SaveConnection(new StoredState(updated.Select(a=>a.Profile).ToArray(),Settings),profile.Id,secret);
        else if(credential!=null) ephemeral[profile.Id]=credential;
        Accounts=updated; Changed?.Invoke();
    }
    public string? ReadSecret(Guid id)=>store?.ReadSecret(id) ?? ephemeral.GetValueOrDefault(id);
    public void Remove(Guid id)
    {
        var updated=Accounts.Where(a=>a.Profile.Id!=id).ToArray();
        if(store!=null) { store.Save(new StoredState(updated.Select(a=>a.Profile).ToArray(),Settings)); Accounts=updated; Changed?.Invoke(); store.DeleteSecret(id); }
        ephemeral.Remove(id); Accounts=updated; Changed?.Invoke();
    }
    public void Favorite(Guid id)
    {
        var updated=Accounts.Select(a=>a.Profile.Id==id?a with { Profile=a.Profile with { IsFavorite=!a.Profile.IsFavorite }}:a).ToArray();
        store?.Save(new StoredState(updated.Select(a=>a.Profile).ToArray(),Settings)); Accounts=updated; Changed?.Invoke();
    }
    public void SetSettings(AppSettings value)
    {
        value=value with { RefreshSeconds=Math.Max(120,value.RefreshSeconds) };
        store?.Save(new StoredState(Accounts.Select(a=>a.Profile).ToArray(),value)); Settings=value; Changed?.Invoke();
    }
    public IEnumerable<AccountView> Filter(string search,string category)=>Accounts.Where(a=>(category=="All connections" || category=="Favorites"&&a.Profile.IsFavorite || category=="Subscriptions"&&!IsApi(a.Profile.Provider) || category=="API spending"&&IsApi(a.Profile.Provider)) && (a.Profile.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||a.Profile.Provider.ToString().Contains(search,StringComparison.OrdinalIgnoreCase)));
    public static bool IsApi(ProviderKind kind)=>kind is ProviderKind.AnthropicApi or ProviderKind.OpenAiApi;
    public async Task RefreshAsync()
    {
        if(refreshing||Offline) return;
        refreshing=true; Changed?.Invoke();
        try
        {
            using var slots=new SemaphoreSlim(3);
            await Task.WhenAll(Accounts.ToArray().Select(async original=>
            {
                if(original.RetryAfter>DateTimeOffset.UtcNow || IsResetBusy(original.Profile.Id)) return;
                var originalResetEpoch = ResetEpoch(original.Profile.Id);
                await slots.WaitAsync(shutdown.Token);
                FetchResult result;
                try { var active=Accounts.FirstOrDefault(a=>a.Profile.Id==original.Profile.Id); if(active==null||active.Generation!=original.Generation||IsResetBusy(original.Profile.Id)||ResetEpoch(original.Profile.Id)!=originalResetEpoch) return; var secret=ReadSecret(original.Profile.Id); result=secret==null?new FetchResult(null,"Reconnect to provide credentials."):await provider.FetchAsync(original.Profile,secret,shutdown.Token); }
                catch(OperationCanceledException) { return; }
                catch { result=new FetchResult(null,"Unable to read this connection. Reconnect or try again."); }
                finally { slots.Release(); }
                var current=Accounts.FirstOrDefault(a=>a.Profile.Id==original.Profile.Id);
                if(current==null||current.Generation!=original.Generation||ResetEpoch(original.Profile.Id)!=originalResetEpoch) return;
                var next=current with { Snapshot=result.Snapshot??current.Snapshot,Error=result.Error,RetryAfter=result.RetryAfter };
                Accounts=Accounts.Select(a=>a.Profile.Id==current.Profile.Id?next:a).ToArray();
                if(Settings.NotificationsEnabled&&result.Snapshot!=null&&current.Snapshot!=null)
                {
                    foreach(var window in result.Snapshot.Windows)
                    {
                        var before=current.Snapshot.Windows.FirstOrDefault(w=>w.Name==window.Name)?.UsedPercent;
                        if(before.HasValue&&window.UsedPercent.HasValue&&Bucket(before.Value)!=Bucket(window.UsedPercent.Value)) Alert?.Invoke($"{current.Profile.Name}: {window.Name} is {window.UsedPercent:0}% used.");
                    }
                }
                if(Settings.NotificationsEnabled&&result.Snapshot?.CostUsd is { } cost&&current.Snapshot?.CostUsd is { } oldCost&&current.Profile.MonthlyBudget is >0)
                { var budget=current.Profile.MonthlyBudget.Value; if(Bucket((double)(oldCost/budget*100))!=Bucket((double)(cost/budget*100))) Alert?.Invoke($"{current.Profile.Name}: local budget is {cost/budget*100:0}% used."); }
                Changed?.Invoke();
            }));
        }
        catch(OperationCanceledException) { }
        finally { refreshing=false; Changed?.Invoke(); }
    }
    private static int Bucket(double percent)=>percent>=100?3:percent>=95?2:percent>=80?1:0;
    public void Dispose() { shutdown.Cancel(); shutdown.Dispose(); (provider as IDisposable)?.Dispose(); }
}
