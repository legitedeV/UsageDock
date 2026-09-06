using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using UsageDock.Core;
namespace UsageDock.App;
public sealed partial class Dashboard
{
    private UIElement LanguageSetting()
    {
        var choices = new[] { new LanguageOption("auto", Ui.L("settings.auto")) }.Concat(Localization.Languages).ToArray();
        var language = Identify(new ComboBox { ItemsSource=choices,DisplayMemberPath="NativeName",SelectedValuePath="Code",SelectedValue=controller.Settings.Language,Width=240,MinHeight=38 },"Settings.Language");
        AutomationProperties.SetName(language,Ui.L("settings.language"));
        language.SelectionChanged+=(_,_)=>{if(language.SelectedValue is string code)TrySetLanguage(code);};
        return Surface(SettingRow(Ui.L("settings.language"),Ui.L("settings.languageDescription"),language));
    }
    internal bool TrySetLanguage(string code)
    {
        try
        {
            controller.SetSettings(controller.Settings with {Language=Localization.NormalizePreference(code)});
            savedMessage="";
            Localization.SetLanguage(code);
            RestoreFocus("Settings.Language");
            return true;
        }
        catch
        {
            Build();
            if(settingsStatus!=null){settingsStatus.Text=Ui.L("settings.languageFailed");settingsStatus.Foreground=Warning;}
            return false;
        }
    }
    private void LanguageChanged()
    {
        if(!Dispatcher.CheckAccess()){Dispatcher.Invoke(LanguageChanged);return;}
        var focus=Keyboard.FocusedElement as DependencyObject;
        var id=focus==null?null:AutomationProperties.GetAutomationId(focus);
        Build();
        if(!string.IsNullOrEmpty(id))RestoreFocus(id);
    }
}
