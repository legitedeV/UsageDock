using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace UsageDock.App;
internal static partial class Ui
{
    private static void ApplyInputTemplates(ResourceDictionary resources)
    {
        var accent = Light ? "#176C51" : "#3CD3AD";
        const string ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
        var input = new Style(typeof(TextBox), (Style)resources[typeof(TextBox)]);
        input.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
            "<ControlTemplate " + ns + " TargetType='TextBox'><Border x:Name='frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='5' Padding='0'><ScrollViewer x:Name='PART_ContentHost'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='" + accent + "'/><Setter TargetName='frame' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#54A48D'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
        resources[typeof(TextBox)] = input;
        var scroll = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
        scroll.Setters.Add(new Setter(Control.BackgroundProperty, Background));
        scroll.Setters.Add(new Setter(FrameworkElement.WidthProperty, 12d));
        scroll.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
            "<ControlTemplate " + ns + " TargetType='ScrollBar'><Grid Background='{TemplateBinding Background}'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'><Track.DecreaseRepeatButton><RepeatButton x:Name='decrease' Command='ScrollBar.PageUpCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='" + (Light ? "#98A8B1" : "#52636E") + "' Margin='3' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton x:Name='increase' Command='ScrollBar.PageDownCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/><Setter TargetName='decrease' Property='Command' Value='ScrollBar.PageLeftCommand'/><Setter TargetName='increase' Property='Command' Value='ScrollBar.PageRightCommand'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
        var horizontal = new Trigger { Property = System.Windows.Controls.Primitives.ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
        horizontal.Setters.Add(new Setter(FrameworkElement.HeightProperty, 12d));
        horizontal.Setters.Add(new Setter(FrameworkElement.WidthProperty, double.NaN));
        scroll.Triggers.Add(horizontal);
        resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scroll;        foreach (var type in new[] { typeof(CheckBox), typeof(RadioButton) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Hex(Light ? "#A4B2BC" : "#667580")));
            style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate " + ns + " TargetType='" + type.Name + "'><Border x:Name='focus' BorderBrush='Transparent' BorderThickness='1' CornerRadius='5' Padding='8,7'><DockPanel><Border x:Name='mark' Width='18' Height='18' CornerRadius='" + (type == typeof(RadioButton) ? "9" : "4") + "' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Margin='0,0,9,0'><Border x:Name='dot' Background='" + accent + "' CornerRadius='4' Margin='4' Visibility='Collapsed'/></Border><ContentPresenter VerticalAlignment='Center'/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='dot' Property='Visibility' Value='Visible'/><Setter TargetName='mark' Property='BorderBrush' Value='" + accent + "'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='#54A48D'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='" + accent + "'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='focus' Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
            resources[type] = style;
        }
    }
}
