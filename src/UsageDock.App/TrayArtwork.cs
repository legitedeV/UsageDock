using System;
using System.Drawing;
using System.Runtime.InteropServices;
namespace UsageDock.App;
internal static class TrayArtwork
{
    public static Icon Create()
    {
        using var bitmap=new Bitmap(32,32);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.FromArgb(21,25,31));
        using var pen=new Pen(Color.FromArgb(129,212,178),4);graphics.DrawLines(pen,new[]{new Point(7,7),new Point(7,21),new Point(12,25),new Point(17,21),new Point(17,7)});
        using var brush=new SolidBrush(Color.FromArgb(219,156,131));graphics.FillRectangle(brush,23,8,4,17);
        var handle=bitmap.GetHicon();try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
