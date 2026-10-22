using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace DesktopWidgets
{
    public static class Icons
    {
        static readonly string[] placementPaths={"M8,1 L6,3 7.3,3 7.3,7.3 3,7.3 3,6 1,8 3,10 3,8.7 7.3,8.7 7.3,13 6,13 8,15 10,13 8.7,13 8.7,8.7 13,8.7 13,10 15,8 13,6 13,7.3 8.7,7.3 8.7,3 10,3 Z", "M5,7 L5,4 A3,3 0 0 1 11,4 L11,7 M3.5,7.5 L12.5,7.5 12.5,14 3.5,14 Z M8,10 L8,12", "M1.5,2.5 L14.5,2.5 14.5,11.5 1.5,11.5 Z M8,11.5 L8,14 M5,14 L11,14"};
        static readonly Geometry[] placementGeometry=new Geometry[3];
        public static FrameworkElement Placement(int mode,double size)
        {
            int i=Math.Max(0,Math.Min(2,mode-1)); if(placementGeometry[i]==null) { placementGeometry[i]=Geometry.Parse(placementPaths[i]); placementGeometry[i].Freeze(); }
            var shape=new System.Windows.Shapes.Path { Data=placementGeometry[i],Width=size,Height=size,Stretch=Stretch.Uniform,StrokeThickness=i==0?0:1.3,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round };
            shape.SetResourceReference(i==0?System.Windows.Shapes.Shape.FillProperty:System.Windows.Shapes.Shape.StrokeProperty,i==0?"Muted":"Accent"); return shape;
        }
        static readonly Dictionary<string, Geometry> geometry = new Dictionary<string, Geometry>();
        static readonly Dictionary<string, ImageSource> targets = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        public static FrameworkElement Glyph(string name, double size, string brush)
        {
            Geometry cached;
            if (!geometry.TryGetValue(name, out cached))
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Icons." + name + ".svg"))
                {
                    if (stream == null) throw new InvalidOperationException("缺少图标资源：" + name);
                    var doc = XDocument.Load(stream); var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                    foreach (var path in doc.Descendants()) if (path.Name.LocalName == "path") group.Children.Add(Geometry.Parse((string)path.Attribute("d")));
                    group.Freeze(); cached = group; geometry.Add(name, cached);
                }
            }
            var shape = new System.Windows.Shapes.Path { Data = cached, Stretch = Stretch.Uniform, Width = size, Height = size };
            shape.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush); return shape;
        }
        public static ImageSource Brand
        {
            get
            {
                if (brand == null) { using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.png")) { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); brand = bitmap; } }
                return brand;
            }
        }
        static ImageSource brand;
        static ImageSource folder;
        public static ImageSource Folder
        {
            get
            {
                if(folder==null) { var group=new DrawingGroup(); group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(231,161,36)),null,Geometry.Parse("M2,7 Q2,4 5,4 L13,4 17,8 28,8 Q30,8 30,11 L30,26 Q30,28 28,28 L4,28 Q2,28 2,26 Z"))); group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(255,207,79)),null,Geometry.Parse("M2,13 Q2,11 4,11 L28,11 Q30,11 30,13 L30,26 Q30,28 28,28 L4,28 Q2,28 2,26 Z"))); group.Freeze(); folder=new DrawingImage(group); folder.Freeze(); } return folder;
            }
        }
        public static void ClearTargets() { targets.Clear(); }
        public static BitmapSource Decode(string file)
        {
            using(var stream=File.OpenRead(file)) { var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth=64; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
        }
        public static string Import(string file,string directory)
        {
            string ext=Path.GetExtension(file).ToLowerInvariant(); if(ext!=".png" && ext!=".jpg" && ext!=".jpeg" && ext!=".ico") throw new InvalidDataException("请选择 ICO、PNG 或 JPG。"); Decode(file);
            string relative="icons/"+Guid.NewGuid().ToString("N")+ext; Directory.CreateDirectory(Path.Combine(directory,"icons")); File.Copy(file,Path.Combine(directory,relative)); return relative;
        }
        public static ImageSource Shortcut(ShortcutItem item,string directory)
        {
            if(!Directory.Exists(item.Path)) return Target(item.Path);
            if(!String.IsNullOrEmpty(item.CustomIcon)) try
            {
                string iconDir=Path.GetFullPath(Path.Combine(directory,"icons"))+Path.DirectorySeparatorChar;
                string file=Path.GetFullPath(Path.Combine(directory,item.CustomIcon)); if(!file.StartsWith(iconDir,StringComparison.OrdinalIgnoreCase)) return Folder;
                ImageSource cached; if(targets.TryGetValue(file,out cached)) return cached; cached=Decode(file); if(targets.Count>=96) targets.Clear(); targets[file]=cached; return cached;
            }
            catch { }
            return Folder;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileInfo
        { public IntPtr Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Display; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attributes, ref FileInfo info, uint size, uint flags);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string path, int index, out IntPtr large, out IntPtr small, uint count);
        public static IntPtr TrayIcon()
        {
            // Use the executable's own multi-size icon instead of a separate tray glyph.
            IntPtr large, small;
            ExtractIconEx(Assembly.GetExecutingAssembly().Location, 0, out large, out small, 1);
            if (small != IntPtr.Zero) { if (large != IntPtr.Zero) DestroyIcon(large); return small; }
            return large;
        }
        public static ImageSource Target(string path)
        {
            ImageSource icon; if (targets.TryGetValue(path, out icon)) return icon;
            var info = new FileInfo();
            uint flags = 0x100; // SHGFI_ICON, large icon; target .lnk is resolved by the shell.
            if (!File.Exists(path) && !Directory.Exists(path)) flags |= 0x10;
            if (SHGetFileInfo(path, 0x80, ref info, (uint)Marshal.SizeOf(typeof(FileInfo)), flags) == IntPtr.Zero || info.Icon == IntPtr.Zero) return Brand;
            try { var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32)); bitmap.Freeze(); icon = bitmap; }
            finally { DestroyIcon(info.Icon); }
            if (targets.Count >= 96) targets.Clear(); targets[path] = icon; return icon;
        }
    }
}
