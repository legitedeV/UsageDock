using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsageDock.Core;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    private sealed record SessionEvent(DateTimeOffset At, string Kind, string Account, string Description);
    private IReadOnlyList<SessionEvent> history = Array.Empty<SessionEvent>();
    private AccountView[] previousAccounts = Array.Empty<AccountView>();
    private AppSettings previousSettings = new();
    private bool previousRefreshing;
    private string historyFilter = "All";

    private void AddEvent(string kind, string account, string description)
    {
        history = new[] { new SessionEvent(DateTimeOffset.Now, kind, account, description) }
            .Concat(history).Take(100).ToArray();
    }
    private void RecordChanges()
    {
        foreach (var account in controller.Accounts)
        {
            var old = previousAccounts.FirstOrDefault(a => a.Profile.Id == account.Profile.Id);
            if (old == null) AddEvent("Account", account.Profile.Name, "Dodano połączenie");
            else
            {
                if (old.Profile.IsFavorite != account.Profile.IsFavorite) AddEvent("Account", account.Profile.Name, account.Profile.IsFavorite ? "Przypięto do widżetu" : "Odpięto od widżetu");
                else if (old.Profile != account.Profile || old.Generation != account.Generation) AddEvent("Account", account.Profile.Name, "Zmieniono ustawienia połączenia");
                if (account.Error != null && (old.Error != account.Error || old.Snapshot != account.Snapshot || old.RetryAfter != account.RetryAfter))
                    AddEvent("Problems", account.Profile.Name, account.Snapshot == null ? "Odczyt niedostępny. Sprawdź połączenie." : "Odczyt nie powiódł się. Zachowano poprzednie dane.");
                else if (account.Error == null && account.Snapshot != null && (account.Snapshot != old.Snapshot || old.Error != null))
                    AddEvent("Reads", account.Profile.Name, "Pobrano aktualny odczyt");
            }
        }
        foreach (var removed in previousAccounts.Where(a => controller.Accounts.All(b => b.Profile.Id != a.Profile.Id)))
            AddEvent("Account", removed.Profile.Name, "Usunięto połączenie");
        if (previousRefreshing && !controller.IsRefreshing)
            AddEvent("Session", "Wszystkie konta", "Zakończono próbę odświeżania");
        if (previousSettings != controller.Settings) AddEvent("Settings", "Aplikacja", "Zapisano ustawienia");
        previousAccounts = controller.Accounts.ToArray();
        previousSettings = controller.Settings;
        previousRefreshing = controller.IsRefreshing;
    }
    private UIElement HistoryPanel()
    {
        var page = Page("Historia", "Zdarzenia z bieżącej sesji aplikacji. Zachowujemy ostatnie 100 wpisów; po zamknięciu historia jest czyszczona.");
        var toolbar = Columns(-1, 160);
        toolbar.Margin = new Thickness(0, 0, 0, 20);
        var filters = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (key, label) in new[] { ("All", "Wszystkie"), ("Reads", "Odczyty"), ("Problems", "Problemy") })
        {
            var button = Identify(Ui.Button(label, () => { historyFilter = key; Repaint(); RestoreFocus("History.Filter." + key); }), "History.Filter." + key);
            button.Height = 38;
            if (historyFilter == key) { button.BorderBrush = Ui.Accent; button.Foreground = Ui.Accent; }
            filters.Children.Add(button);
        }
        Cell(toolbar, filters, 0);
        var clear = Identify(Ui.Button("Wyczyść sesję", () => { history = Array.Empty<SessionEvent>(); Repaint(); }), "History.Clear");
        clear.IsEnabled = history.Count > 0;
        clear.Height = 38;
        Cell(toolbar, clear, 1);
        page.Children.Add(toolbar);
        var headings = Columns(112, 124, 228, -1);
        headings.Margin = new Thickness(18, 0, 18, 12);
        foreach (var (label, column) in new[] { ("Czas", 0), ("Zdarzenie", 1), ("Konto", 2), ("Szczegóły", 3) })
            Cell(headings, Truncated(label, 13, Ui.Muted), column);
        page.Children.Add(headings);
        var entries = Identify(new StackPanel(), "History.Entries");
        var filtered = history.Where(h => historyFilter == "All" || h.Kind == historyFilter).ToArray();
        foreach (var entry in filtered)
        {
            var row = Columns(112, 124, 228, -1);
            row.Margin = new Thickness(18, 17, 18, 17);
            Cell(row, Truncated(entry.At.ToString("HH:mm:ss"), 13, Ui.Muted), 0);
            Cell(row, Truncated(entry.Kind switch { "Problems" => "Problem", "Reads" => "Odczyt", "Settings" => "Ustawienia", "Session" => "Odświeżanie", _ => "Połączenie" }, 13, entry.Kind == "Problems" ? Warning : Ui.Accent), 1);
            var account = Truncated(entry.Account); account.Margin = new Thickness(0, 0, 16, 0);
            Cell(row, account, 2);
            Cell(row, new TextBlock { Text = entry.Description, Foreground = Ui.Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap }, 3);
            entries.Children.Add(new Border { Child = row, BorderBrush = Design.Line, BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        page.Children.Add(new Border { Child = entries, Background = Ui.Surface, CornerRadius = new CornerRadius(8) });
        if (filtered.Length == 0)
            page.Children.Add(Identify(Surface(Ui.Stack(Ui.Label("Brak zdarzeń", 18), Ui.Label(historyFilter == "All" ? "Nowe odczyty i zmiany połączeń pojawią się tutaj." : "W tej sesji nie ma zdarzeń wybranego rodzaju.", 14, Ui.Muted))), "History.Empty"));
        return page;
    }
}
