using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;

namespace DesktopWidgets
{
    public static class Native
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
        public static int CaptionMode(Window window) { int value; IntPtr h=new WindowInteropHelper(window).Handle; if(DwmGetWindowAttribute(h,20,out value,4)!=0 && DwmGetWindowAttribute(h,19,out value,4)!=0) return -1; return value; }
        public static void StyleWindow(Window window) { window.SourceInitialized += delegate { ApplyCaption(window); }; }
        public static void ApplyCaption(Window window)
        {
            IntPtr h=new WindowInteropHelper(window).Handle; if(h==IntPtr.Zero) return;
            var app=Application.Current as WidgetApp; int dark=app!=null && app.Theme!=null && app.Theme.Dark && !SystemParameters.HighContrast?1:0;
            if(DwmSetWindowAttribute(h,20,ref dark,4)!=0) DwmSetWindowAttribute(h,19,ref dark,4);
        }
        [StructLayout(LayoutKind.Sequential)] public struct FileTime { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }
        [StructLayout(LayoutKind.Sequential)] public struct MemoryStatus
        { public uint Length, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [StructLayout(LayoutKind.Sequential)] public struct ProcessorPower { public uint Number,MaxMhz,CurrentMhz,MhzLimit,MaxIdleState,CurrentIdleState; }
        [DllImport("kernel32.dll")] public static extern uint GetActiveProcessorCount(ushort group);
        [DllImport("powrprof.dll")] public static extern uint CallNtPowerInformation(int level,IntPtr input,uint inputBytes,[Out] ProcessorPower[] output,uint outputBytes);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromRect(ref Rect rect, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect rect);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
        [StructLayout(LayoutKind.Sequential)] public struct CursorPoint { public int X,Y; }
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out CursorPoint point);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        public static bool AltDown { get { return (GetAsyncKeyState(0x12)&0x8000)!=0; } }
        public static double Scale(IntPtr h) { double scale=GetDpiForWindow(h)/96.0; return scale>0?scale:1; }
        public static Rect WorkingArea(Window w)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            IntPtr h = new WindowInteropHelper(w).Handle;
            if (!GetMonitorInfo(MonitorFromWindow(h, 2), ref info)) throw new InvalidOperationException(L.T("无法读取屏幕工作区。"));
            return info.Work;
        }
        public static Rect WorkingArea(Rect proposed)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromRect(ref proposed, 2), ref info)) throw new InvalidOperationException(L.T("无法读取目标屏幕工作区。"));
            return info.Work;
        }
        public static void Clamp(Window window)
        {
            IntPtr h = new WindowInteropHelper(window).Handle;
            if (h == IntPtr.Zero) return;
            Rect r = WorkingArea(window);
            double scale = Scale(h);
            window.MaxHeight = Math.Max(window.MinHeight, (r.Bottom - r.Top) / scale);
            window.MaxWidth = Math.Max(window.MinWidth, (r.Right - r.Left) / scale);
            if (window.Height > window.MaxHeight) window.Height = window.MaxHeight;
            if (window.Width > window.MaxWidth) window.Width = window.MaxWidth;
            // WPF Left/Top are DIPs in the current monitor transform; SetWindowPos uses physical pixels.
            Rect bounds;
            if (!GetWindowRect(h, out bounds)) return;
            int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
            int x = bounds.Left, y = bounds.Top;
            x = Math.Max(r.Left, Math.Min(x, r.Right - width));
            y = Math.Max(r.Top, Math.Min(y, r.Bottom - height));
            SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x15); // no resize, no z-order, no activate
        }
        public static void SetAutoStart(bool enabled, string executable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled) key.SetValue("ChineseDesktopWidgets", "\"" + executable + "\"", RegistryValueKind.String);
                else key.DeleteValue("ChineseDesktopWidgets", false);
            }
        }
    }
    public sealed class SystemSampler
    {
        ulong lastTotal, lastIdle;
        bool baseline;
        Native.ProcessorPower[] processors;
        public double? FrequencyGHz()
        {
            try
            {
                if(processors==null) { uint count=Native.GetActiveProcessorCount(0xffff); if(count==0 || count>4096) return null; processors=new Native.ProcessorPower[(int)count]; }
                if(Native.CallNtPowerInformation(11,IntPtr.Zero,0,processors,(uint)(processors.Length*Marshal.SizeOf(typeof(Native.ProcessorPower))))!=0) return null;
                double sum=0; int valid=0; foreach(var processor in processors) if(processor.CurrentMhz>0) { sum+=processor.CurrentMhz; valid++; }
                return valid==0?(double?)null:sum/valid/1000.0;
            }
            catch { return null; }
        }
        public int Samples { get; private set; }
        public void Reset() { baseline = false; }
        public double? Cpu()
        {
            Native.FileTime idle, kernel, user;
            if (!Native.GetSystemTimes(out idle, out kernel, out user)) return null;
            ulong total = kernel.Value + user.Value;
            double? result = null;
            if (baseline && total > lastTotal) result = Math.Max(0, Math.Min(100, 100.0 * (1 - (double)(idle.Value - lastIdle) / (total - lastTotal))));
            baseline = true; lastTotal = total; lastIdle = idle.Value; Samples++;
            return result;
        }
        public Native.MemoryStatus Memory()
        {
            var m = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(Native.MemoryStatus)) };
            if (!Native.GlobalMemoryStatusEx(ref m)) throw new InvalidOperationException(L.T("内存采样失败。"));
            return m;
        }
    }
}
