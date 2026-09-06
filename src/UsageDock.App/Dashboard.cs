using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UsageDock.Core;
namespace UsageDock.App;
public sealed partial class Dashboard : Window
{
 private readonly DockController controller;
 private StackPanel rows=new();
 private TextBox search=new();
 private string tab="Konta";
 private TextBlock? updateStamp;
 private TextBlock? accountCount;

 public event Action? WidgetRequested;
 public bool AllowClose;
 public Dashboard(DockController controller){this.controller=controller;clock=new UsageClock(this);Ui.Window(this,"UsageDock",1128,756);MinWidth=960;MinHeight=580;Design.Chrome(this);InitializeDraft();previousAccounts=controller.Accounts.ToArray();previousSettings=controller.Settings;Build();controller.Changed+=Changed;Closed+=(_,_)=>controller.Changed-=Changed;Closing+=(_,e)=>{if(!AllowClose){e.Cancel=true;Hide();}};}
 private void Changed(){if(!Dispatcher.CheckAccess()){Dispatcher.Invoke(Changed);return;}RecordChanges();Repaint();}
 public void SelectTab(string value){tab=value;Build();RestoreFocus("Nav."+value);}
 public void Build()
 {
  Background=Ui.Background;Foreground=Ui.Text;var query=search.Text;
  var root=new Grid();foreach(var h in new[]{88d,72d,-1d,tab=="Ustawienia"?72d:52d})root.RowDefinitions.Add(new RowDefinition{Height=h<0?new GridLength(1,GridUnitType.Star):new GridLength(h)});
  var header=Design.Canvas(1126,88);header.Width=double.NaN;header.HorizontalAlignment=HorizontalAlignment.Stretch;header.Background=Ui.Hex(Ui.Light?"#F2F4F6":"#161E25");header.MouseRightButtonUp+=(_,_)=>System.Windows.SystemCommands.ShowSystemMenu(this,PointToScreen(new Point(30,60)));Design.DragHeader(this,header);
  var logoButton=Design.Action("logo","Menu okna",()=>System.Windows.SystemCommands.ShowSystemMenu(this,PointToScreen(new Point(30,60))),28,28,false,true);logoButton.Content=Design.Icon("logo",28,Ui.Accent);logoButton.Padding=new Thickness(0);Design.At(header,logoButton,30,26);Design.Txt(header,"UsageDock",72,21,20,null,true);Design.Txt(header,"Dla tych, którzy robią więcej",72,55,14,Ui.Muted);
  Design.At(header,ThemeButton("Theme.Toggle.Main",false),426,26);
  search=new TextBox{Text=query,Width=250,Height=44,Padding=new Thickness(8,10,8,8),Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=14,ToolTip="Szukaj kont…"};search.TextChanged+=(_,_)=>Repaint();
  var searchFrame=Design.Canvas(294,46);searchFrame.Background=Ui.Surface;Design.At(searchFrame,Design.Icon("search",20),10,13);Design.At(searchFrame,search,34,0);if(string.IsNullOrEmpty(query)){var hint=Design.Text("Szukaj kont…",14,Ui.Muted);hint.IsHitTestVisible=false;Design.At(searchFrame,hint,44,13);search.TextChanged+=(_,_)=>hint.Visibility=string.IsNullOrEmpty(search.Text)?Visibility.Visible:Visibility.Collapsed;}
  var searchBorder=new Border{CornerRadius=new CornerRadius(6),BorderBrush=Design.Line,BorderThickness=new Thickness(1),Child=searchFrame};Design.At(header,searchBorder,550,23);
  var closeButton=Design.Action("close","Zamknij do zasobnika",Close,28,20,false,true);var minimizeButton=Design.Action("minimize","Minimalizuj",()=>WindowState=WindowState.Minimized,28,20,false,true);closeButton.Padding=new Thickness(0);minimizeButton.Padding=new Thickness(0);Design.At(header,closeButton,1080,1);Design.At(header,minimizeButton,1046,1);var addButton=Design.Action("plus","Dodaj połączenie",()=>Edit(null),172,46,true);var refreshButton=Design.Action("refresh","Odśwież wszystko",Refresh,46,46,false,true);Design.At(header,addButton,866,23);Design.At(header,refreshButton,1056,23);header.SizeChanged+=(_,_)=>{var deficit=Math.Max(0,1126-header.ActualWidth);searchFrame.Width=Math.Max(126,294-deficit);search.Width=Math.Max(82,250-deficit);Canvas.SetLeft(closeButton,1080-deficit);Canvas.SetLeft(minimizeButton,1046-deficit);Canvas.SetLeft(addButton,866-deficit);Canvas.SetLeft(refreshButton,1056-deficit);};root.Children.Add(header);
  var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(18,10,0,14)};var names=new[]{"Konta","Statystyki","Historia","Ustawienia"};var icons=new[]{"logo","stats","history","settings"};for(var i=0;i<names.Length;i++){var name=names[i];var b=Design.Action(icons[i],name,()=>SelectTab(name),i==0?124:148,46);b.Background=name==tab?Ui.Hex(Ui.Light?"#DCECE7":"#1E2C37"):Brushes.Transparent;b.BorderBrush=Brushes.Transparent;b.Margin=new Thickness(0,0,8,0);if(name==tab){b.Foreground=Ui.Accent;b.BorderBrush=Ui.Accent;b.BorderThickness=new Thickness(3,0,0,0);}System.Windows.Automation.AutomationProperties.SetAutomationId(b,"Nav."+name);tabs.Children.Add(b);}Grid.SetRow(tabs,1);root.Children.Add(tabs);
  rows=new StackPanel();var scroll=new ScrollViewer{Content=rows,HorizontalScrollBarVisibility=tab=="Konta"?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,2);root.Children.Add(scroll);
  var footer=new Grid{Margin=new Thickness(32,0,32,0)};footer.Children.Add(new Border{BorderBrush=Design.Line,BorderThickness=new Thickness(0,1,0,0)});var count=Design.Text(AccountNumber(controller.Accounts.Count)+(controller.Offline?" · DEMO":""),14,Ui.Muted);accountCount=count;count.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(count);var latest=controller.Accounts.Where(a=>a.Snapshot!=null).Select(a=>a.Snapshot!.FetchedAt).DefaultIfEmpty().Max();var stamp=Design.Text(latest==default?"Oczekiwanie na aktualizację":"Ostatnia aktualizacja: "+(controller.Offline?"5 min temu":latest.ToLocalTime().ToString("HH:mm")),14,Ui.Muted);updateStamp=stamp;stamp.HorizontalAlignment=HorizontalAlignment.Right;stamp.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(stamp);Grid.SetRow(footer,3);if(tab=="Ustawienia"){var savebar=SettingsSaveBar();Grid.SetRow(savebar,3);root.Children.Add(savebar);}else root.Children.Add(footer);
  searchBorder.Visibility=tab is "Konta" or "Statystyki"?Visibility.Visible:Visibility.Collapsed;Content=Design.Frame(root);Repaint();
 }
 public void Repaint()
 {
  if(!Dispatcher.CheckAccess()){Dispatcher.Invoke(Repaint);return;}if(accountCount!=null)accountCount.Text=AccountNumber(controller.Accounts.Count)+(controller.Offline?" · DEMO":"");if(updateStamp!=null){var latest=controller.Accounts.Where(a=>a.Snapshot!=null).Select(a=>a.Snapshot!.FetchedAt).DefaultIfEmpty().Max();updateStamp.Text=controller.IsRefreshing?"Odświeżanie…":latest==default?"Oczekiwanie na aktualizację":"Ostatnia aktualizacja: "+(controller.Offline?"5 min temu":latest.ToLocalTime().ToString("HH:mm"));}
  if(tab=="Ustawienia"){if(rows.Children.Count==0)rows.Children.Add(SettingsPanel());return;}
  rows.Children.Clear();
  if(tab=="Historia"){rows.Children.Add(HistoryPanel());return;}
  if(tab=="Statystyki"){rows.Children.Add(StatisticsPanel());return;}
  var accountHeading=Page("Konta","Wszystkie połączenia w jednym miejscu. Gwiazdka dodaje konto do widżetu.");accountHeading.Margin=new Thickness(32,8,32,0);rows.Children.Add(accountHeading);
  var head=Design.Canvas(1104,50);Design.At(head,new Border{Width=1068,Height=1,Background=Design.Line},32,0);var labels=new[]{"Nazwa","Typ","Wykorzystanie / Koszt","Reset / Budżet","Tokeny","Akcje"};var xs=new[]{58d,306,432,714,890,1022};for(var i=0;i<labels.Length;i++)Design.Txt(head,labels[i],xs[i],16,16,Ui.Muted);rows.Children.Add(head);
  var accounts=controller.Filter(search.Text,"All connections").ToArray();foreach(var a in accounts)rows.Children.Add(AccountCard(a,false));
  if(accounts.Length==0)rows.Children.Add(new Border{Padding=new Thickness(32),Child=Ui.Stack(Ui.Label("Brak kont do wyświetlenia",22),Ui.Label("Dodaj połączenie lub zmień wyszukiwanie.",14,Ui.Muted),Ui.Button("Dodaj połączenie",()=>Edit(null),true))});

 }
 private UIElement MiniCard(AccountView account)
 {
  var canvas=Design.Canvas(230,86);
  Design.At(canvas,Design.Provider(account.Profile.Provider,46,true),10,18);
  var name=Truncated(account.Profile.Name,14);name.Width=82;name.FontWeight=FontWeights.SemiBold;Design.At(canvas,name,68,12);
  var snapshot=account.Snapshot;var api=DockController.IsApi(account.Profile.Provider);
  var used=snapshot?.Windows.FirstOrDefault()?.UsedPercent;
  var percent=api?(account.Profile.MonthlyBudget is >0&&snapshot?.CostUsd is {} cost?(double)(cost/account.Profile.MonthlyBudget.Value*100):null):used;
  var value=Truncated(api?Money(snapshot?.CostUsd):used is {} number?$"{number:0.#}%":"—",16);value.Width=80;value.TextAlignment=TextAlignment.Right;value.FontWeight=FontWeights.SemiBold;Design.At(canvas,value,138,12);
  if(percent.HasValue)Design.At(canvas,Design.Bar(percent.Value,150,8),68,40);
  TextBlock detail;
  if(account.Error!=null)detail=Truncated("Nieaktualne",11,Warning);
  else if(api)detail=Truncated("z "+Money(account.Profile.MonthlyBudget),11,Ui.Muted);
  else detail=clock.Label(snapshot?.Windows.FirstOrDefault()?.ResetsAt,false,11);
  detail.Width=150;detail.TextAlignment=TextAlignment.Right;Design.At(canvas,detail,68,56);
  if(account.Profile.Provider==ProviderKind.Codex)
  {
   var inventory=snapshot?.ResetCredits;
   var count=Truncated(inventory==null?"Zapasowe resety: —":$"Zapasowe resety: {inventory.AvailableCount}",11,Ui.Muted);count.Width=150;
   var next=inventory?.Credits?.Where(credit=>credit.CanRedeem(DateTimeOffset.UtcNow)&&credit.ExpiresAt.HasValue).OrderBy(credit=>credit.ExpiresAt).FirstOrDefault();
   count.ToolTip=next==null?"Szczegóły ważności w zarządzaniu resetami":UsageTime.Full(next.ExpiresAt,DateTimeOffset.UtcNow,TimeZoneInfo.Local);
   Design.At(canvas,count,68,72);
  }
  return new Border{Background=Ui.Hex(Ui.Light?"#FFFFFF":"#171F25"),BorderBrush=Design.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,0,10),Child=canvas};
 }
 private void ShowDetails(AccountView account)
 {
  var window=new Window{Owner=this};Ui.Window(window,"Szczegóły odczytu · UsageDock",600,520);var p=new StackPanel{Margin=new Thickness(24)};p.Children.Add(Ui.Label(account.Profile.Name,24));p.Children.Add(Ui.Label(ProviderLabel(account.Profile.Provider),14,Ui.Muted));
  if(account.Snapshot is {} snapshot){foreach(var w in snapshot.Windows){p.Children.Add(Ui.Label($"{WindowLabel(w.Name)}: {(w.UsedPercent.HasValue?w.UsedPercent.Value.ToString("0.#")+"%":"Brak danych")}",16));var time=clock.Label(w.ResetsAt,true);time.TextWrapping=TextWrapping.Wrap;time.Margin=new Thickness(0,0,0,16);p.Children.Add(time);}if(snapshot.CostUsd.HasValue)p.Children.Add(Ui.Label(Money(snapshot.CostUsd)));if(snapshot.Tokens.HasValue)p.Children.Add(Ui.Label(Tokens(snapshot.Tokens)+" tokenów"));if(!string.IsNullOrWhiteSpace(snapshot.Note))p.Children.Add(Ui.Label(snapshot.Note,14,Ui.Muted));p.Children.Add(Ui.Label("Ostatni odczyt: "+snapshot.FetchedAt.ToLocalTime().ToString("g"),14,Ui.Muted));}else p.Children.Add(Ui.Label("Brak odczytu."));if(account.Error!=null)p.Children.Add(Ui.Label("Odczyt nie powiódł się. Dane mogą być nieaktualne.",14,Ui.Hex("#F7BA61")));p.Children.Add(Ui.Button("Zamknij",window.Close));window.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};window.ShowDialog();
 }
 internal static string WindowLabel(string name)=>name switch {"5-hour window" or "5 hours" or "Codex - 5 hours"=>"Limit 5 h","Weekly" or "7 days" or "Codex - 7 days"=>"Tygodniowy",_=>name};
 private async void Refresh(){await controller.RefreshAsync();}
 internal static string Money(decimal? v)=>v.HasValue?v.Value.ToString("0.##",CultureInfo.GetCultureInfo("pl-PL"))+" USD":"—";
 internal static string Tokens(long? v)=>v.HasValue?v.Value.ToString("N0",CultureInfo.GetCultureInfo("pl-PL")).Replace('\u00a0',' '):"–";
 internal static string ResetText(DateTimeOffset? reset)=>UsageTime.Relative(reset,DateTimeOffset.UtcNow);
 public static string ProviderLabel(ProviderKind p)=>p switch{ProviderKind.ClaudeOAuth=>"Claude",ProviderKind.ClaudeSession=>"Claude",ProviderKind.Codex=>"Codex",ProviderKind.AnthropicApi=>"Anthropic API",_=>"OpenAI API"};
 public void Edit(ConnectionProfile? profile)=>new ConnectionEditor(controller,profile){Owner=this}.ShowDialog();
 public static void Safe(Action action){try{action();}catch{MessageBox.Show("Nie udało się zapisać zmiany. Sprawdź dostęp do lokalnego magazynu i spróbuj ponownie.","UsageDock",MessageBoxButton.OK,MessageBoxImage.Warning);}}
}
internal static class StartupIntegration
{
 public static void Set(bool enabled){using var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)key.SetValue("UsageDock","\""+Environment.ProcessPath+"\"");else key.DeleteValue("UsageDock",false);}
}
