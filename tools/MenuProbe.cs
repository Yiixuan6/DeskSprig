using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopWidgets;
public static class MenuProbe
{
    [STAThread] public static void Main(string[] args)
    {
        var app=new WidgetApp { Verifying=true,ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"menu-probe-data")); app.HideAll(); app.Settings.Theme="dark"; app.Theme.Refresh();
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async delegate {
            try { var menu=app.CreateTrayMenu(); menu.IsOpen=true; await Task.Delay(200); menu.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(menu); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(args[0])) png.Save(stream); menu.IsOpen=false; }
            finally { app.Quit(); }
        })); app.Run();
    }
}
