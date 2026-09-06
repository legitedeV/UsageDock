using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UsageDock.Core;
using Forms=System.Windows.Forms;
namespace UsageDock.App;
public partial class App : Application
{
    private System.Threading.Mutex? instance;
    private DockController? controller;
    private Forms.NotifyIcon? tray;
    private Dashboard? dashboard;
    private Widget? widget;
    private DispatcherTimer? timer;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            var screenshot=Value(e.Args,"--screenshot");var smoke=Value(e.Args,"--smoke-test");var demo=e.Args.Contains("--demo")||screenshot!=null||smoke!=null;
            if(!demo){instance=new System.Threading.Mutex(true,@"Local\UsageDock-"+System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value,out var created);if(!created){MessageBox.Show("UsageDock już działa. Otwórz go z zasobnika systemowego.","UsageDock");Shutdown();return;}}
            controller=new DockController(demo?new OfflineProvider():new ProviderService(),demo?null:new LocalStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UsageDock")),demo);
            if(demo)controller.Seed(DemoData.Accounts());Ui.Theme(controller.Settings.Theme=="Light");dashboard=new Dashboard(controller);MainWindow=dashboard;dashboard.WidgetRequested+=ShowWidget;dashboard.Show();
            if(screenshot!=null){await Task.Delay(300);await Verification.CaptureAsync(screenshot,controller,dashboard);dashboard.AllowClose=true;Shutdown();return;}
            if(smoke!=null){var okay=await Verification.SmokeAsync(smoke,controller,dashboard);dashboard.AllowClose=true;Shutdown(okay?0:1);return;}
            tray=new Forms.NotifyIcon{Text="UsageDock",Icon=TrayArtwork.Create(),Visible=true};
            var menu=new Forms.ContextMenuStrip();menu.Items.Add("Otwórz UsageDock",null,(_,_)=>{dashboard.Show();dashboard.Activate();});menu.Items.Add("Mini widget",null,(_,_)=>ShowWidget());menu.Items.Add("Odśwież",null,async(_,_)=>await controller.RefreshAsync());menu.Items.Add("Zakończ",null,(_,_)=>{dashboard.AllowClose=true;Shutdown();});tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>{dashboard.Show();dashboard.Activate();};
            controller.Alert+=message=>tray.ShowBalloonTip(5000,"UsageDock",message,Forms.ToolTipIcon.Info);
            timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(controller.Settings.RefreshSeconds)};timer.Tick+=async(_,_)=>await controller.RefreshAsync();controller.Changed+=()=>timer.Interval=TimeSpan.FromSeconds(controller.Settings.RefreshSeconds);timer.Start();await controller.RefreshAsync();
        }
        catch(Exception failure){if(Value(e.Args,"--smoke-test") is { } failurePath){File.WriteAllText(failurePath,failure.GetType().Name+" at "+failure.StackTrace);Shutdown(1);return;}MessageBox.Show("Nie udało się uruchomić UsageDock. Sprawdź dostęp do folderu danych aplikacji. Zapisane poświadczenia nie zostały zmienione.","UsageDock",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(1);}
    }
    private static string? Value(string[] args,string key){var index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:null;}
    private void ShowWidget(){if(widget==null){widget=new Widget(controller!,dashboard!);widget.Closed+=(_,_)=>widget=null;}widget.Show();widget.Activate();}
    protected override void OnExit(ExitEventArgs e){timer?.Stop();tray?.Dispose();controller?.Dispose();instance?.Dispose();base.OnExit(e);}
}
