using System;
using System.Windows;
using System.Windows.Controls;

namespace UsageDock.App;
public sealed partial class Dashboard
{
    public bool TrySetTheme(bool light)
    {
        var theme = light ? "Light" : "Dark";
        if (controller.Settings.Theme == theme && Ui.Light == light) return true;
        try
        {
            controller.SetSettings(controller.Settings with { Theme = theme });
        }
        catch
        {
            draftTheme = controller.Settings.Theme;
            Build();
            var message = Ui.L("Nie udało się zapisać motywu. Spróbuj ponownie.");
            if (tab == "Ustawienia" && settingsStatus != null)
            {
                settingsStatus.Text = message;
                settingsStatus.Foreground = Warning;
            }
            else if (updateStamp != null) updateStamp.Text = message;
            return false;
        }
        draftTheme = theme;
        savedMessage = Ui.L("Motyw zapisany");
        Ui.Theme(light);
        Build();
        return true;
    }

    internal Button ThemeButton(string automationId, bool compact)
    {
        var button = Design.Action(Ui.Light ? "moon" : "sun",
            Ui.Light ? Ui.L("Włącz ciemny motyw") : Ui.L("Włącz jasny motyw"),
            () => { },
            compact ? 28 : 108, compact ? 30 : 40, false, compact);
        if (!compact)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(Design.Icon(Ui.Light ? "moon" : "sun", 18, Ui.Muted));
            content.Children.Add(new TextBlock { Text = Ui.Light ? Ui.L("Ciemny") : Ui.L("Jasny"), FontSize = 14,
                Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            button.Content = content;
        }
        button.Click += (_, _) =>
        {
            var owner = Window.GetWindow(button);
            var saved = TrySetTheme(!Ui.Light);
            if (owner is Widget widget) widget.SetThemeError(saved ? null : "Nie zapisano motywu. Spróbuj ponownie.");
            if (owner != null) Dispatcher.BeginInvoke(new Action(() => FindFocus(owner, automationId)?.Focus()));
        };
        button.Padding = new Thickness(compact ? 0 : 6);
        return Identify(button, automationId);
    }
}
