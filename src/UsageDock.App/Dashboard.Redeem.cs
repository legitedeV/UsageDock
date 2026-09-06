using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UsageDock.Core;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    internal static MessageBoxResult ResetConfirmationDefault => MessageBoxResult.No;

    internal static string BuildResetConfirmation(AccountView account, CodexResetCredit? credit, bool retry)
    {
        var expiry = credit == null || !credit.ExpiryKnown ? Ui.L("termin ważności nieznany")
            : credit.ExpiresAt == null ? Ui.L("bez terminu wygaśnięcia")
            : Ui.L("wygasa ") + UsageTime.Absolute(credit.ExpiresAt,TimeZoneInfo.Local,Localization.CurrentLanguage);
        var identity = Ui.L("resets.identity",account.Profile.Name,expiry);
        return identity + (retry
            ? Ui.L("Sprawdzić lub ponowić poprzednią próbę użycia resetu? Zachowamy ten sam identyfikator operacji, aby nie zużyć kolejnego resetu.")
            : Ui.L("Użyć jednego zapasowego resetu Codex? Może to odnowić wykorzystane limity 5 h i tygodniowy. Potwierdzonego zużycia nie można cofnąć."));
    }

    private async Task UseReset(Guid accountId, string? creditId, Button button, TextBlock status, Window? owner)
    {
        CodexResetAttempt? pending;
        try { pending = controller.GetPendingReset(accountId); }
        catch { status.Text = Ui.L("Nie można odczytać poprzedniej próby. Operacja zablokowana."); return; }
        var account = controller.Accounts.FirstOrDefault(a => a.Profile.Id == accountId);
        if (account == null || account.Profile.Provider != ProviderKind.Codex)
        {
            status.Text = Ui.L("To konto nie jest już dostępne jako połączenie Codex.");
            return;
        }
        var selectedId = pending != null ? pending.CreditId : creditId;
        var selected = account.Snapshot?.ResetCredits?.Credits?.FirstOrDefault(c => c.Id == selectedId);
        var text = BuildResetConfirmation(account,selected,pending != null);
        if (MessageBox.Show(owner ?? this, text, Ui.L("Potwierdź użycie resetu"), MessageBoxButton.YesNo, MessageBoxImage.Question, ResetConfirmationDefault) != MessageBoxResult.Yes) return;
        button.IsEnabled = false;
        status.Text = Ui.L("Sprawdzanie dostępności i realizacja…");
        CodexResetResult result;
        try { result = await controller.ConsumeResetAsync(accountId, pending != null ? pending.CreditId : creditId); }
        catch { status.Text = Ui.L("Nie udało się zakończyć próby. Otwórz zarządzanie resetami, aby bezpiecznie sprawdzić jej wynik."); return; }
        var message = result.Outcome switch
        {
            CodexResetOutcome.Reset => Ui.L("Reset został użyty. Ponowny odczyt może wymagać chwili."),
            CodexResetOutcome.AlreadyRedeemed => Ui.L("Ta próba została już zrealizowana. Nie zużyto kolejnego resetu."),
            CodexResetOutcome.NothingToReset => Ui.L("Nie ma wykorzystanych okien wymagających resetu."),
            CodexResetOutcome.NoCredit => Ui.L("Wybrany reset nie jest już dostępny. Odśwież dane konta."),
            CodexResetOutcome.Uncertain => Ui.L("Wynik jest niepewny. Sprawdź poprzednią próbę; jej identyfikator został zachowany."),
            CodexResetOutcome.RateLimited => Ui.L("Dostawca prosi o odczekanie. ") + UsageTime.Full(result.RetryAfter,DateTimeOffset.UtcNow,TimeZoneInfo.Local,Localization.CurrentLanguage),
            CodexResetOutcome.Denied => Ui.L("Dostawca nie zezwolił na użycie resetu. Sprawdź połączenie."),
            _ => Ui.L("Nie można wykonać tej operacji. Odśwież konto i sprawdź dostępność resetu.")
        };
        var current = controller.Accounts.FirstOrDefault(a => a.Profile.Id == accountId);
        if (owner != null && current != null) owner.Content = ResetDialogContent(current,owner,message,result.Outcome is not (CodexResetOutcome.Reset or CodexResetOutcome.AlreadyRedeemed));
        else status.Text = message;
    }
}
