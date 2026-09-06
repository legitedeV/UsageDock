using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using UsageDock.Core;
namespace UsageDock.App;
internal static class Design
{
 public static Brush Line=>Ui.Hex(Ui.Light?"#DAE2E5":"#1C2931");
 public static Canvas Canvas(double width,double height)=>new(){Width=width,Height=height};
 public static void At(Canvas c,UIElement e,double x,double y){System.Windows.Controls.Canvas.SetLeft(e,x);System.Windows.Controls.Canvas.SetTop(e,y);c.Children.Add(e);}
 public static TextBlock Text(string s,double size=16,Brush? brush=null,bool bold=false)=>new(){Text=s,FontSize=size,Foreground=brush??Ui.Text,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.NoWrap};
 public static void Txt(Canvas c,string s,double x,double y,double size=16,Brush? brush=null,bool bold=false)=>At(c,Text(s,size,brush,bold),x,y);
 public static FrameworkElement Icon(string name,double size=20,Brush? brush=null)
 {
  var data=name switch{
   "logo"=>"M1,1 H8 V8 H1 Z M12,1 H19 V8 H12 Z M1,12 H8 V19 H1 Z M12,12 H19 V19 H12 Z",
   "sun"=>"M14,10 A4,4 0 1 1 6,10 A4,4 0 1 1 14,10 M10,1 V3 M10,17 V19 M1,10 H3 M17,10 H19 M3,3 L5,5 M15,15 L17,17 M3,17 L5,15 M15,5 L17,3",
   "moon"=>"M15,3 A8,8 0 1 0 17,15 A7,7 0 0 1 15,3 Z",
   "minimize"=>"M3,10 H17",
   "plus"=>"M10,3 V17 M3,10 H17", "close"=>"M5,5 L15,15 M15,5 L5,15",
   "search"=>"M14,9 A5,5 0 1 1 4,9 A5,5 0 1 1 14,9 M13,13 L18,18",
   "refresh"=>"M16,7 A7,7 0 1 0 17,13 M16,2 V7 H11",
   "pin"=>"M7,2 H14 L13,7 L16,11 H5 L8,7 Z M10,11 V19",
   "edit"=>"M4,14 L13,3 L17,7 L8,17 L3,18 Z M11,5 L15,9",
   "more"=>"M10,3 L10,5 M10,9 L10,11 M10,15 L10,17",
   "star"=>"M10,1 L13,7 L19,8 L14,12 L15,19 L10,16 L4,19 L5,12 L1,8 L7,7 Z",
   "stats"=>"M3,17 H17 M5,14 V9 M10,14 V3 M15,14 V6",
   "history"=>"M5,3 H17 V18 H5 Z M2,6 H7 M2,10 H7 M2,14 H7 M10,7 H14 M10,11 H14",
   _=>"M7,2 H13 L14,5 L17,6 L18,12 L15,14 L14,18 H7 L6,15 L3,13 L2,7 L5,5 Z M13,10 A3,3 0 1 1 7,10 A3,3 0 1 1 13,10"};
  return new Viewbox{Width=size,Height=size,Child=new Path{Data=Geometry.Parse(data),Stroke=brush??Ui.Muted,StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round,Width=20,Height=20}};
 }
 public static Button Action(string icon,string label,Action action,double width=40,double height=40,bool primary=false,bool iconOnly=false)
 {
  var panel=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var glyph=Icon(icon,18,primary?(Ui.Light?Brushes.White:Ui.Hex("#082F2E")):Ui.Muted);if(icon=="star"&&label==Ui.L("Usuń z widgetu")){var path=(Path)((Viewbox)glyph).Child;path.Fill=Ui.Hex("#F5BC35");path.Stroke=Ui.Hex("#F5BC35");}panel.Children.Add(glyph);
  if(!iconOnly)panel.Children.Add(new TextBlock{Text=label,FontSize=16,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});
  var b=Ui.Button(label,action,primary);b.Content=panel;b.Width=width;b.Height=height;b.Margin=new Thickness(0);b.Padding=new Thickness(6);b.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(b,label);if(iconOnly){b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0);b.BorderBrush=Brushes.Transparent;}return b;
 }
 public static FrameworkElement Bar(double value,double width,double height=14,Brush? color=null)
 {
  var grid=new Grid{Width=width,Height=height};grid.Children.Add(new Border{Background=Ui.Hex(Ui.Light?"#DAE3E7":"#24323D"),CornerRadius=new CornerRadius(height/2)});grid.Children.Add(new Border{Background=color??Ui.Hex(value>=80?"#F7645B":"#54D9BB"),Width=width*Math.Clamp(value,0,100)/100,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(height/2)});return grid;
 }
 public static UIElement Provider(ProviderKind provider,double size=36,bool widget=false)
 {
  var c=Canvas(48,48);
  if(provider==ProviderKind.AnthropicApi){c.Background=Ui.Hex("#EAC7A2");Txt(c,"AI",5,4,31,Ui.Hex("#121A20"),true);}
  else if(provider is ProviderKind.ClaudeOAuth or ProviderKind.ClaudeSession){c.Background=Ui.Hex("#42322B");for(var i=0;i<12;i++){var a=i*Math.PI/6;At(c,new Line{X1=24+8*Math.Cos(a),Y1=24+8*Math.Sin(a),X2=24+21*Math.Cos(a),Y2=24+21*Math.Sin(a),Stroke=Ui.Hex("#DB8458"),StrokeThickness=3.5},0,0);}}
  else{c.Background=provider==ProviderKind.OpenAiApi&&!widget?Ui.Hex("#286759"):Ui.Hex("#111719");At(c,new Path{Data=Geometry.Parse("M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z"),Fill=Ui.Hex("#E3E8EB"),Stretch=Stretch.Uniform,Width=42,Height=42},3,3);}
  return new Viewbox{Width=size,Height=size,Child=c};
 }
 public static bool CanDrag(DependencyObject? source,FrameworkElement header)
 {
  for(var node=source;node!=null;node=VisualTreeHelper.GetParent(node))
  {
   if(node is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase or ComboBox)return false;
   if(ReferenceEquals(node,header))return true;
  }
  return false;
 }
 public static void DragHeader(Window window,FrameworkElement header)
 {
  header.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==1&&CanDrag(e.OriginalSource as DependencyObject,header))window.DragMove();};
 }
 public static void Chrome(Window w){w.WindowStyle=WindowStyle.None;WindowChrome.SetWindowChrome(w,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(5),CornerRadius=new CornerRadius(10),GlassFrameThickness=new Thickness(0)});}
 public static Border Frame(UIElement child)=>new(){Background=Ui.Background,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Child=child,ClipToBounds=true};
}
