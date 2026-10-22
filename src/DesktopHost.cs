using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopWidgets
{
    // A separate child HWND is born in Explorer's DPI context. Never SetParent a
    // per-monitor WPF top-level across DPI contexts: Windows can reset the process.
    public sealed class DesktopHost : IDisposable
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr l);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref Point p);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int command);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
        [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetFocus();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder text,int count);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SetWindowText(IntPtr h,string text);
        public void RefreshName() { if(root!=null) System.Windows.Automation.AutomationProperties.SetName(root,window.Title); if(Attached) SetWindowText(Handle,window.Title); }
        public string Snapshot() { return "sourceDisposed="+(source==null?"null":source.IsDisposed.ToString())+"; child="+Handle+"; childExists="+IsWindow(Handle)+"; parent="+Parent+"; parentExists="+IsWindow(Parent)+"; actualParent="+GetParent(Handle)+"; style="+GetWindowLong(Handle,-16).ToInt64().ToString("X")+"; currentHost="+FindHost(); }
        public static string ForegroundClass() { var text=new System.Text.StringBuilder(256); GetClassName(GetForegroundWindow(),text,text.Capacity); return text.ToString(); }
        public static bool DesktopForeground() { string cls=ForegroundClass(); return cls=="Progman" || cls=="WorkerW" || cls=="Shell_TrayWnd"; }
        readonly WidgetWindow window;
        HwndSource source;
        FrameworkElement root;
        Grid surface;
        internal FrameworkElement Surface { get { return surface; } }
        public bool TransparentSurface { get { return source!=null && !source.IsDisposed && source.UsesPerPixelOpacity && source.CompositionTarget.BackgroundColor.A==0; } }
        public bool AboveIcons { get { IntPtr icons=FindWindowEx(Parent,IntPtr.Zero,"SHELLDLL_DefView",null); if(icons==IntPtr.Zero) return false; for(IntPtr h=GetWindow(Handle,2);h!=IntPtr.Zero;h=GetWindow(h,2)) if(h==icons) return true; return false; } }
        Transform originalTransform;
        public IntPtr Parent { get; private set; }
        public IntPtr Handle { get { return source == null ? IntPtr.Zero : source.Handle; } }
        public bool Attached
        {
            get
            {
                if(source==null || !IsWindow(Handle)) return false;
                IntPtr actual=GetParent(Handle);
                // Explorer moves DefView and its sibling children between Progman
                // and WorkerW on Show Desktop. Follow the actual desktop parent.
                if(actual!=Parent && actual!=IntPtr.Zero && FindWindowEx(actual,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero) { Parent=actual; RaiseInDesktop(); }
                return IsWindow(Parent) && actual==Parent;
            }
        }
        public bool Visible { get { return Attached && IsWindowVisible(Handle); } }
        public DesktopHost(WidgetWindow widget) { window=widget; }
        public static IntPtr FindHost()
        {
            var app=Application.Current as WidgetApp; if(app!=null && app.Verifying && FailForVerification) return IntPtr.Zero;
            IntPtr result=IntPtr.Zero;
            EnumWindows(delegate(IntPtr h,IntPtr l) { if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero) { result=h; return false; } return true; },IntPtr.Zero);
            return result;
        }
        public static bool FailForVerification;
        public void Attach()
        {
            if(Attached) return;
            Dispose(); Parent=FindHost(); if(Parent==IntPtr.Zero) throw new InvalidOperationException("未找到 Explorer 桌面宿主。");
            IntPtr context=GetWindowDpiAwarenessContext(Parent);
            IntPtr previous=SetThreadDpiAwarenessContext(context);
            if(previous==IntPtr.Zero) throw new InvalidOperationException("无法建立兼容的桌面 DPI 上下文。");
            Native.Rect bounds; Native.GetWindowRect(new WindowInteropHelper(window).Handle,out bounds);
            var point=new Point { X=bounds.Left,Y=bounds.Top }; ScreenToClient(Parent,ref point);
            try
            {
                source=new HwndSource(new HwndSourceParameters(window.Title) { ParentWindow=Parent,WindowStyle=0x44000000,UsesPerPixelTransparency=true,TreatAsInputRoot=true,PositionX=point.X,PositionY=point.Y,Width=bounds.Right-bounds.Left,Height=bounds.Bottom-bounds.Top });
                source.CompositionTarget.RenderMode=RenderMode.SoftwareOnly;
                source.CompositionTarget.BackgroundColor=Colors.Transparent;
                root=(FrameworkElement)window.Content; window.Content=null;
                double scale=Native.Scale(source.Handle);
                root.Width=window.Width; root.Height=window.Height; originalTransform=root.LayoutTransform;
                double windowScale=Native.GetDpiForWindow(new WindowInteropHelper(window).Handle)/96.0;
                root.LayoutTransform=new ScaleTransform(windowScale/scale,windowScale/scale); root.UseLayoutRounding=true;
                System.Windows.Documents.TextElement.SetFontFamily(root,window.FontFamily);
                root.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,"Ink");
                // The HwndSource root itself must not carry the DPI layout transform.
                surface=new Grid { Background=Brushes.Transparent };
                // Register this independent visual tree as an owner of application
                // resources so dynamic theme changes invalidate its descendants too.
                surface.Resources.MergedDictionaries.Add(Application.Current.Resources);
                surface.Children.Add(root);
                root.PreviewMouseDown+=FocusOnMouse;
                source.RootVisual=surface; window.Hide(); SetVisible(window.State.Visible);
                RefreshName();
                if(!Attached) throw new InvalidOperationException("桌面挂接校验失败。");
            }
            catch { Dispose(); throw; }
            finally { SetThreadDpiAwarenessContext(previous); }
        }
        void RaiseInDesktop() { if(source!=null && IsWindow(Handle)) SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x13); }
        public bool FocusContent() { if(!Attached) return false; SetFocus(Handle); return GetFocus()==Handle; }
        void FocusOnMouse(object sender,MouseButtonEventArgs e) { FocusContent(); }
        public void SetVisible(bool visible) { if(Attached) { ShowWindow(Handle,visible?4:0); if(visible) RaiseInDesktop(); } }
        public void Dispose()
        {
            if(source!=null) { if(!source.IsDisposed) { source.RootVisual=null; source.Dispose(); } source=null; }
            if(surface!=null) { surface.Children.Clear(); surface.Resources.MergedDictionaries.Clear(); surface=null; }
            if(root!=null) { root.PreviewMouseDown-=FocusOnMouse; root.Width=Double.NaN; root.Height=Double.NaN; root.LayoutTransform=originalTransform; window.Content=root; root=null; }
            Parent=IntPtr.Zero;
        }
    }
}
