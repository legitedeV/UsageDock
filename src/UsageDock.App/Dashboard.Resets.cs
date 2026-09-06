using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsageDock.Core;

namespace UsageDock.App;

public sealed partial class Dashboard
{
    private UIElement ResetSummary(AccountView account)
    {
        var inventory = account.Snapshot?.ResetCredits;
        var panel = new StackPanel();
        var count = inventory == null ? Ui.L("Zapasowe resety: brak danych") : Ui.L("resets.count",inventory.AvailableCount);
        panel.Children.Add(Identify(Truncated(count, 13, inventory == null ? Ui.Muted : Ui.Accent), "Resets.Count." + account.Profile.Id));
        var next = inventory?.Credits?.Where(c => c.CanRedeem(DateTimeOffset.UtcNow) && c.ExpiresAt.HasValue)
            .OrderBy(c => c.ExpiresAt).FirstOrDefault();
        if (next != null)
        {
            var expiry = clock.Label(next.ExpiresAt, true, 11);
            expiry.TextTrimming = TextTrimming.CharacterEllipsis;
            var expiryLine = new StackPanel { Orientation = Orientation.Horizontal };
            expiryLine.Children.Add(Truncated(Ui.L("Najbliższy wygasa: "),11,Ui.Muted));
            expiryLine.Children.Add(expiry);
            panel.Children.Add(expiryLine);
        }
        else panel.Children.Add(Truncated(inventory?.Credits == null ? Ui.L("Terminy wygaśnięcia niedostępne") : Ui.L("Szczegóły ważności w zarządzaniu resetami"), 11, Ui.Muted));
        return panel;
    }

    internal void ShowResetCredits(Guid accountId) => CreateResetCreditsWindow(accountId)?.ShowDialog();

    internal Window? CreateResetCreditsWindow(Guid accountId)
    {
        var account = controller.Accounts.FirstOrDefault(a => a.Profile.Id == accountId);
        if (account == null) return null;
        var window = new Window { Owner = this };
        Ui.Window(window, Ui.L("Zapasowe resety Codex · UsageDock"), 660, 600);
        window.Content = ResetDialogContent(account,window);
        void Relocalize(){window.Title=Ui.L("Zapasowe resety Codex · UsageDock");window.Content=ResetDialogContent(controller.Accounts.FirstOrDefault(a=>a.Profile.Id==accountId)??account,window);}
        Localization.Changed+=Relocalize;window.Closed+=(_,_)=>Localization.Changed-=Relocalize;
        return window;
    }

    private UIElement ResetDialogContent(AccountView account, Window window, string? message = null, bool problem = false)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(Ui.Label(account.Profile.Name, 24));
        panel.Children.Add(Ui.Label(Ui.L("Jeden zapasowy reset może odnowić wykorzystane okna Codex 5 h i tygodniowe. Zostanie zużyty dopiero po Twoim potwierdzeniu."), 14, Ui.Muted));
        if (message != null) panel.Children.Add(Ui.Label(message,14,problem?Warning:Ui.Accent));
        panel.Children.Add(ResetInventoryPanel(account, window));
        var close = Ui.Button(Ui.L("Zamknij"), window.Close);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.MinWidth = 100;
        panel.Children.Add(close);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    internal UIElement ResetInventoryPanel(AccountView account, Window? owner = null)
    {
        var panel = Identify(new StackPanel { Margin = new Thickness(0, 16, 0, 16) }, "Resets.Inventory");
        var inventory = account.Snapshot?.ResetCredits;
        panel.Children.Add(ResetSummary(account));
        CodexResetAttempt? pending;
        try { pending = controller.GetPendingReset(account.Profile.Id); }
        catch { panel.Children.Add(Ui.Label(Ui.L("Nie można odczytać poprzedniej próby. Użycie resetów zablokowane, aby uniknąć podwójnego zużycia."),14,Warning)); return panel; }
        if (pending != null)
        {
            var pendingStatus = Ui.Label(Ui.L("Poprzednia próba wymaga sprawdzenia. Ponowienie zachowa ten sam identyfikator operacji."),13,Warning);
            var retry = Identify(Ui.Button(Ui.L("Sprawdź poprzednią próbę"), () => { }), "Resets.Retry");
            retry.HorizontalAlignment = HorizontalAlignment.Left;
            retry.IsEnabled = !controller.IsResetBusy(account.Profile.Id);
            retry.Click += async (_, _) => await UseReset(account.Profile.Id,pending.CreditId,retry,pendingStatus,owner);
            panel.Children.Add(pendingStatus); panel.Children.Add(retry);
        }
        if (inventory == null)
        {
            panel.Children.Add(Ui.Label(Ui.L("Nie udało się pobrać zapasowych resetów. Odśwież konto i spróbuj ponownie."), 14, Ui.Muted));
            return panel;
        }
        if (account.Error != null || account.Snapshot?.ResetCreditsError != null)
            panel.Children.Add(Ui.Label(Ui.L("Dane są nieaktualne. Przed użyciem odśwież konto."), 14, Warning));
        if (inventory.Credits == null)
        {
            panel.Children.Add(Ui.Label(Ui.L("Dostawca podał liczbę resetów bez ich szczegółów. Nie można bezpiecznie ustalić ważności ani wybrać resetu."), 14, Ui.Muted));
            return panel;
        }
        if (inventory.Credits.Count == 0)
            panel.Children.Add(Ui.Label(inventory.AvailableCount == 0 ? Ui.L("Nie ma dostępnych zapasowych resetów.") : Ui.L("Lista szczegółów jest pusta. Dostawca nie udostępnił danych poszczególnych resetów."), 14, Ui.Muted));
        foreach (var credit in inventory.Credits)
        {
            var details = new StackPanel();
            details.Children.Add(Ui.Label(credit.ResetType == "codex_rate_limits" ? Ui.L("Reset limitów Codex") : Ui.L("Inny rodzaj resetu"), 16));
            details.Children.Add(Ui.Label(CreditState(credit), 13, Ui.Muted));
            if (credit.GrantedAt.HasValue) details.Children.Add(Ui.Label(Ui.L("Przyznano: ") + UsageTime.Absolute(credit.GrantedAt,TimeZoneInfo.Local,Localization.CurrentLanguage),12,Ui.Muted));
            if (!credit.ExpiryKnown) details.Children.Add(Ui.Label(Ui.L("Termin ważności nieznany — użycie niedostępne"), 13, Warning));
            else if (credit.ExpiresAt == null) details.Children.Add(Ui.Label(Ui.L("Bez terminu wygaśnięcia"), 13, Ui.Muted));
            else
            {
                details.Children.Add(Ui.Label(Ui.L("Wygasa"), 12, Ui.Muted));
                var expiry = clock.Label(credit.ExpiresAt, true);
                expiry.TextWrapping = TextWrapping.Wrap;
                details.Children.Add(expiry);
            }
            var status = Identify(new TextBlock { Foreground = Ui.Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8) }, "Resets.Status." + credit.Id);
            var use = Identify(Ui.Button(Ui.L("Użyj resetu"), () => { }, true), "Resets.Use." + credit.Id);
            use.HorizontalAlignment = HorizontalAlignment.Left;
            use.MinWidth = 140;
            use.IsEnabled = pending == null && !controller.IsResetBusy(account.Profile.Id) && inventory.AvailableCount > 0 && credit.CanRedeem(DateTimeOffset.UtcNow) && account.Error == null && account.Snapshot?.ResetCreditsError == null;
            use.Click += async (_, _) => await UseReset(account.Profile.Id, credit.Id, use, status, owner);
            details.Children.Add(status);
            details.Children.Add(use);
            panel.Children.Add(Surface(details));
        }
        if (inventory.AvailableCount > inventory.Credits.Count)
            panel.Children.Add(Ui.Label(Ui.L("Dostawca zwrócił tylko część szczegółów. Liczba dostępnych resetów może być większa od tej listy."), 13, Ui.Muted));
        return panel;
    }

    private static string CreditState(CodexResetCredit credit) => credit.ExpiryKnown && credit.ExpiresAt <= DateTimeOffset.UtcNow && credit.Status == "available" ? Ui.L("Wygasły") : credit.Status switch
    {
        "available" => Ui.L("Dostępny"), "redeeming" => Ui.L("W trakcie realizacji"), "redeemed" => Ui.L("Wykorzystany"), "expired" => Ui.L("Wygasły"), _ => Ui.L("Status nieznany")
    };
}
