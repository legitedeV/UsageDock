using System;
using System.Windows;
using System.Windows.Controls;
using UsageDock.Core;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    public UIElement AccountCard(AccountView account, bool compact)
    {
        if (compact) return MiniCard(account);
        var profile = account.Profile;
        var snapshot = account.Snapshot;
        var hasResets = profile.Provider == ProviderKind.Codex;
        var row = Design.Canvas(1104, hasResets ? 144 : 90);
        Design.At(row, new Border { Width = 1068, Height = 1, Background = Design.Line }, 32, hasResets ? 143 : 89);
        Design.At(row, Design.Provider(profile.Provider, 32), 34, 29);
        var name = Truncated(profile.Name, 16); name.Width = 208;
        name.FontWeight = FontWeights.SemiBold;
        Design.At(row, name, 82, 22);
        var provider = Truncated(ProviderLabel(profile.Provider), 13, Ui.Muted);
        provider.Width = 208; Design.At(row, provider, 82, 51);
        Design.Txt(row, DockController.IsApi(profile.Provider) ? "API" : "Abonament", 306, 30, 14, Ui.Muted);
        if (snapshot == null)
        {
            Design.Txt(row, account.Error == null ? "Oczekiwanie na odczyt" : "Odczyt niedostępny", 432, 22, 14, account.Error == null ? Ui.Muted : Warning);
            if (account.Error != null)
            {
                var reconnect = Ui.Button("Sprawdź połączenie", () => Edit(profile));
                reconnect.Height = 32; reconnect.Margin = new Thickness(0);
                Design.At(row, reconnect, 432, 48);
            }
        }
        else if (DockController.IsApi(profile.Provider))
        {
            Design.Txt(row, Money(snapshot.CostUsd), 432, 21, 16, null, true);
            if (profile.MonthlyBudget is > 0 && snapshot.CostUsd.HasValue)
            {
                var percent = (double)(snapshot.CostUsd.Value / profile.MonthlyBudget.Value * 100);
                Design.At(row, Design.Bar(percent, 226, 9), 432, 55);
                Design.Txt(row, "z " + Money(profile.MonthlyBudget), 714, 21, 14, Ui.Muted);
                Design.Txt(row, $"{percent:0.#}% budżetu", 714, 49, 13, Ui.Muted);
            }
            else Design.Txt(row, "Brak budżetu", 714, 30, 14, Ui.Muted);
        }
        else
        {
            for (var i = 0; i < Math.Min(snapshot.Windows.Count, 2); i++)
            {
                var window = snapshot.Windows[i];
                var y = 11 + i * 34;
                var label = Truncated(WindowLabel(window.Name), 13, Ui.Muted); label.Width = 94;
                Design.At(row, label, 432, y);
                if (window.UsedPercent is { } value)
                {
                    Design.At(row, Design.Bar(value, 100, 9), 534, y + 5);
                    Design.Txt(row, $"{value:0.#}%", 646, y, 13);
                }
                else Design.Txt(row, "—", 646, y, 13, Ui.Muted);
                var reset = ResetDisplay(window.ResetsAt,162);
                Design.At(row, reset, 714, y);
            }
            if (snapshot.Windows.Count == 0) Design.Txt(row, "Brak udostępnionych limitów", 432, 27, 14, Ui.Muted);
        }
        var tokens = Truncated(Tokens(snapshot?.Tokens), 13, Ui.Muted); tokens.Width = 92;
        Design.At(row, tokens, 890, 31);
        Design.At(row, Design.Action("edit", "Edytuj konto", () => Edit(profile), 34, 38, false, true), 988, 22);
        Design.At(row, Design.Action("star", profile.IsFavorite ? "Usuń z widgetu" : "Przypnij do widgetu", () => Safe(() => controller.Favorite(profile.Id)), 34, 38, false, true), 1028, 22);
        var more = Design.Action("more", "Więcej akcji", () => { }, 28, 38, false, true);
        var menu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[] {
            ("Szczegóły odczytu", () => ShowDetails(account)), ("Odśwież wszystko", Refresh),
            ("Edytuj połączenie", () => Edit(profile)), ("Otwórz widget", () => WidgetRequested?.Invoke()),
            ("Usuń konto", () => { if (MessageBox.Show("Usunąć konto „" + profile.Name + "” i zapisane poświadczenie?", "UsageDock", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Safe(() => controller.Remove(profile.Id)); }) })
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        more.ContextMenu = menu; more.Click += (_, _) => menu.IsOpen = true;
        Design.At(row, more, 1070, 22);
        if (account.Error != null && snapshot != null)
            Design.Txt(row, "Nieaktualne · ponów odczyt lub sprawdź połączenie", 432, 78, 12, Warning);
        if (hasResets)
        {
            var summary = (FrameworkElement)ResetSummary(account); summary.Width = 720;
            Design.At(row, summary, 82, 96);
            var manage = Identify(Ui.Button("Zarządzaj resetami", () => ShowResetCredits(profile.Id)), "Resets.Manage." + profile.Id);
            manage.Width = 192; manage.Height = 36; manage.Margin = new Thickness(0);
            Design.At(row, manage, 890, 96);
        }
        row.ToolTip = account.Error == null ? snapshot?.Note : "Odczyt nie powiódł się. Sprawdź połączenie.";
        return row;
    }
}
