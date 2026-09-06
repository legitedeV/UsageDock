using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UsageDock.Core;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    private string draftTheme = "Dark";
    private string draftInterval = "300";
    private bool draftAlerts;
    private bool draftStartup;
    private Button? saveSettings;
    private Button? revertSettings;
    private TextBlock? settingsStatus;
    private string savedMessage = "";

    private void InitializeDraft()
    {
        draftTheme = controller.Settings.Theme;
        draftInterval = controller.Settings.RefreshSeconds.ToString();
        draftAlerts = controller.Settings.NotificationsEnabled;
        draftStartup = controller.Settings.StartWithWindows;
    }
    private bool IsDirty => draftInterval != controller.Settings.RefreshSeconds.ToString()
        || draftAlerts != controller.Settings.NotificationsEnabled
        || draftStartup != controller.Settings.StartWithWindows;
    private bool ValidInterval => int.TryParse(draftInterval, out var seconds) && seconds is >= 120 and <= 86400;
    private void UpdateDraftState()
    {
        if (saveSettings == null || revertSettings == null || settingsStatus == null) return;
        saveSettings.IsEnabled = IsDirty && ValidInterval;
        revertSettings.IsEnabled = IsDirty;
        settingsStatus.Text = !ValidInterval ? "Wpisz liczbę od 120 do 86400 sekund." : IsDirty ? "Masz niezapisane zmiany" : string.IsNullOrEmpty(savedMessage) ? "Wszystkie zmiany zapisane" : savedMessage;
        settingsStatus.Foreground = !ValidInterval ? Warning : IsDirty ? Ui.Text : Ui.Muted;
    }
    private UIElement SettingsPanel()
    {
        var page = Page("Ustawienia", "Motyw zmienia się od razu. Pozostałe preferencje zatwierdź przyciskiem Zapisz ustawienia.");
        page.Children.Add(SectionTitle("Wygląd"));
        var choices = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in new[] { ("Dark", "Ciemny"), ("Light", "Jasny") })
        {
            var choice = Identify(new RadioButton { Content = label, GroupName = "Appearance", IsChecked = draftTheme == value,
                MinWidth = 112, MinHeight = 38, Padding = new Thickness(10), Margin = new Thickness(0, 0, 8, 0) }, "Settings.Theme." + value);
            choice.Checked += (_, _) => { TrySetTheme(value == "Light"); RestoreFocus("Settings.Theme." + value); };
            choices.Children.Add(choice);
        }
        page.Children.Add(Surface(SettingRow("Motyw aplikacji", "Zapisuje się od razu. Obejmuje główne okno i widżet.", choices)));
        page.Children.Add(SectionTitle("Odświeżanie i powiadomienia"));
        var refresh = new StackPanel();
        var interval = Identify(new TextBox { Text = draftInterval, Width = 96, Height = 38,
            VerticalContentAlignment = VerticalAlignment.Center }, "Settings.Interval");
        System.Windows.Automation.AutomationProperties.SetName(interval, "Interwał odświeżania w sekundach");
        interval.TextChanged += (_, _) => { draftInterval = interval.Text; savedMessage = ""; UpdateDraftState(); };
        var intervalControl = new StackPanel { Orientation = Orientation.Horizontal };
        intervalControl.Children.Add(interval);
        intervalControl.Children.Add(new TextBlock { Text = "sekund", Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
        refresh.Children.Add(SettingRow("Automatyczny odczyt", "Od 120 sekund do 24 godzin. Rzadsze odczyty ograniczają liczbę zapytań.", intervalControl));
        refresh.Children.Add(new Border { Height = 1, Background = Design.Line, Margin = new Thickness(0, 16, 0, 16) });
        refresh.Children.Add(SettingRow("Powiadomienia o zużyciu", "Przy 80%, 95%, wyczerpaniu limitu i ponownej dostępności.",
            SettingToggle("Settings.Notifications", "Powiadomienia o zużyciu", draftAlerts, value => draftAlerts = value)));
        page.Children.Add(Surface(refresh));
        page.Children.Add(SectionTitle("Uruchamianie"));
        var startup = SettingToggle("Settings.Startup", "Uruchamiaj z Windows", draftStartup, value => draftStartup = value);
        startup.IsEnabled = !controller.Offline;
        page.Children.Add(Surface(SettingRow("Uruchamiaj z Windows", controller.Offline ? "Niedostępne w trybie demonstracyjnym." : "UsageDock będzie gotowy po zalogowaniu do komputera.", startup)));
        page.Children.Add(SectionTitle("Widżet"));
        var widget = Ui.Button("Otwórz mini widget", () => WidgetRequested?.Invoke());
        widget.Margin = new Thickness(0); widget.Height = 38;
        page.Children.Add(Surface(SettingRow("Podgląd na pulpicie", "Przypnij wybrane konta gwiazdką w zakładce Konta.", widget)));
        return page;
    }
    private UIElement SettingsSaveBar()
    {
        var bar = Columns(-1, 132, 176);
        bar.Margin = new Thickness(32, 10, 32, 8);
        settingsStatus = Identify(new TextBlock { FontSize = 14, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, "Settings.Status");
        saveSettings = Identify(Ui.Button("Zapisz ustawienia", SaveDraft, true), "Settings.Save");
        revertSettings = Identify(Ui.Button("Cofnij zmiany", () => { InitializeDraft(); savedMessage = "Przywrócono zapisane ustawienia"; Build(); }), "Settings.Revert");
        saveSettings.Height = revertSettings.Height = 42;
        Cell(bar, settingsStatus, 0); Cell(bar, revertSettings, 1); Cell(bar, saveSettings, 2);

        UpdateDraftState();
        return bar;
    }
    private ToggleButton SettingToggle(string id, string label, bool value, Action<bool> changed)
    {
        var toggle = Identify(new CheckBox { Content = "Włączone", IsChecked = value, MinHeight = 38, MinWidth = 140,
            VerticalAlignment = VerticalAlignment.Center }, id);
        System.Windows.Automation.AutomationProperties.SetName(toggle, label);
        void Change() { changed(toggle.IsChecked == true); toggle.Content = toggle.IsChecked == true ? "Włączone" : "Wyłączone"; savedMessage = ""; UpdateDraftState(); }
        toggle.Checked += (_, _) => Change(); toggle.Unchecked += (_, _) => Change();
        toggle.Content = value ? "Włączone" : "Wyłączone";
        return toggle;
    }
    private static UIElement SettingRow(string title, string description, UIElement control)
    {
        var row = Columns(-1, 256);
        var text = Ui.Stack(Ui.Label(title, 16), Ui.Label(description, 13, Ui.Muted));
        text.Margin = new Thickness(0, 0, 28, 0);
        Cell(row, text, 0);
        var holder = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        holder.Children.Add(control); Cell(row, holder, 1);
        return row;
    }
    private void SaveDraft()
    {
        if (!ValidInterval || !IsDirty) return;
        var previousStartup = controller.Settings.StartWithWindows;
        var startupChanged = false;
        try
        {
            if (!controller.Offline && draftStartup != previousStartup) { StartupIntegration.Set(draftStartup); startupChanged = true; }
            controller.SetSettings(new AppSettings(draftTheme, int.Parse(draftInterval), draftAlerts, draftStartup));
            InitializeDraft();
            savedMessage = "Ustawienia zapisane";
            Build();
        }
        catch
        {
            if (startupChanged) { try { StartupIntegration.Set(previousStartup); } catch { if (settingsStatus != null) settingsStatus.ToolTip = "Sprawdź ustawienia autostartu Windows."; } }
            if (settingsStatus != null) { settingsStatus.Text = "Nie udało się zapisać. Sprawdź dostęp do lokalnego magazynu i spróbuj ponownie."; settingsStatus.Foreground = Warning; }
        }
    }
}
