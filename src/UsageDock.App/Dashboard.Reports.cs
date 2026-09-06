using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using UsageDock.Core;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    private Button EmptyAddButton()
    {
        var button = Ui.Button(Ui.L("Dodaj połączenie"), () => Edit(null), true);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Width = 180; button.Height = 40;
        return button;
    }
    private void RestoreFocus(string id)
    {
        Dispatcher.BeginInvoke(new Action(() => FindFocus(this, id)?.Focus()));
    }
    private static FrameworkElement? FindFocus(DependencyObject parent, string id)
    {
        if (parent is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id) return element;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var match = FindFocus(VisualTreeHelper.GetChild(parent, index), id);
            if (match != null) return match;
        }
        return null;
    }    private static T Identify<T>(T element, string id) where T : DependencyObject
    {
        AutomationProperties.SetAutomationId(element, id);
        return element;
    }
    private static StackPanel Page(string title, string description)
    {
        var page = new StackPanel { Margin = new Thickness(32, 14, 32, 24) };
        page.Children.Add(new TextBlock { Text = Ui.L(title), FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Ui.Text });
        page.Children.Add(new TextBlock { Text = Ui.L(description), FontSize = 14, Foreground = Ui.Muted,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 24) });
        return page;
    }
    private static TextBlock SectionTitle(string title) => new()
    {
        Text = Ui.L(title), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Ui.Text,
        Margin = new Thickness(0, 16, 0, 12)
    };
    private static Border Surface(UIElement content) => new()
    {
        Child = content, Background = Ui.Surface, BorderBrush = Design.Line,
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12)
    };
    private static Grid Columns(params double[] widths)
    {
        var grid = new Grid();
        foreach (var width in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(-width, GridUnitType.Star) : new GridLength(width) });
        return grid;
    }
    private static void Cell(Grid grid, UIElement child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }
    private static TextBlock Truncated(string text, double size = 14, Brush? color = null)
        => new() { Text = text, FontSize = size, Foreground = color ?? Ui.Text,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = text, VerticalAlignment = VerticalAlignment.Center };
    private static Brush Warning => Ui.Hex(Ui.Light ? "#8A3E16" : "#F7BA61");
    private static string Health(AccountView a) => a.Error != null
        ? a.Snapshot == null ? Ui.L("Wymaga uwagi") : Ui.L("Nieaktualne")
        : a.Snapshot == null ? Ui.L("Oczekiwanie") : Ui.L("Aktualne");
    private static Brush HealthColor(AccountView a) => a.Error != null ? Warning : a.Snapshot == null ? Ui.Muted : Ui.Accent;
    private static string AccountNumber(int count)
    {
        var ending = count % 10;
        var teen = count % 100;
        return Ui.L(count == 1 ? "accounts.one" : Localization.CurrentLanguage=="pl" && ending is >= 2 and <= 4 && teen is not (>= 12 and <= 14) ? "accounts.few" : "accounts.many",count);
    }
    private static UIElement StatusLine(AccountView account)
    {
        var line = Columns(-1, 160);
        var fetched = account.Snapshot?.FetchedAt.ToLocalTime().ToString("g",Localization.Culture);
        Cell(line, Truncated(ProviderLabel(account.Profile.Provider) + (fetched == null ? Ui.L(" · Brak odczytu") : " · " + fetched), 13, Ui.Muted), 0);
        var status = Truncated(Health(account), 13, HealthColor(account));
        status.TextAlignment = TextAlignment.Right;
        Cell(line, status, 1);
        return line;
    }
    private UIElement StatisticsPanel()
    {
        var page = Page("Statystyki", Ui.L("Ostatni odczyt każdego konta. Limity i koszty pozostają osobne — zakresy organizacji mogą się pokrywać."));
        var all = controller.Accounts;
        var summary = Identify(new TextBlock {
            Text = Ui.L("statistics.summary",AccountNumber(all.Count),all.Count(a => a.Snapshot != null && a.Error == null),all.Count(a => a.Error != null),all.Count(a => a.Snapshot == null && a.Error == null)),
            FontSize = 15, Foreground = Ui.Text, TextWrapping = TextWrapping.Wrap
        }, "Stats.Summary");
        page.Children.Add(Surface(summary));
        var filtered = controller.Filter(search.Text, "All connections").ToArray();
        foreach (var api in new[] { false, true })
        {
            var group = filtered.Where(a => DockController.IsApi(a.Profile.Provider) == api).ToArray();
            if (group.Length == 0) continue;
            page.Children.Add(SectionTitle(api ? Ui.L("Koszty API") : Ui.L("Limity abonamentów")));
            foreach (var account in group) page.Children.Add(StatisticsAccount(account, api));
        }
        if (filtered.Length == 0)
            page.Children.Add(Identify(Surface(Ui.Stack(Ui.Label(Ui.L("Brak kont w tym widoku"), 18), Ui.Label(Ui.L("Dodaj połączenie lub zmień wyszukiwanie."), 14, Ui.Muted), EmptyAddButton())), "Stats.Empty"));
        return page;
    }
    private UIElement StatisticsAccount(AccountView account, bool api)
    {
        var content = new StackPanel();
        content.Children.Add(Truncated(account.Profile.Name, 17));
        var status = (FrameworkElement)StatusLine(account);
        status.Margin = new Thickness(0, 7, 0, 18);
        content.Children.Add(status);
        if (account.Snapshot is not { } snapshot)
            content.Children.Add(Ui.Label(account.Error == null ? Ui.L("Oczekujemy na pierwszy odczyt.") : Ui.L("Nie udało się odczytać konta. Sprawdź połączenie lub zaloguj się ponownie."), 14, Ui.Muted));
        else if (api)
        {
            var values = Columns(-1, -1, -1);
            Cell(values, Ui.Stack(Ui.Label(Ui.L("Koszt od dostawcy"), 13, Ui.Muted), Ui.Label(Money(snapshot.CostUsd), 20)), 0);
            Cell(values, Ui.Stack(Ui.Label(Ui.L("Lokalny budżet / miesiąc"), 13, Ui.Muted), Ui.Label(account.Profile.MonthlyBudget.HasValue ? Money(account.Profile.MonthlyBudget) : Ui.L("Nie ustawiono"), 20)), 1);
            Cell(values, Ui.Stack(Ui.Label(Ui.L("Tokeny"), 13, Ui.Muted), Ui.Label(Tokens(snapshot.Tokens), 20)), 2);
            content.Children.Add(values);
            if (snapshot.CostUsd is { } cost && account.Profile.MonthlyBudget is > 0)
            {
                var percent = (double)(cost / account.Profile.MonthlyBudget.Value * 100);
                content.Children.Add(LimitRow(Ui.L("Wykorzystanie budżetu"), percent, Ui.L("Budżet lokalny")));
            }
        }
        else
        {
            foreach (var window in snapshot.Windows)
                content.Children.Add(LimitRow(WindowLabel(window.Name), window.UsedPercent, "", window.ResetsAt, true));
            if (snapshot.Windows.Count == 0) content.Children.Add(Ui.Label(Ui.L("Dostawca nie udostępnił limitów."), 14, Ui.Muted));
        }
        if (account.Profile.Provider == ProviderKind.Codex)
        {
            var reset = (FrameworkElement)ResetSummary(account); reset.Margin = new Thickness(0,16,0,8);
            content.Children.Add(reset);
            var manage = Ui.Button(Ui.L("Zarządzaj resetami"), () => ShowResetCredits(account.Profile.Id));
            manage.HorizontalAlignment = HorizontalAlignment.Left; manage.MinWidth = 180;
            content.Children.Add(manage);
        }
        if (account.Error != null && account.Snapshot != null)
            content.Children.Add(Ui.Label(Ui.L("Ostatnia próba nie powiodła się. Powyżej ostatni dostępny odczyt."), 13, Warning));
        return Identify(Surface(content), "Stats.Account." + account.Profile.Id);
    }
    private UIElement LimitRow(string name, double? percent, string reset, DateTimeOffset? at = null, bool showDate = false)
    {
        var row = Columns(-1, 180, 84, 160);
        row.Margin = new Thickness(0, 8, 0, 8);
        Cell(row, Truncated(name), 0);
        if (percent.HasValue) Cell(row, Design.Bar(percent.Value, 160, 8), 1);
        var number = Truncated(percent.HasValue ? Localization.Percent(percent) : Ui.L("Niedostępne"), 13, percent.HasValue ? Ui.Text : Ui.Muted);
        Cell(row, number, 2);
        if (showDate) Cell(row, ResetDisplay(at,160), 3);
        else { var timing = Truncated(reset,13,Ui.Muted); timing.TextAlignment=TextAlignment.Right; Cell(row,timing,3); }
        return row;
    }
}
