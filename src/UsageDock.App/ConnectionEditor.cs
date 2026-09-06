using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsageDock.Core;
namespace UsageDock.App;
public sealed class ConnectionEditor : Window
{
    private readonly DockController controller;
    private readonly ConnectionProfile? existing;
    private readonly TextBox name=new(),account=new(),scope=new(),budget=new();
    private readonly PasswordBox secret=new();
    private readonly ComboBox provider=new(){ItemsSource=Enum.GetValues<ProviderKind>().Select(p=>new ProviderOption(p,p switch { ProviderKind.ClaudeOAuth=>"Claude — abonament (CLI)",ProviderKind.ClaudeSession=>"Claude — sesja organizacji",ProviderKind.Codex=>"Codex / konto ChatGPT",ProviderKind.AnthropicApi=>"Anthropic API",_=>"OpenAI API"})).ToArray(),SelectedValuePath="Key",DisplayMemberPath="Value"};
    private readonly System.Collections.Generic.Dictionary<UIElement,TextBlock> fieldLabels=new();
    private Button? importButton,discoverButton;
    private readonly TextBlock help=Ui.Label("",12,Ui.Muted),error=Ui.Label("",13,Ui.Hex("#E99685"));
    public ConnectionEditor(DockController controller,ConnectionProfile? existing)
    {
        this.controller=controller;this.existing=existing;Ui.Window(this,existing==null?"Dodaj połączenie · UsageDock":"Edytuj połączenie · UsageDock",560,790);MinWidth=470;MinHeight=560;
        name.Text=existing?.Name??"";account.Text=existing?.AccountId??"";scope.Text=existing?.ScopeId??"";budget.Text=existing?.MonthlyBudget?.ToString(CultureInfo.InvariantCulture)??"";provider.SelectedValue=existing?.Provider??ProviderKind.ClaudeOAuth;provider.IsEnabled=existing==null;
        provider.SelectionChanged+=(_,_)=>UpdateHelp();UpdateHelp();
        var stack=Ui.Stack(Ui.Label(existing==null?"Połącz swoje konto":"Szczegóły połączenia",26),Ui.Label("Osobne, szyfrowane połączenie na tym komputerze.",13,Ui.Muted));
        foreach(var pair in new (string,UIElement)[]{("Nazwa konta",name),("Dostawca",provider)}){var label=Ui.Label(pair.Item1,12,Ui.Muted);fieldLabels[pair.Item2]=label;stack.Children.Add(label);stack.Children.Add(pair.Item2);}
        stack.Children.Add(help);stack.Children.Add(Ui.Label(existing==null?"Poświadczenie":"Poświadczenie · puste pole zachowa zapisane",12,Ui.Muted));stack.Children.Add(secret);
        importButton=Ui.Button("Importuj poświadczenia JSON…",Import);stack.Children.Add(importButton);
        foreach(var pair in new (string,UIElement)[]{("Identyfikator konta / organizacji",account),("Identyfikator obszaru / projektu (filtr API)",scope),("Miesięczny lokalny budżet USD (opcjonalny, API)",budget)}){var label=Ui.Label(pair.Item1,12,Ui.Muted);fieldLabels[pair.Item2]=label;stack.Children.Add(label);stack.Children.Add(pair.Item2);}
        discoverButton=Ui.Button("Znajdź obszary / organizacje",Discover);stack.Children.Add(discoverButton);stack.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};buttons.Children.Add(Ui.Button("Zapisz połączenie",Save,true));buttons.Children.Add(Ui.Button("Anuluj",Close));
        if(existing!=null)buttons.Children.Add(Ui.Button("Usuń",()=>{if(MessageBox.Show("Usunąć połączenie i zapisane poświadczenie?","Usuń połączenie",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){try{controller.Remove(existing.Id);Close();}catch{error.Text="Nie udało się usunąć połączenia z lokalnego magazynu.";}}}));
        stack.Children.Add(buttons);Content=new ScrollViewer{Content=new Border{Padding=new Thickness(26),Child=stack},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Closed+=(_,_)=>secret.Clear();UpdateHelp();
    }
    private void UpdateHelp()
    {
        var kind=(ProviderKind)provider.SelectedValue;
        help.Text=kind switch{ProviderKind.ClaudeOAuth=>"Eksperymentalny odczyt Claude OAuth. Wklej token dostępu OAuth lub importuj poświadczenia Claude Code. Wygasły token wymaga ponownego połączenia.",ProviderKind.ClaudeSession=>"Eksperymentalny odczyt sesji Claude. Podaj sessionKey i identyfikator organizacji lub wyszukaj organizacje. Sesje mogą wygasać.",ProviderKind.Codex=>"Eksperymentalny odczyt Codex z konta ChatGPT. Importuj auth.json Codex lub podaj token dostępu i identyfikator konta. To nie są ogólne limity rozmów ChatGPT.",ProviderKind.AnthropicApi=>"Wymagany klucz Anthropic Admin API. Obszar roboczy jest opcjonalny. Koszt i tokeny wiadomości są niezależne od limitów abonamentu.",_=>"Wymagany klucz OpenAI Admin API organizacji. Projekt jest opcjonalny. Tokeny Completions nie obejmują wszystkich produktów API."};
        ShowField(account,kind is ProviderKind.ClaudeSession or ProviderKind.Codex);ShowField(scope,DockController.IsApi(kind));ShowField(budget,DockController.IsApi(kind));
        if(importButton!=null)importButton.Visibility=kind is ProviderKind.ClaudeOAuth or ProviderKind.Codex?Visibility.Visible:Visibility.Collapsed;
        if(discoverButton!=null)discoverButton.Visibility=kind is ProviderKind.ClaudeSession or ProviderKind.Codex?Visibility.Visible:Visibility.Collapsed;
    }
    private void ShowField(UIElement field,bool show){field.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(fieldLabels.TryGetValue(field,out var label))label.Visibility=field.Visibility;}
    private void Import()
    {
        var dialog=new Microsoft.Win32.OpenFileDialog{Title="Wybierz plik poświadczeń",Filter="Poświadczenia JSON (*.json)|*.json",CheckFileExists=true};
        if(dialog.ShowDialog(this)!=true)return;
        try{if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidOperationException();var parsed=CredentialImporter.Parse(File.ReadAllText(dialog.FileName),(ProviderKind)provider.SelectedValue);secret.Password=parsed.Secret;if(parsed.AccountId!=null)account.Text=parsed.AccountId;error.Text="Zaimportowano poświadczenie. Zapisz, aby zachować szyfrowaną kopię.";}catch{error.Text="Ten plik nie zawiera obsługiwanych poświadczeń wybranego dostawcy.";}
    }
    private ConnectionProfile Profile()=>new(existing?.Id??Guid.NewGuid(),name.Text.Trim(),(ProviderKind)provider.SelectedValue,Null(account.Text),DockController.IsApi((ProviderKind)provider.SelectedValue)?Null(scope.Text):null,decimal.TryParse(budget.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)&&DockController.IsApi((ProviderKind)provider.SelectedValue)?value:null,existing?.IsFavorite??true);
    private static string? Null(string s)=>string.IsNullOrWhiteSpace(s)?null:s.Trim();
    private void Save()
    {
        if(!string.IsNullOrWhiteSpace(budget.Text)&&DockController.IsApi((ProviderKind)provider.SelectedValue)&&(!decimal.TryParse(budget.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)||amount<=0)){error.Text="Wpisz dodatni budżet USD lub pozostaw pole puste.";return;}
        if(existing!=null&&existing.Provider!=(ProviderKind)provider.SelectedValue&&string.IsNullOrWhiteSpace(secret.Password)){error.Text="Przy zmianie dostawcy podaj nowe poświadczenie.";return;}
        try{var profile=Profile();var credential=Null(secret.Password)??(existing!=null?controller.ReadSecret(existing.Id):null);var validation=ProfileValidator.Validate(profile,credential);if(validation!=null){error.Text=validation;return;}controller.Save(profile,Null(secret.Password));Close();_ = controller.RefreshAsync();}catch{error.Text="Nie udało się zapisać połączenia. Sprawdź dostęp do magazynu i poświadczenia.";}
    }
    private async void Discover()
    {
        if(controller.Offline){error.Text="Wyszukiwanie jest wyłączone w demonstracji offline.";return;}
        try
        {
            var credential=Null(secret.Password)??(existing!=null?controller.ReadSecret(existing.Id):null);
            if(credential==null){error.Text="Najpierw podaj poświadczenie.";return;}
            error.Text="Wyszukiwanie dostępnych obszarów…";
            using var service=new ProviderService();var options=await service.DiscoverAsync(Profile(),credential);
            if(options.Count==0){error.Text="Nie znaleziono obszarów. Jeśli to możliwe, wpisz identyfikator ręcznie.";return;}
            var picker=new Window{Owner=this};Ui.Window(picker,"Wybierz obszar",440,220);
            var list=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedIndex=0};
            picker.Content=new Border{Padding=new Thickness(24),Child=Ui.Stack(Ui.Label("Wybierz obszar roboczy",22),list,Ui.Button("Użyj wybranego",()=>{var selected=(WorkspaceOption)list.SelectedItem;if(DockController.IsApi((ProviderKind)provider.SelectedValue))scope.Text=selected.Id;else account.Text=selected.Id;picker.Close();},true))};picker.ShowDialog();error.Text="";
        }
        catch{error.Text="Wyszukiwanie niedostępne. Sprawdź uprawnienia lub wpisz identyfikator ręcznie.";}
    }
}




internal sealed record ProviderOption(ProviderKind Key,string Value){public override string ToString()=>Value;}
