using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;

namespace UsageDock.App;
internal static partial class Ui
{
    public static bool Light;
    public static event Action? PaletteChanged;
    public static Brush Background=>Hex(Light?"#F2F4F6":"#111B21");
    public static Brush Surface=>Hex(Light?"#FFFFFF":"#1C272F");
    public static Brush Text=>Hex(Light?"#18232D":"#E3E8EB");
    public static Brush Muted=>Hex(Light?"#526170":"#A2ADB8");
    public static Brush Accent=>Hex(Light?"#176C51":"#3CD3AD");
    public static Brush Hex(string value)=>(Brush)new BrushConverter().ConvertFromString(value)!;
    public static void Theme(bool light)
    {
        Light=light;
        var resources=Application.Current.Resources;
        resources[SystemColors.WindowBrushKey]=Surface;
        resources[SystemColors.WindowTextBrushKey]=Text;
        resources[SystemColors.ControlBrushKey]=Surface;
        resources[SystemColors.ControlTextBrushKey]=Text;
        resources[SystemColors.HighlightBrushKey]=Accent;
        resources[SystemColors.HighlightTextBrushKey]=Background;
        foreach(var type in new[]{typeof(Button),typeof(TextBox),typeof(PasswordBox),typeof(ComboBox),typeof(ComboBoxItem),typeof(CheckBox)})
        {
            var style=new Style(type);
            style.Setters.Add(new Setter(Control.ForegroundProperty,Text));
            style.Setters.Add(new Setter(Control.BackgroundProperty,Surface));
            style.Setters.Add(new Setter(Control.FontSizeProperty,14.0));
            style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty,Hex(light?"#CBD4DC":"#3B4755")));
            resources[type]=style;
        }
        // Full control template keeps dark mode independent of Windows' light chrome.
        var button=new Style(typeof(Button),(Style)resources[typeof(Button)]);
        button.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='frame' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#54A48D'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='" + (light ? "#176C51" : "#3CD3AD") + "'/><Setter TargetName='frame' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
        resources[typeof(Button)]=button;
        var combo=new Style(typeof(ComboBox),(Style)resources[typeof(ComboBox)]);
        combo.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' Background='{TemplateBinding Background}' Foreground='{TemplateBinding Foreground}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='{TemplateBinding Background}' BorderBrush='#526170' BorderThickness='1' Padding='10,7'><DockPanel><TextBlock Text='⌄' DockPanel.Dock='Right' Margin='12,0,0,0'/><ContentPresenter/></DockPanel></Border></ControlTemplate></ToggleButton.Template><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' HorizontalAlignment='Left'/></ToggleButton><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='{TemplateBinding Background}' BorderBrush='#526170' BorderThickness='1' MinWidth='{TemplateBinding ActualWidth}'><ScrollViewer MaxHeight='300'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid></ControlTemplate>")));
        resources[typeof(ComboBox)]=combo;
        ApplyInputTemplates(resources);
        PaletteChanged?.Invoke();
    }
    public static TextBlock Label(string text,double size=14,Brush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)};
    public static Button Button(string text,Action action,bool primary=false)
    {
        var b=new Button{Content=text,Margin=new Thickness(0,0,8,8),Cursor=System.Windows.Input.Cursors.Hand};
        if(primary){b.Background=Accent;b.Foreground=Background;}
        b.Click+=(_,_)=>action();return b;
    }
    public static StackPanel Stack(params UIElement[] children) { var p=new StackPanel(); foreach(var child in children)p.Children.Add(child);return p; }
    public static Border Card(UIElement child)=>new(){Background=Surface,CornerRadius=new CornerRadius(10),Padding=new Thickness(20),Margin=new Thickness(0,0,0,12),Child=child};
    public static void Window(Window window,string title,double width,double height)
    { window.Title=title;window.Width=width;window.Height=height;window.Background=Background;window.Foreground=Text;window.FontFamily=new FontFamily("Segoe UI");window.WindowStartupLocation=WindowStartupLocation.CenterScreen; }
}
