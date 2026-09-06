using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UsageDock.Core;
namespace UsageDock.App;
internal sealed class OfflineProvider : IUsageProvider
{
    public Task<FetchResult> FetchAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)=>Task.FromResult(new FetchResult(new UsageSnapshot(profile.Id,DateTimeOffset.UtcNow,new[]{new UsageWindow("Test window",25,DateTimeOffset.UtcNow.AddHours(2))})));
    public Task<IReadOnlyList<WorkspaceOption>> DiscoverAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<WorkspaceOption>>(Array.Empty<WorkspaceOption>());
}
internal static class DemoData
{
    public static AccountView[] Accounts()
    {
        var now=DateTimeOffset.UtcNow;
        var claude=new ConnectionProfile(Guid.NewGuid(),"Prywatne",ProviderKind.ClaudeOAuth);
        var codex=new ConnectionProfile(Guid.NewGuid(),"Studio",ProviderKind.Codex,"demo-account");
        var api=new ConnectionProfile(Guid.NewGuid(),"Produkcja",ProviderKind.OpenAiApi,ScopeId:"project-demo",MonthlyBudget:100,IsFavorite:true);
        var anthropic=new ConnectionProfile(Guid.NewGuid(),"Research",ProviderKind.AnthropicApi,MonthlyBudget:60,IsFavorite:true);
        return new[]{new AccountView(claude,new UsageSnapshot(claude.Id,now,new[]{new UsageWindow("5-hour window",38,now.AddHours(2)),new UsageWindow("Weekly",64,now.AddDays(3))})),new AccountView(codex,new UsageSnapshot(codex.Id,now.AddMinutes(-4),new[]{new UsageWindow("5-hour window",84,now.AddMinutes(55)),new UsageWindow("Weekly",47,now.AddDays(3))},ResetCredits:new CodexResetInventory(2,new[]{new CodexResetCredit("demo-expiring","codex_rate_limits","available",now.AddDays(-1),now.AddDays(6).AddHours(3),true),new CodexResetCredit("demo-unlimited","codex_rate_limits","available",now.AddDays(-1),null,true)}))),new AccountView(api,new UsageSnapshot(api.Id,now,Array.Empty<UsageWindow>(),24.86m,248320,"Completions tokens · UTC month to date")),new AccountView(anthropic,new UsageSnapshot(anthropic.Id,now,Array.Empty<UsageWindow>(),8.42m,104112,"Messages tokens · UTC month to date"))};
    }
}
internal static class Verification
{
    public static async Task CaptureAsync(string directory,DockController controller,Dashboard dashboard)
    {
        Directory.CreateDirectory(directory);
        var baseline=controller.Accounts.ToArray();
        foreach(var light in new[]{false,true})
        {
            if(Ui.Light!=light)Click(dashboard,"Theme.Toggle.Main");dashboard.Width=1128;dashboard.Height=756;dashboard.SelectTab("Konta");await Task.Delay(60);
            Capture(dashboard,Path.Combine(directory,light?"dashboard-light.png":"dashboard-dark.png"));
            if(!light){Capture(dashboard,Path.Combine(directory,"main-client.png"));var widget=new Widget(controller,dashboard);widget.Show();await Task.Delay(60);Capture(widget,Path.Combine(directory,"widget-dark.png"));Capture(widget,Path.Combine(directory,"widget-client.png"));widget.Close();var editor=new ConnectionEditor(controller,null);editor.Show();await Task.Delay(60);Capture(editor,Path.Combine(directory,"connection-editor.png"));editor.Close();}
            if(light){var widget=new Widget(controller,dashboard);widget.Show();await Task.Delay(60);Capture(widget,Path.Combine(directory,"widget-light.png"));widget.Close();}
            controller.Seed(baseline.Select((a,i)=>a with {Snapshot=a.Snapshot! with {FetchedAt=a.Snapshot!.FetchedAt.AddSeconds(light?2:1)}}));
            controller.Seed(controller.Accounts.Select((a,i)=>i==1?a with {Error="Synthetic demo read failure"}:a));
            foreach(var pair in new[]{("Statystyki","statistics"),("Historia","history"),("Ustawienia","settings")}){dashboard.SelectTab(pair.Item1);await Task.Delay(60);Capture(dashboard,Path.Combine(directory,pair.Item2+(light?"-light.png":"-dark.png")));}
        }
        if(Ui.Light)Click(dashboard,"Theme.Toggle.Main");dashboard.Width=960;dashboard.Height=580;
        foreach(var pair in new[]{("Konta","dashboard"),("Statystyki","statistics"),("Historia","history"),("Ustawienia","settings")}){dashboard.SelectTab(pair.Item1);await Task.Delay(60);Capture(dashboard,Path.Combine(directory,pair.Item2+"-minimum.png"));}
        dashboard.Width=1128;dashboard.Height=756;controller.Seed(baseline.Select(a=>a with {Snapshot=null,Error="Synthetic demo read failure"}));dashboard.SelectTab("Statystyki");await Task.Delay(60);Capture(dashboard,Path.Combine(directory,"statistics-unavailable.png"));
        controller.Seed(Array.Empty<AccountView>());dashboard.SelectTab("Statystyki");await Task.Delay(60);Capture(dashboard,Path.Combine(directory,"statistics-empty.png"));
        dashboard.SelectTab("Historia");dashboard.UpdateLayout();Click(dashboard,"History.Clear");await Task.Delay(60);Capture(dashboard,Path.Combine(directory,"history-empty.png"));
        dashboard.SelectTab("Ustawienia");dashboard.UpdateLayout();Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text="30";await Task.Delay(60);Capture(dashboard,Path.Combine(directory,"settings-invalid.png"));
        controller.Seed(baseline.Select((a,i)=>a with {Profile=a.Profile with {Name=new[]{"Claude — prywatne konto robocze","Codex — Workspace produkcyjny","OpenAI — obszar zespołu aplikacji","Anthropic — badania i rozwój"}[i]}}));dashboard.SelectTab("Konta");await Task.Delay(60);Capture(dashboard,Path.Combine(directory,"accounts-longnames.png"));
        var resetAccount=baseline.First(a=>a.Profile.Provider==ProviderKind.Codex);controller.Seed(baseline);var resetWindow=dashboard.CreateResetCreditsWindow(resetAccount.Profile.Id)!;resetWindow.Show();await Task.Delay(60);Capture(resetWindow,Path.Combine(directory,"reset-inventory.png"));resetWindow.Close();Click(dashboard,"Theme.Toggle.Main");resetWindow=dashboard.CreateResetCreditsWindow(resetAccount.Profile.Id)!;resetWindow.Show();await Task.Delay(60);Capture(resetWindow,Path.Combine(directory,"reset-inventory-light.png"));resetWindow.Close();Click(dashboard,"Theme.Toggle.Main");controller.Seed(baseline.Select(a=>a.Profile.Id==resetAccount.Profile.Id?a with {Snapshot=a.Snapshot! with {ResetCredits=null,ResetCreditsError="Synthetic unavailable inventory"}}:a));resetWindow=dashboard.CreateResetCreditsWindow(resetAccount.Profile.Id)!;resetWindow.Show();await Task.Delay(60);Capture(resetWindow,Path.Combine(directory,"reset-inventory-unavailable.png"));resetWindow.Close();
        controller.Seed(baseline);
    }
    private static void Capture(Window window,string path)
    {
        window.UpdateLayout();var content=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var drawing=background.RenderOpen())drawing.DrawRectangle(window.Background,null,new Rect(0,0,content.ActualWidth,content.ActualHeight));bitmap.Render(background);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
    public static async Task<bool> SmokeAsync(string output,DockController controller,Dashboard dashboard)
    {
        var report=new List<string>();
        try
        {
            dashboard.UpdateLayout();foreach(var tab in new[]{"Statystyki","Historia","Ustawienia","Konta"}){var button=Find<System.Windows.Controls.Button>(dashboard,"Nav."+tab);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));dashboard.UpdateLayout();Assert(Descendants<System.Windows.Controls.TextBlock>(dashboard).Any(t=>tab=="Konta"?t.Text=="Nazwa":t.Text.StartsWith(tab)),"tab "+tab,report);}
            CheckThemeControls(controller,dashboard,report);
            CheckResetPresentation(controller,dashboard,report);
            CheckTabs(controller,dashboard,report);
            var search=Descendants<System.Windows.Controls.TextBox>(dashboard).First();search.Text="Produkcja";dashboard.UpdateLayout();Assert(Descendants<System.Windows.Controls.TextBlock>(dashboard).Any(t=>t.Text=="Produkcja")&&!Descendants<System.Windows.Controls.TextBlock>(dashboard).Any(t=>t.Text=="Prywatne"),"search UI event",report);search.Text="";dashboard.UpdateLayout();
            var pin=Descendants<System.Windows.Controls.Button>(dashboard).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Usuń z widgetu");var favorites=controller.Accounts.Count(a=>a.Profile.IsFavorite);pin.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Assert(controller.Accounts.Count(a=>a.Profile.IsFavorite)==favorites-1,"favorite UI event",report);
            Assert(Dashboard.WindowLabel("Codex - 5 hours")=="Limit 5 h"&&Dashboard.WindowLabel("Codex - 7 days")=="Tygodniowy"&&Dashboard.WindowLabel("Unknown window")=="Unknown window","real Codex window names preserve meaning",report);
            var before=controller.Accounts.Count;var id=Guid.NewGuid();var profile=new ConnectionProfile(id,"Smoke account",ProviderKind.ClaudeOAuth);
            controller.Save(profile,"fixture-credential");Assert(controller.Accounts.Count==before+1,"add",report);dashboard.UpdateLayout();Assert(Descendants<System.Windows.Controls.TextBlock>(dashboard).Any(t=>t.Text.StartsWith($"{before+1} ")&&t.Text.EndsWith(" · DEMO")),"footer count updates after add",report);
            controller.Save(profile with{Name="Renamed smoke"},null);Assert(controller.Accounts.Single(a=>a.Profile.Id==id).Profile.Name=="Renamed smoke","edit",report);
            Assert(controller.Filter("Renamed","All connections").Count()==1,"search",report);
            controller.Favorite(id);Assert(!controller.Accounts.Single(a=>a.Profile.Id==id).Profile.IsFavorite,"favorite",report);
            controller.SetSettings(controller.Settings with {Theme="Light"});Ui.Theme(true);dashboard.Build();Assert(Ui.Light,"theme",report);
            var widget=new Widget(controller,dashboard);widget.Show();Assert(widget.IsVisible&&widget.Topmost,"widget",report);widget.UpdateLayout();var widgetHeader=Descendants<System.Windows.Controls.Canvas>(widget).First(c=>c.Height==70);Assert(ReferenceEquals(widgetHeader.InputHitTest(new Point(165,60)),widgetHeader),"widget header empty area hit target",report);var widgetTitle=Descendants<System.Windows.Controls.TextBlock>(widgetHeader).First(t=>t.Text=="UsageDock");var widgetClose=Descendants<System.Windows.Controls.Button>(widgetHeader).First();Assert(Design.CanDrag(widgetTitle,widgetHeader)&&!Design.CanDrag(widgetClose,widgetHeader),"header drag accepts title excludes buttons",report);widget.Close();
            var editor=new ConnectionEditor(controller,profile);editor.Show();Assert(editor.IsVisible,"editor renders",report);editor.Close();
            controller.Remove(id);Assert(controller.Accounts.Count==before&&controller.ReadSecret(id)==null,"remove credential",report);
            using var liveFake=new DockController(new OfflineProvider(),null);liveFake.Save(profile,"fixture-credential");await liveFake.RefreshAsync();Assert(liveFake.Accounts.Single().Snapshot?.Windows.Single().UsedPercent==25,"provider refresh integration",report);
            var rejected=false;try{liveFake.Save(profile with {Provider=ProviderKind.OpenAiApi},null);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"provider change requires credential",report);
            await CheckHistoryRefresh(report);
            await CheckResetBridge(report,Path.GetDirectoryName(output)!);
            await CheckRaces(report);
            report.Add($"PASS: {report.Count} checks; offline synthetic data; no real account or storage access.");File.WriteAllLines(output,report);return true;
        }
        catch(Exception error){report.Add("FAIL: "+error.Message);File.WriteAllLines(output,report);return false;}
    }
    private static T Find<T>(DependencyObject root,string id) where T:FrameworkElement => Descendants<T>(root).SingleOrDefault(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)==id) ?? throw new InvalidOperationException("Missing control: "+id);
    private static void Click(DependencyObject root,string id){var button=Find<System.Windows.Controls.Primitives.ButtonBase>(root,id);if(!button.IsEnabled)throw new InvalidOperationException("Disabled action: "+id);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));}
    private static string Text(DependencyObject root)=>string.Join(" | ",Descendants<System.Windows.Controls.TextBlock>(root).Select(t=>t.Text));
    private static void CheckResetPresentation(DockController controller,Dashboard dashboard,List<string> report)
    {
        var baseline=controller.Accounts.ToArray();var now=DateTimeOffset.Parse("2026-09-06T10:00:00Z");
        var profile=new ConnectionProfile(Guid.Parse("8c3b90c2-6607-4721-b2b0-2cbd6bf0a642"),"Reset fixture",ProviderKind.Codex,"fixture-account");
        var snapshot=new UsageSnapshot(profile.Id,now,new[]{new UsageWindow("5-hour window",42,now.AddDays(6).AddHours(3))});
        var account=new AccountView(profile,snapshot);controller.Seed(new[]{account});dashboard.SelectTab("Konta");dashboard.UpdateLayout();dashboard.RefreshClock(now);
        Assert(Text(dashboard).Contains("za 6 dni 3 godz.")&&!Text(dashboard).Contains("Za 7 dni"),"countdown keeps remaining days and hours",report);
        var relative=Descendants<System.Windows.Controls.TextBlock>(dashboard).First(t=>t.Text=="za 6 dni 3 godz.");var changes=0;Action changed=()=>changes++;controller.Changed+=changed;
        try{dashboard.RefreshClock(now.AddHours(1));Assert(relative.Text=="za 6 dni 2 godz."&&changes==0,"clock ticks update existing labels without data refresh",report);}finally{controller.Changed-=changed;}
        Assert(relative.ToolTip?.ToString()?.Contains(UsageTime.Absolute(snapshot.Windows[0].ResetsAt,TimeZoneInfo.Local))==true,"reset tooltip includes exact date and timezone",report);
        Assert(Find<System.Windows.Controls.TextBlock>(dashboard,"Resets.Count."+profile.Id).Text.Contains("brak danych"),"unknown banked reset count is not zero",report);
        var credits=new[]{new CodexResetCredit("available","codex_rate_limits","available",now,null,true),new CodexResetCredit("unknown-expiry","codex_rate_limits","available",now,null,false),new CodexResetCredit("redeemed","codex_rate_limits","redeemed",now,null,true),new CodexResetCredit("expired","codex_rate_limits","available",now,DateTimeOffset.MinValue,true)};
        var expiring=credits[0] with {ExpiresAt=now.AddDays(6),ExpiryKnown=true};var confirmation=Dashboard.BuildResetConfirmation(account,expiring,false);
        Assert(confirmation.Contains(profile.Name)&&confirmation.Contains(UsageTime.Absolute(expiring.ExpiresAt,TimeZoneInfo.Local)),"reset confirmation identifies account and exact expiry",report);
        Assert(Dashboard.BuildResetConfirmation(account,credits[0],false).Contains("bez terminu wygaśnięcia")&&Dashboard.BuildResetConfirmation(account,credits[1],false).Contains("termin ważności nieznany"),"confirmation distinguishes unlimited from unknown expiry",report);
        Assert(Dashboard.BuildResetConfirmation(account,expiring,true).Contains("ten sam identyfikator operacji"),"retry confirmation explains original idempotency identity",report);
        Assert(Dashboard.ResetConfirmationDefault==MessageBoxResult.No,"reset confirmation defaults to no consumption",report);
        var inventory=new CodexResetInventory(8,credits);account=account with {Snapshot=snapshot with {ResetCredits=inventory}};
        var window=new Window();Ui.Window(window,"Synthetic reset inventory",660,600);window.Content=new System.Windows.Controls.ScrollViewer{Content=dashboard.ResetInventoryPanel(account),VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto};window.Show();window.UpdateLayout();
        try
        {
            Assert(Find<System.Windows.Controls.TextBlock>(window,"Resets.Count."+profile.Id).Text.EndsWith("8"),"banked count uses server count rather than visible credits",report);
            Assert(Text(window).Contains("Bez terminu wygaśnięcia"),"explicit no expiry remains distinguishable",report);
            Assert(!Find<System.Windows.Controls.Button>(window,"Resets.Use.unknown-expiry").IsEnabled&&!Find<System.Windows.Controls.Button>(window,"Resets.Use.redeemed").IsEnabled&&!Find<System.Windows.Controls.Button>(window,"Resets.Use.expired").IsEnabled,"unsafe reset credits cannot be consumed",report);
            window.Content=dashboard.ResetInventoryPanel(account with {Error="Synthetic stale read"});window.UpdateLayout();Assert(!Find<System.Windows.Controls.Button>(window,"Resets.Use.available").IsEnabled,"stale inventory cannot be consumed",report);
            window.Content=dashboard.ResetInventoryPanel(account with {Snapshot=snapshot with {ResetCredits=new CodexResetInventory(3,null)}});window.UpdateLayout();Assert(!Descendants<System.Windows.Controls.Button>(window).Any(b=>System.Windows.Automation.AutomationProperties.GetAutomationId(b).StartsWith("Resets.Use.")),"unknown credit details cannot expose consume action",report);
        }
        finally{window.Close();controller.Seed(baseline);dashboard.SelectTab("Konta");dashboard.UpdateLayout();}
    }
    private static void CheckThemeControls(DockController controller,Dashboard dashboard,List<string> report)
    {
        dashboard.SelectTab("Ustawienia");dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.Button>(dashboard,"Theme.Toggle.Main").IsVisible,"main theme toggle visible",report);
        var original=controller.Settings;var originalColor=((SolidColorBrush)dashboard.Background).Color;
        Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text="777";
        Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Notifications").IsChecked=!original.NotificationsEnabled;
        Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Startup").IsChecked=!original.StartWithWindows;
        var widget=new Widget(controller,dashboard){Topmost=false};widget.Show();widget.UpdateLayout();
        try
        {
            Assert(Find<System.Windows.Controls.Button>(widget,"Theme.Toggle.Widget").IsVisible,"widget theme toggle visible",report);
            Click(dashboard,"Theme.Toggle.Main");dashboard.UpdateLayout();widget.UpdateLayout();
            Assert(controller.Settings.Theme=="Light"&&Ui.Light,"main toggle immediately persists light theme",report);
            Assert(Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Theme.Light").IsChecked==true&&Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Theme.Dark").IsChecked==false,"settings radios reflect immediate theme toggle",report);
            Assert(((SolidColorBrush)dashboard.Background).Color!=originalColor&&((SolidColorBrush)widget.Background).Color==((SolidColorBrush)dashboard.Background).Color,"main toggle updates both visible windows",report);
            Assert(controller.Settings with {Theme=original.Theme}==original,"theme toggle leaves other persisted preferences unchanged",report);
            Click(widget,"Theme.Toggle.Widget");dashboard.UpdateLayout();widget.UpdateLayout();
            Assert(controller.Settings.Theme==original.Theme&&!Ui.Light&&((SolidColorBrush)dashboard.Background).Color==originalColor&&((SolidColorBrush)widget.Background).Color==originalColor,"widget toggle restores both windows and persisted theme",report);
            Assert(!widget.Topmost,"theme change preserves widget topmost preference",report);
            Assert(Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text=="777"&&Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Notifications").IsChecked==!original.NotificationsEnabled&&Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Startup").IsChecked==!original.StartWithWindows,"widget theme change preserves unsaved settings draft",report);
            Click(dashboard,"Settings.Revert");dashboard.UpdateLayout();
            Assert(controller.Settings==original&&Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text==original.RefreshSeconds.ToString(),"revert discards draft without changing immediate theme",report);
            Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text="888";
            Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Theme.Light").IsChecked=true;dashboard.UpdateLayout();widget.UpdateLayout();
            Assert(controller.Settings.Theme=="Light"&&Ui.Light&&controller.Settings.RefreshSeconds==original.RefreshSeconds&&Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text=="888","settings radio applies only theme immediately",report);
            Click(dashboard,"Settings.Revert");dashboard.UpdateLayout();Assert(controller.Settings.Theme=="Light"&&Ui.Light&&!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled,"revert keeps immediately applied theme",report);
            Click(widget,"Theme.Toggle.Widget");dashboard.UpdateLayout();
        }
        finally {widget.Close();}
        dashboard.SelectTab("Konta");dashboard.UpdateLayout();
    }
    private static void CheckTabs(DockController controller,Dashboard dashboard,List<string> report)
    {
        dashboard.Width=960;dashboard.Height=580;dashboard.SelectTab("Konta");dashboard.UpdateLayout();var accountScroll=Descendants<System.Windows.Controls.ScrollViewer>(dashboard).First(s=>s.ScrollableWidth>0);accountScroll.ScrollToHorizontalOffset(0);accountScroll.ScrollToVerticalOffset(0);dashboard.UpdateLayout();var horizontal=Descendants<System.Windows.Controls.Primitives.ScrollBar>(accountScroll).First(s=>s.Orientation==System.Windows.Controls.Orientation.Horizontal);var track=(System.Windows.Controls.Primitives.Track)horizontal.Template.FindName("PART_Track",horizontal);((System.Windows.Input.RoutedCommand)track.IncreaseRepeatButton.Command).Execute(null,track.IncreaseRepeatButton);dashboard.UpdateLayout();Assert(accountScroll.HorizontalOffset>0&&accountScroll.VerticalOffset==0,"horizontal scrollbar page click moves horizontally",report);dashboard.Width=1128;dashboard.Height=756;
        dashboard.SelectTab("Ustawienia");dashboard.UpdateLayout();
        dashboard.Activate();var nav=Find<System.Windows.Controls.Button>(dashboard,"Nav.Ustawienia");nav.Focus();Click(dashboard,"Nav.Ustawienia");dashboard.UpdateLayout();dashboard.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Background);Assert(Find<System.Windows.Controls.Button>(dashboard,"Nav.Ustawienia").IsKeyboardFocused,"navigation preserves keyboard focus",report);
        var scroller=Descendants<System.Windows.Controls.ScrollViewer>(dashboard).First();scroller.ScrollToBottom();dashboard.UpdateLayout();var startupControl=Find<FrameworkElement>(dashboard,"Settings.Startup");var startupPosition=startupControl.TransformToAncestor(dashboard).Transform(new Point(0,0));var saveControl=Find<FrameworkElement>(dashboard,"Settings.Save");var savePosition=saveControl.TransformToAncestor(dashboard).Transform(new Point(0,0));Assert(startupPosition.Y>=160&&startupPosition.Y+startupControl.ActualHeight<=dashboard.ActualHeight-60&&savePosition.Y+saveControl.ActualHeight<=dashboard.ActualHeight,"settings scroll reaches startup with save visible",report);scroller.ScrollToTop();dashboard.UpdateLayout();
        var original=controller.Settings;var interval=Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval");
        var host=(System.Windows.Controls.ScrollViewer)interval.Template.FindName("PART_ContentHost",interval);var viewport=Descendants<System.Windows.Controls.ScrollContentPresenter>(host).First();var glyph=new FormattedText(interval.Text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(interval.FontFamily,interval.FontStyle,interval.FontWeight,interval.FontStretch),interval.FontSize,interval.Foreground,1);Assert(viewport.ActualHeight>=glyph.Height,"interval viewport fits full glyph height",report);
        Assert(!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled,"settings clean save disabled",report);
        interval.Text="777";controller.Favorite(controller.Accounts.First().Profile.Id);dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text=="777","settings draft survives controller Changed",report);
        dashboard.SelectTab("Konta");dashboard.SelectTab("Ustawienia");dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text=="777","settings draft survives tab switch",report);
        foreach(var invalid in new[]{"","abc","119","86401"})
        {
            Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text=invalid;dashboard.UpdateLayout();
            Assert(!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled&&!string.IsNullOrWhiteSpace(Find<System.Windows.Controls.TextBlock>(dashboard,"Settings.Status").Text)&&controller.Settings==original,"inline invalid interval "+(invalid==""?"empty":invalid),report);
        }
        Click(dashboard,"Settings.Revert");dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text==original.RefreshSeconds.ToString()&&!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled,"revert restores persisted settings",report);
        Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text="120";Click(dashboard,"Settings.Save");dashboard.UpdateLayout();
        Assert(controller.Settings.RefreshSeconds==120&&!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled&&!string.IsNullOrWhiteSpace(Find<System.Windows.Controls.TextBlock>(dashboard,"Settings.Status").Text),"save lower boundary with feedback",report);
        Find<System.Windows.Controls.TextBox>(dashboard,"Settings.Interval").Text="86400";Click(dashboard,"Settings.Save");dashboard.UpdateLayout();
        Assert(controller.Settings.RefreshSeconds==86400,"save upper boundary",report);
        var light=Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Theme.Light");light.IsChecked=true;
        dashboard.UpdateLayout();Assert(controller.Settings.Theme=="Light"&&Ui.Light&&!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled,"light theme applied immediately via UI",report);
        Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Theme.Dark").IsChecked=true;dashboard.UpdateLayout();Assert(controller.Settings.Theme=="Dark"&&!Ui.Light&&!Find<System.Windows.Controls.Button>(dashboard,"Settings.Save").IsEnabled,"dark theme applied immediately via UI",report);
        var alerts=Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Notifications");alerts.IsChecked=!controller.Settings.NotificationsEnabled;var expectedAlerts=alerts.IsChecked==true;Click(dashboard,"Settings.Save");dashboard.UpdateLayout();Assert(controller.Settings.NotificationsEnabled==expectedAlerts,"notification preference saved via UI",report);
        Assert(!Find<System.Windows.Controls.Primitives.ToggleButton>(dashboard,"Settings.Startup").IsEnabled,"offline startup cannot mutate registry",report);
        var baseline=controller.Accounts.ToArray();var time=DateTimeOffset.Parse("2026-01-01T12:00:00Z");var profile=new ConnectionProfile(Guid.Parse("15c8327f-5027-4dfa-bbc3-48eeab008872"),"Edge windows",ProviderKind.Codex,"demo");
        var fixture=new AccountView(profile,new UsageSnapshot(profile.Id,time,new[]{new UsageWindow("Window alpha",null,null),new UsageWindow("Window beta",35,null),new UsageWindow("Window gamma",91,null)}),"Synthetic read failure");
        controller.Seed(new[]{fixture});dashboard.SelectTab("Statystyki");dashboard.UpdateLayout();var card=Find<FrameworkElement>(dashboard,"Stats.Account."+profile.Id);var text=Text(card);
        Assert(text.Contains("Window alpha")&&text.Contains("Window beta")&&text.Contains("Window gamma"),"statistics shows all provider windows",report);
        Assert(text.Contains("Niedostępne")&&!text.Contains("0%"),"missing percentage stays unavailable",report);
        Assert(text.Contains("Nieaktualne"),"statistics exposes stale read",report);
        controller.Seed(new[]{new AccountView(profile,Error:"Synthetic read failure")});dashboard.UpdateLayout();
        Assert(Text(dashboard).Contains("Brak odczytu")||Text(dashboard).Contains("Niedostępne"),"statistics missing snapshot state",report);
        controller.Seed(Array.Empty<AccountView>());dashboard.UpdateLayout();Assert(Find<FrameworkElement>(dashboard,"Stats.Empty").IsVisible,"statistics empty state",report);
        controller.Seed(new[]{fixture with {Error=null}});dashboard.SelectTab("Historia");dashboard.UpdateLayout();Click(dashboard,"History.Clear");dashboard.UpdateLayout();Assert(Find<FrameworkElement>(dashboard,"History.Empty").IsVisible,"history clear removes session events",report);Click(dashboard,"History.Filter.Problems");dashboard.UpdateLayout();Assert(Find<FrameworkElement>(dashboard,"History.Empty").IsVisible,"history filter empty state",report);Click(dashboard,"History.Filter.All");
        controller.Seed(new[]{fixture with {Error=null,Snapshot=fixture.Snapshot! with {FetchedAt=time.AddMinutes(1)}}});
        controller.Seed(new[]{fixture with {Snapshot=fixture.Snapshot! with {FetchedAt=time.AddMinutes(1)}}});dashboard.UpdateLayout();
        var all=Find<System.Windows.Controls.Panel>(dashboard,"History.Entries").Children.Count;Click(dashboard,"History.Filter.Problems");dashboard.UpdateLayout();var problems=Find<System.Windows.Controls.Panel>(dashboard,"History.Entries").Children.Count;
        Assert(problems>0&&problems<all,"history problems filter excludes successful reads",report);
        Find<System.Windows.Controls.Button>(dashboard,"History.Filter.Reads").Focus();Click(dashboard,"History.Filter.Reads");dashboard.UpdateLayout();dashboard.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Background);Assert(Find<System.Windows.Controls.Button>(dashboard,"History.Filter.Reads").IsKeyboardFocused,"history filter preserves keyboard focus",report);Assert(Find<System.Windows.Controls.Panel>(dashboard,"History.Entries").Children.Count>0,"history reads filter retains successful reads",report);
        Click(dashboard,"History.Filter.All");for(var i=0;i<105;i++)controller.Seed(new[]{fixture with {Error=null,Snapshot=fixture.Snapshot! with {FetchedAt=time.AddMinutes(i+2)}}});dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.Panel>(dashboard,"History.Entries").Children.Count==100,"history bounded at 100 actual events",report);
        var prior=Text(Find<FrameworkElement>(dashboard,"History.Entries"));controller.SetSettings(controller.Settings);dashboard.UpdateLayout();
        Assert(Text(Find<FrameworkElement>(dashboard,"History.Entries"))==prior,"unchanged controller state does not create history",report);
        Click(dashboard,"History.Clear");controller.Save(profile,"fixture-credential");dashboard.UpdateLayout();Click(dashboard,"History.Clear");controller.Save(profile,"replacement-fixture-credential");dashboard.UpdateLayout();
        Assert(Find<System.Windows.Controls.Panel>(dashboard,"History.Entries").Children.Count>0&&!Descendants<FrameworkElement>(dashboard).Any(x=>x.IsVisible&&System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="History.Empty"),"credential replacement creates edit history",report);
        controller.Seed(new[]{fixture with {RetryAfter=time.AddHours(1)}});Click(dashboard,"History.Clear");controller.Seed(new[]{fixture with {RetryAfter=time.AddHours(2)}});dashboard.UpdateLayout();
        Assert(!Descendants<FrameworkElement>(dashboard).Any(x=>x.IsVisible&&System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="History.Empty"),"changed cooldown creates history",report);
        var sameCooldown=Text(Find<FrameworkElement>(dashboard,"History.Entries"));controller.Seed(new[]{fixture with {RetryAfter=time.AddHours(2)}});dashboard.UpdateLayout();Assert(Text(Find<FrameworkElement>(dashboard,"History.Entries"))==sameCooldown,"identical failed read does not duplicate history",report);
        controller.Seed(baseline);controller.SetSettings(original);Ui.Theme(original.Theme=="Light");dashboard.SelectTab("Konta");dashboard.UpdateLayout();
    }
    private static async Task CheckHistoryRefresh(List<string> report)
    {
        using var model=new DockController(new OfflineProvider(),null);
        var window=new Dashboard(model){AllowClose=true};window.Show();
        try
        {
            var profile=new ConnectionProfile(Guid.NewGuid(),"Cooldown fixture",ProviderKind.ClaudeOAuth);
            model.Seed(new[]{new AccountView(profile,Error:"Synthetic cooldown",RetryAfter:DateTimeOffset.MaxValue)});
            window.SelectTab("Historia");window.UpdateLayout();Click(window,"History.Clear");await model.RefreshAsync();window.UpdateLayout();Click(window,"History.Filter.Reads");window.UpdateLayout();
            Assert(Find<FrameworkElement>(window,"History.Empty").IsVisible,"skipped cooldown refresh creates no successful read",report);
            model.Seed(Array.Empty<AccountView>());Click(window,"History.Filter.All");Click(window,"History.Clear");await model.RefreshAsync();window.UpdateLayout();Click(window,"History.Filter.Reads");window.UpdateLayout();
            Assert(Find<FrameworkElement>(window,"History.Empty").IsVisible,"empty refresh creates no successful read",report);
        }
        finally {window.Close();}
    }
    private static async Task CheckResetBridge(List<string> report,string outputDirectory)
    {
        var directory=Path.Combine(outputDirectory,"reset-fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var profile=new ConnectionProfile(Guid.Parse("d41c80c6-5d23-4ae8-b354-393867b52612"),"Bridge fixture",ProviderKind.Codex,"fixture-account");
        try
        {
            var provider=new ResetFixtureProvider{Outcome=CodexResetOutcome.Uncertain};string requestId;
            using(var model=new DockController(provider,null,resetJournalDirectory:directory))
            {
                model.Save(profile,"fixture-secret");var result=await model.ConsumeResetAsync(profile.Id,"credit-1");Assert(result.Outcome==CodexResetOutcome.Uncertain&&provider.RequestIds.Count==1,"uncertain reset dispatch is not automatically retried",report);requestId=model.GetPendingReset(profile.Id)!.RequestId;
                Assert(Guid.TryParse(requestId,out _)&&provider.RequestIds.Single()==requestId,"pending reset records original idempotency key",report);
                Assert(!string.Join("",Directory.GetFiles(directory).Select(File.ReadAllText)).Contains("fixture-secret"),"reset journal does not expose credential",report);
            }
            provider.Outcome=CodexResetOutcome.AlreadyRedeemed;
            using(var restarted=new DockController(provider,null,resetJournalDirectory:directory))
            {
                restarted.Save(profile,"fixture-secret");Assert(restarted.GetPendingReset(profile.Id)?.RequestId==requestId,"pending reset survives controller restart",report);
                var result=await restarted.ConsumeResetAsync(profile.Id,"credit-1");Assert(result.Outcome==CodexResetOutcome.AlreadyRedeemed&&provider.RequestIds.Last()==requestId,"explicit reset retry preserves original request key",report);
                Assert(restarted.GetPendingReset(profile.Id)==null&&provider.FetchCount>0,"confirmed reset clears journal and refreshes usage",report);
            }
            var delayed=new ResetFixtureProvider{ConsumeGate=new TaskCompletionSource<CodexResetResult>(TaskCreationOptions.RunContinuationsAsynchronously)};
            using(var model=new DockController(delayed,null,resetJournalDirectory:directory))
            {
                model.Save(profile,"fixture-secret");var first=model.ConsumeResetAsync(profile.Id,"credit-1");Assert(model.IsResetBusy(profile.Id),"reset operation exposes busy state",report);await model.ConsumeResetAsync(profile.Id,"credit-1");Assert(delayed.RequestIds.Count==1,"concurrent reset click cannot dispatch twice",report);delayed.ConsumeGate.SetResult(new CodexResetResult(CodexResetOutcome.Reset,WindowsReset:1));await first;Assert(!model.IsResetBusy(profile.Id),"completed reset clears busy state",report);
            }
            foreach(var remove in new[]{false,true})
            {
                var delayedRead=new ResetFixtureProvider{InventoryGate=new TaskCompletionSource<CodexResetInventory>(TaskCreationOptions.RunContinuationsAsynchronously)};
                using var model=new DockController(delayedRead,null,resetJournalDirectory:directory);model.Save(profile,"fixture-secret");var pending=model.ConsumeResetAsync(profile.Id,"credit-1");if(remove)model.Remove(profile.Id);else model.Save(profile with {AccountId="different-account"},"different-fixture-secret");delayedRead.InventoryGate.SetResult(ResetFixtureProvider.Inventory);await pending;
                Assert(delayedRead.RequestIds.Count==0,remove?"deleted account cannot consume after preflight":"edited account cannot consume after preflight",report);
            }
            foreach(var remove in new[]{false,true})
            {
                var delayedPost=new ResetFixtureProvider{ConsumeGate=new TaskCompletionSource<CodexResetResult>(TaskCreationOptions.RunContinuationsAsynchronously)};
                using var model=new DockController(delayedPost,null,resetJournalDirectory:directory);model.Save(profile,"fixture-secret");var pending=model.ConsumeResetAsync(profile.Id,"credit-1");if(remove)model.Remove(profile.Id);else model.Save(profile with {Name="Edited after dispatch"},"replacement-secret");delayedPost.ConsumeGate.SetResult(new CodexResetResult(CodexResetOutcome.Reset,WindowsReset:1));await pending;
                Assert(delayedPost.FetchCount==0&&(remove?model.Accounts.Count==0:model.Accounts.Single().Snapshot==null),remove?"deleted account stays deleted after reset response":"edited account ignores stale reset refresh",report);
            }
            var oldRead=new ResetFixtureProvider{FetchGate=new TaskCompletionSource<FetchResult>(TaskCreationOptions.RunContinuationsAsynchronously)};
            using(var model=new DockController(oldRead,null,resetJournalDirectory:directory))
            {
                model.Save(profile,"fixture-secret");var refresh=model.RefreshAsync();await model.ConsumeResetAsync(profile.Id,"credit-1");oldRead.FetchGate.SetResult(new FetchResult(new UsageSnapshot(profile.Id,DateTimeOffset.Parse("2026-01-01T00:00:00Z"),new[]{new UsageWindow("5-hour window",99,null)})));await refresh;Assert(model.Accounts.Single().Snapshot?.Windows.Single().UsedPercent==0,"pre-reset refresh cannot overwrite reset snapshot",report);
            }
            var retained=new ResetFixtureProvider{Outcome=CodexResetOutcome.Uncertain};var retainedProfile=profile with {Id=Guid.Parse("26d30215-c6ea-45b9-9e6a-d582f1623b2d")};
            using(var model=new DockController(retained,null,resetJournalDirectory:directory))
            {
                model.Save(retainedProfile,"fixture-secret");await model.ConsumeResetAsync(retainedProfile.Id,"credit-1");var key=model.GetPendingReset(retainedProfile.Id)!.RequestId;retained.Outcome=CodexResetOutcome.Denied;await model.ConsumeResetAsync(retainedProfile.Id,"credit-1");Assert(model.GetPendingReset(retainedProfile.Id)?.RequestId==key,"denied retry retains uncertain original request",report);retained.Outcome=CodexResetOutcome.RateLimited;await model.ConsumeResetAsync(retainedProfile.Id,"credit-1");var calls=retained.RequestIds.Count;await model.ConsumeResetAsync(retainedProfile.Id,"credit-1");Assert(retained.RequestIds.Count==calls&&model.GetPendingReset(retainedProfile.Id)?.RequestId==key,"rate limited reset retains key and blocks immediate retry",report);
            }        }
        finally{Directory.Delete(directory,true);}
    }
    private static async Task CheckRaces(List<string> report)
    {
        var delayed=new DelayedProvider();using var model=new DockController(delayed,null);
        var profiles=Enumerable.Range(0,4).Select(i=>new ConnectionProfile(Guid.NewGuid(),"Race "+i,ProviderKind.ClaudeOAuth)).ToArray();
        foreach(var p in profiles)model.Save(p,"old-credential");
        var refresh=model.RefreshAsync();Assert(delayed.Calls.Count==3,"refresh concurrency bound",report);
        model.Save(profiles[3] with{Name="Edited while queued"},"new-credential");
        model.Remove(profiles[0].Id);delayed.Release();await refresh;
        Assert(delayed.Calls.Count==3,"queued edited account not sent",report);
        Assert(model.Accounts.All(a=>a.Profile.Id!=profiles[0].Id),"deleted account response discarded",report);
        Assert(model.Accounts.Single(a=>a.Profile.Id==profiles[3].Id).Snapshot==null,"edited account snapshot invalidated",report);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T typed)yield return typed;foreach(var nested in Descendants<T>(child))yield return nested;}}
    private static void Assert(bool condition,string label,List<string> report){if(!condition)throw new InvalidOperationException(label);report.Add("PASS: "+label);}
}
internal sealed class DelayedProvider : IUsageProvider
{
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<Guid> Calls { get; }=new();
    public void Release()=>ready.TrySetResult();
    public async Task<FetchResult> FetchAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)
    { Calls.Add(profile.Id);await ready.Task.WaitAsync(cancellationToken);return new FetchResult(new UsageSnapshot(profile.Id,DateTimeOffset.UtcNow,new[]{new UsageWindow("Test",50,null)})); }
    public Task<IReadOnlyList<WorkspaceOption>> DiscoverAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<WorkspaceOption>>(Array.Empty<WorkspaceOption>());
}
























internal sealed class ResetFixtureProvider:IUsageProvider,ICodexResetService
{
    public static readonly CodexResetInventory Inventory=new(1,new[]{new CodexResetCredit("credit-1","codex_rate_limits","available",DateTimeOffset.Parse("2026-01-01T00:00:00Z"),null,true)});
    public CodexResetOutcome Outcome{get;set;}=CodexResetOutcome.Reset;
    public List<string> RequestIds{get;}=new();
    public int FetchCount{get;private set;}
    public TaskCompletionSource<CodexResetResult>? ConsumeGate{get;init;}
    public TaskCompletionSource<CodexResetInventory>? InventoryGate{get;init;}
    public TaskCompletionSource<FetchResult>? FetchGate{get;init;}
    public Task<CodexResetInventory> GetResetCreditsAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)=>InventoryGate?.Task??Task.FromResult(Inventory);
    public Task<CodexResetResult> ConsumeAsync(ConnectionProfile profile,string secret,string requestId,string? creditId=null,CancellationToken cancellationToken=default){RequestIds.Add(requestId);return ConsumeGate?.Task??Task.FromResult(new CodexResetResult(Outcome,RetryAfter:Outcome==CodexResetOutcome.RateLimited?DateTimeOffset.MaxValue:null,WindowsReset:Outcome==CodexResetOutcome.Reset?1:0));}
    public Task<FetchResult> FetchAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default){FetchCount++;if(FetchCount==1&&FetchGate!=null)return FetchGate.Task;return Task.FromResult(new FetchResult(new UsageSnapshot(profile.Id,DateTimeOffset.Parse("2026-09-06T10:00:00Z"),new[]{new UsageWindow("5-hour window",0,null)},ResetCredits:Inventory)));}
    public Task<IReadOnlyList<WorkspaceOption>> DiscoverAsync(ConnectionProfile profile,string secret,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<WorkspaceOption>>(Array.Empty<WorkspaceOption>());
}
