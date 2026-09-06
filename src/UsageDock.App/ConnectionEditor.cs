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
    private readonly ComboBox provider=new(){ItemsSource=Enum.GetValues<ProviderKind>().Select(p=>new ProviderOption(p,p switch { ProviderKind.ClaudeOAuth=>Ui.L("Claude — abonament (CLI)"),ProviderKind.ClaudeSession=>Ui.L("Claude — sesja organizacji"),ProviderKind.Codex=>Ui.L("Codex / konto ChatGPT"),ProviderKind.AnthropicApi=>"Anthropic API",_=>"OpenAI API"})).ToArray(),SelectedValuePath="Key",DisplayMemberPath="Value"};
    private readonly System.Collections.Generic.Dictionary<UIElement,TextBlock> fieldLabels=new();
    private Button? importButton,discoverButton;
    private readonly TextBlock help=Ui.Label("",12,Ui.Muted),error=Ui.Label("",13,Ui.Hex("#E99685"));
    private string errorKey="";
    private string editorLanguage=Localization.CurrentLanguage;
    private void SetError(string key){errorKey=key;error.Text=Ui.L(key);}
    public ConnectionEditor(DockController controller,ConnectionProfile? existing)
    {
        this.controller=controller;this.existing=existing;Ui.Window(this,existing==null?Ui.L("Dodaj połączenie · UsageDock"):Ui.L("Edytuj połączenie · UsageDock"),560,790);MinWidth=470;MinHeight=560;
        System.Windows.Automation.AutomationProperties.SetAutomationId(name,"Editor.Name");
        System.Windows.Automation.AutomationProperties.SetAutomationId(budget,"Editor.Budget");
        System.Windows.Automation.AutomationProperties.SetAutomationId(secret,"Editor.Secret");
        name.Text=existing?.Name??"";account.Text=existing?.AccountId??"";scope.Text=existing?.ScopeId??"";budget.Text=existing?.MonthlyBudget?.ToString(Localization.Culture)??"";provider.SelectedValue=existing?.Provider??ProviderKind.ClaudeOAuth;provider.IsEnabled=existing==null;
        provider.SelectionChanged+=(_,_)=>UpdateHelp();UpdateHelp();
        BuildEditor();Localization.Changed+=OnLanguageChanged;
        Closed+=(_,_)=>{secret.Clear();Localization.Changed-=OnLanguageChanged;};
    }
    private void OnLanguageChanged()
    {
        if(Localization.TryParseBudget(budget.Text,editorLanguage,out var amount)&&amount.HasValue)budget.Text=amount.Value.ToString(Localization.Culture);
        var focused=System.Windows.Input.Keyboard.FocusedElement as UIElement;
        editorLanguage=Localization.CurrentLanguage;BuildEditor();
        if(focused!=null)Dispatcher.BeginInvoke(new Action(()=>focused.Focus()));
    }
    private void BuildEditor()
    {
        var selected=provider.SelectedValue;
        foreach(var element in new FrameworkElement[]{name,account,scope,budget,secret,provider,help,error})if(element.Parent is Panel parent)parent.Children.Remove(element);
        fieldLabels.Clear();
        Title=existing==null?Ui.L("Dodaj połączenie · UsageDock"):Ui.L("Edytuj połączenie · UsageDock");
        provider.ItemsSource=Enum.GetValues<ProviderKind>().Select(p=>new ProviderOption(p,p switch {ProviderKind.ClaudeOAuth=>Ui.L("Claude — abonament (CLI)"),ProviderKind.ClaudeSession=>Ui.L("Claude — sesja organizacji"),ProviderKind.Codex=>Ui.L("Codex / konto ChatGPT"),ProviderKind.AnthropicApi=>"Anthropic API",_=>"OpenAI API"})).ToArray();
        provider.SelectedValue=selected;
        error.Text=Ui.L(errorKey);
        var stack=Ui.Stack(Ui.Label(existing==null?Ui.L("Połącz swoje konto"):Ui.L("Szczegóły połączenia"),26),Ui.Label(Ui.L("Osobne, szyfrowane połączenie na tym komputerze."),13,Ui.Muted));
        foreach(var pair in new (string,UIElement)[]{(Ui.L("Nazwa konta"),name),(Ui.L("Dostawca"),provider)}){var label=Ui.Label(pair.Item1,12,Ui.Muted);fieldLabels[pair.Item2]=label;stack.Children.Add(label);stack.Children.Add(pair.Item2);}
        stack.Children.Add(help);stack.Children.Add(Ui.Label(existing==null?Ui.L("Poświadczenie"):Ui.L("Poświadczenie · puste pole zachowa zapisane"),12,Ui.Muted));stack.Children.Add(secret);
        importButton=Ui.Button(Ui.L("Importuj poświadczenia JSON…"),Import);stack.Children.Add(importButton);
        foreach(var pair in new (string,UIElement)[]{(Ui.L("Identyfikator konta / organizacji"),account),(Ui.L("Identyfikator obszaru / projektu (filtr API)"),scope),(Ui.L("Miesięczny lokalny budżet USD (opcjonalny, API)"),budget)}){var label=Ui.Label(pair.Item1,12,Ui.Muted);fieldLabels[pair.Item2]=label;stack.Children.Add(label);stack.Children.Add(pair.Item2);}
        discoverButton=Ui.Button(Ui.L("Znajdź obszary / organizacje"),Discover);stack.Children.Add(discoverButton);stack.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};buttons.Children.Add(Ui.Button(Ui.L("Zapisz połączenie"),Save,true));buttons.Children.Add(Ui.Button(Ui.L("Anuluj"),Close));
        if(existing!=null)buttons.Children.Add(Ui.Button(Ui.L("Usuń"),()=>{if(MessageBox.Show(Ui.L("Usunąć połączenie i zapisane poświadczenie?"),Ui.L("Usuń połączenie"),MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){try{controller.Remove(existing.Id);Close();}catch{SetError("Nie udało się usunąć połączenia z lokalnego magazynu.");}}}));
        stack.Children.Add(buttons);Content=new ScrollViewer{Content=new Border{Padding=new Thickness(26),Child=stack},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};UpdateHelp();
    }
    private void UpdateHelp()
    {
        if(provider.SelectedValue is not ProviderKind kind)return;
        help.Text=kind switch{ProviderKind.ClaudeOAuth=>Ui.L("Eksperymentalny odczyt Claude OAuth. Wklej token dostępu OAuth lub importuj poświadczenia Claude Code. Wygasły token wymaga ponownego połączenia."),ProviderKind.ClaudeSession=>Ui.L("Eksperymentalny odczyt sesji Claude. Podaj sessionKey i identyfikator organizacji lub wyszukaj organizacje. Sesje mogą wygasać."),ProviderKind.Codex=>Ui.L("Eksperymentalny odczyt Codex z konta ChatGPT. Importuj auth.json Codex lub podaj token dostępu i identyfikator konta. To nie są ogólne limity rozmów ChatGPT."),ProviderKind.AnthropicApi=>Ui.L("Wymagany klucz Anthropic Admin API. Obszar roboczy jest opcjonalny. Koszt i tokeny wiadomości są niezależne od limitów abonamentu."),_=>Ui.L("Wymagany klucz OpenAI Admin API organizacji. Projekt jest opcjonalny. Tokeny Completions nie obejmują wszystkich produktów API.")};
        ShowField(account,kind is ProviderKind.ClaudeSession or ProviderKind.Codex);ShowField(scope,DockController.IsApi(kind));ShowField(budget,DockController.IsApi(kind));
        if(importButton!=null)importButton.Visibility=kind is ProviderKind.ClaudeOAuth or ProviderKind.Codex?Visibility.Visible:Visibility.Collapsed;
        if(discoverButton!=null)discoverButton.Visibility=kind is ProviderKind.ClaudeSession or ProviderKind.Codex?Visibility.Visible:Visibility.Collapsed;
    }
    private void ShowField(UIElement field,bool show){field.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(fieldLabels.TryGetValue(field,out var label))label.Visibility=field.Visibility;}
    private void Import()
    {
        var dialog=new Microsoft.Win32.OpenFileDialog{Title=Ui.L("Wybierz plik poświadczeń"),Filter=Ui.L("Poświadczenia JSON (*.json)|*.json"),CheckFileExists=true};
        if(dialog.ShowDialog(this)!=true)return;
        try{if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidOperationException();var parsed=CredentialImporter.Parse(File.ReadAllText(dialog.FileName),(ProviderKind)provider.SelectedValue);secret.Password=parsed.Secret;if(parsed.AccountId!=null)account.Text=parsed.AccountId;SetError("Zaimportowano poświadczenie. Zapisz, aby zachować szyfrowaną kopię.");}catch{SetError("Ten plik nie zawiera obsługiwanych poświadczeń wybranego dostawcy.");}
    }
    private ConnectionProfile Profile()=>new(existing?.Id??Guid.NewGuid(),name.Text.Trim(),(ProviderKind)provider.SelectedValue,Null(account.Text),DockController.IsApi((ProviderKind)provider.SelectedValue)?Null(scope.Text):null,Localization.TryParseBudget(budget.Text,Localization.CurrentLanguage,out var value)&&DockController.IsApi((ProviderKind)provider.SelectedValue)?value:null,existing?.IsFavorite??true);
    private static string? Null(string s)=>string.IsNullOrWhiteSpace(s)?null:s.Trim();
    private void Save()
    {
        if(!string.IsNullOrWhiteSpace(budget.Text)&&DockController.IsApi((ProviderKind)provider.SelectedValue)&&!Localization.TryParseBudget(budget.Text,Localization.CurrentLanguage,out _)){SetError("Wpisz dodatni budżet USD lub pozostaw pole puste.");return;}
        if(existing!=null&&existing.Provider!=(ProviderKind)provider.SelectedValue&&string.IsNullOrWhiteSpace(secret.Password)){SetError("Przy zmianie dostawcy podaj nowe poświadczenie.");return;}
        try{var profile=Profile();var credential=Null(secret.Password)??(existing!=null?controller.ReadSecret(existing.Id):null);var validation=ProfileValidator.Validate(profile,credential);if(validation!=null){SetError(validation);return;}controller.Save(profile,Null(secret.Password));Close();_ = controller.RefreshAsync();}catch{SetError("Nie udało się zapisać połączenia. Sprawdź dostęp do magazynu i poświadczenia.");}
    }
    private async void Discover()
    {
        if(controller.Offline){SetError("Wyszukiwanie jest wyłączone w demonstracji offline.");return;}
        try
        {
            var credential=Null(secret.Password)??(existing!=null?controller.ReadSecret(existing.Id):null);
            if(credential==null){SetError("Najpierw podaj poświadczenie.");return;}
            SetError("Wyszukiwanie dostępnych obszarów…");
            using var service=new ProviderService();var options=await service.DiscoverAsync(Profile(),credential);
            if(options.Count==0){SetError("Nie znaleziono obszarów. Jeśli to możliwe, wpisz identyfikator ręcznie.");return;}
            var picker=new Window{Owner=this};Ui.Window(picker,Ui.L("Wybierz obszar"),440,220);
            var list=new ComboBox{ItemsSource=options,DisplayMemberPath="Name",SelectedIndex=0};
            picker.Content=new Border{Padding=new Thickness(24),Child=Ui.Stack(Ui.Label(Ui.L("Wybierz obszar roboczy"),22),list,Ui.Button(Ui.L("Użyj wybranego"),()=>{var selected=(WorkspaceOption)list.SelectedItem;if(DockController.IsApi((ProviderKind)provider.SelectedValue))scope.Text=selected.Id;else account.Text=selected.Id;picker.Close();},true))};picker.ShowDialog();SetError("");
        }
        catch{SetError("Wyszukiwanie niedostępne. Sprawdź uprawnienia lub wpisz identyfikator ręcznie.");}
    }
}




internal sealed record ProviderOption(ProviderKind Key,string Value){public override string ToString()=>Value;}
