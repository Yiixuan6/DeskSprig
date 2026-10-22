using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopWidgets;
class TrayIconProbe
{
    [STAThread] static int Main(string[] args)
    {
        IntPtr icon=IntPtr.Zero;
        try {
            Directory.CreateDirectory(args[1]); icon=Icons.TrayIcon();
            if(icon==IntPtr.Zero) throw new Exception("Native icon extraction failed");
            var native=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); native.Freeze();
            BitmapSource expected=null;
            using(var stream=File.OpenRead(args[0])) {
                var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                foreach(var frame in decoder.Frames) if(frame.PixelWidth==native.PixelWidth && frame.PixelHeight==native.PixelHeight) { expected=frame; break; }
            }
            if(expected==null) throw new Exception("Executable icon size has no matching source ICO frame");
            var a=new FormatConvertedBitmap(native,PixelFormats.Pbgra32,null,0); var b=new FormatConvertedBitmap(expected,PixelFormats.Pbgra32,null,0);
            int stride=a.PixelWidth*4; byte[] ap=new byte[stride*a.PixelHeight],bp=new byte[ap.Length]; a.CopyPixels(ap,stride,0); b.CopyPixels(bp,stride,0);
            int max=0; for(int i=0;i<ap.Length;i++) max=Math.Max(max,Math.Abs(ap[i]-bp[i]));
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(native)); using(var file=File.Create(Path.Combine(args[1],"tray-native.png"))) encoder.Save(file);
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,192,96)); dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32,32,32)),null,new Rect(0,96,192,96));
                dc.DrawImage(expected,new Rect(16,16,64,64)); dc.DrawImage(native,new Rect(112,16,64,64)); dc.DrawImage(expected,new Rect(16,112,64,64)); dc.DrawImage(native,new Rect(112,112,64,64));
            }
            var bitmap=new RenderTargetBitmap(192,192,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var file=File.Create(Path.Combine(args[1],"brand-vs-tray-light-dark.png"))) encoder.Save(file);
            string result="Native size: "+native.PixelWidth+"x"+native.PixelHeight+Environment.NewLine+"Max premultiplied channel difference vs source ICO: "+max+Environment.NewLine+(max<=2?"PASS native tray matches application ICO":"FAIL icon mismatch");
            File.WriteAllText(Path.Combine(args[1],"icon-check.txt"),result); Console.WriteLine(result); return max<=2?0:1;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if(icon!=IntPtr.Zero) Icons.DestroyIcon(icon); }
    }
}
