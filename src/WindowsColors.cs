using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace DesktopWidgets
{
    // Windows SDK IUISettings3 ABI: no WinRT SDK or third-party dependency is shipped.
    // Reference: microsoft/win32metadata windows.ui.viewmanagement.h, IUISettings3.
    [StructLayout(LayoutKind.Sequential)] public struct WinColor { public byte A, R, G, B; }
    [ComImport, Guid("03021be4-5254-4781-8194-5168f7d06d7b"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    interface IUISettings3
    {
        WinColor GetColorValue(int type);
        long AddColorValuesChanged([MarshalAs(UnmanagedType.Interface)] IColorChanged handler);
        void RemoveColorValuesChanged(long token);
    }
    [ComVisible(true), Guid("2dbdba9d-20da-519d-9078-09f835bc5bc7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IColorChanged { [PreserveSig] int Invoke(IntPtr sender, IntPtr args); }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class ColorChangedHandler : IColorChanged
    {
        readonly Action changed;
        public ColorChangedHandler(Action action) { changed = action; }
        public int Invoke(IntPtr sender, IntPtr args) { try { changed(); } catch { } return 0; }
    }
    public sealed class WindowsColors : IDisposable
    {
        [DllImport("combase.dll")] static extern int RoInitialize(uint type);
        [DllImport("combase.dll")] static extern void RoUninitialize();
        [DllImport("combase.dll", CharSet = CharSet.Unicode)] static extern int WindowsCreateString(string text, int length, out IntPtr value);
        [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr value);
        [DllImport("combase.dll")] static extern int RoActivateInstance(IntPtr name, out IntPtr instance);
        IUISettings3 settings;
        ColorChangedHandler handler;
        long token;
        bool subscribed, initialized;
        public bool Listening { get { return subscribed; } }
        public string Error { get; private set; }
        readonly Action changed;
        public WindowsColors(Action action) { changed = action; TryConnect(); }
        public void TryConnect()
        {
            if (settings != null) return;
            IntPtr name = IntPtr.Zero, instance = IntPtr.Zero;
            try
            {
                if (!initialized) { int hr = RoInitialize(0); if (hr >= 0) initialized = true; else if (hr != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(hr); }
                const string cls = "Windows.UI.ViewManagement.UISettings";
                Marshal.ThrowExceptionForHR(WindowsCreateString(cls, cls.Length, out name));
                Marshal.ThrowExceptionForHR(RoActivateInstance(name, out instance));
                settings = (IUISettings3)Marshal.GetObjectForIUnknown(instance);
                handler = new ColorChangedHandler(changed);
                token = settings.AddColorValuesChanged(handler); subscribed = true;
                Error = null;
            }
            catch (Exception ex) { Error = ex.Message; DisposeSettings(); }
            finally { if (instance != IntPtr.Zero) Marshal.Release(instance); if (name != IntPtr.Zero) WindowsDeleteString(name); }
        }
        public Color Get(int type)
        {
            if (settings == null) throw new InvalidOperationException(Error ?? L.T("系统配色接口不可用。"));
            WinColor c = settings.GetColorValue(type);
            return Color.FromRgb(c.R, c.G, c.B);
        }
        void DisposeSettings()
        {
            if (settings != null)
            {
                if (subscribed) try { settings.RemoveColorValuesChanged(token); } catch { }
                subscribed = false; Marshal.ReleaseComObject(settings); settings = null;
            }
            handler = null;
        }
        public void Dispose() { DisposeSettings(); if (initialized) { RoUninitialize(); initialized = false; } }
    }
}
