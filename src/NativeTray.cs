using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopWidgets
{
    // Shell tray integration uses an existing WPF dispatcher, avoiding a second UI framework.
    public sealed class NativeTray : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct IconData
        {
            public uint Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint VersionOrTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
            public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint operation, ref IconData data);
        [DllImport("user32.dll")] static extern IntPtr LoadIcon(IntPtr instance, IntPtr resource);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string text);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        readonly HwndSource source;
        readonly Action explorerReady;
        public readonly TrayClicks Clicks;
        readonly Func<ContextMenu> menuFactory;
        readonly uint taskbarCreated;
        IconData data;
        ContextMenu menu;
        IntPtr brandIcon;
        public bool Added { get; private set; }
        public NativeTray(Action openManager, Action openNote, Func<ContextMenu> contextMenu, Action explorerRestarted)
        {
            menuFactory = contextMenu; explorerReady=explorerRestarted; Clicks=new TrayClicks(openManager,openNote);
            source = new HwndSource(new HwndSourceParameters("DesktopWidgets.Tray") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) });
            source.AddHook(Hook); taskbarCreated = RegisterWindowMessage("TaskbarCreated");
            brandIcon = Icons.TrayIcon();
            data = new IconData { Size = (uint)Marshal.SizeOf(typeof(IconData)), Window = source.Handle, Id = 1, Flags = 7, Callback = 0x8001, Icon = brandIcon != IntPtr.Zero ? brandIcon : LoadIcon(IntPtr.Zero, new IntPtr(32512)), Tip = "桌面小组件 · 点击打开管理", Info = "", Title = "" };
            Add();
        }
        void Add()
        {
            data.Flags = 7; Added = Shell_NotifyIcon(0, ref data);
            if (!Added) throw new InvalidOperationException("无法创建托盘图标。");
            data.VersionOrTimeout = 4; Shell_NotifyIcon(4, ref data);
        }
        IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if ((uint)msg == taskbarCreated) { Add(); source.Dispatcher.BeginInvoke(explorerReady,DispatcherPriority.ApplicationIdle); handled = true; }
            if(msg==0x1A || msg==0x320 || msg==0x31A) RefreshIcon();
            if (msg == 0x8001)
            {
                int notification = (int)(l.ToInt64() & 0xffff);
                Clicks.Notify(notification);
                if (notification == 0x7b || notification == 0x205) OpenMenu();
                handled = true;
            }
            return IntPtr.Zero;
        }
        public void OpenMenu()
        {
            if (menu != null && menu.IsOpen) return;
            SetForegroundWindow(source.Handle);
            menu = menuFactory(); menu.Placement = PlacementMode.MousePoint;
            menu.Closed += delegate { if (menu != null) { menu.Items.Clear(); menu = null; } };
            menu.IsOpen = true;
        }
        public void Balloon(string title, string text)
        {
            data.Flags = 0x10; data.Title = title; data.Info = text; data.InfoFlags = 0x11; // NIIF_INFO | NIIF_NOSOUND: timer plays one explicit sound
            data.VersionOrTimeout = 8000; Shell_NotifyIcon(1, ref data);
            data.Title = data.Info = ""; data.VersionOrTimeout = 4;
        }
        public void Dispose()
        {
            Clicks.Dispose();
            if (menu != null) menu.IsOpen = false;
            if (Added) { Shell_NotifyIcon(2, ref data); Added = false; }
            source.Dispose();
            if (brandIcon != IntPtr.Zero) { Icons.DestroyIcon(brandIcon); brandIcon = IntPtr.Zero; }
        }
        public void RefreshIcon()
        {
            if(brandIcon==IntPtr.Zero) return;
            data.Icon=brandIcon; data.Flags=2; if(Added) Shell_NotifyIcon(1,ref data);
        }
    }
    public sealed class TrayClicks : IDisposable
    {
        [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
        readonly Action manager,note;
        readonly DispatcherTimer single;
        DateTime suppressUntil;
        public int Delay { get; private set; }
        public TrayClicks(Action showManager,Action showNote)
        {
            manager=showManager; note=showNote; Delay=(int)GetDoubleClickTime(); single=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(Delay) }; single.Tick+=delegate { single.Stop(); manager(); };
        }
        public void Notify(int notification)
        {
            if(notification==0x401) { single.Stop(); manager(); }
            else if(notification==0x203) { single.Stop(); suppressUntil=DateTime.UtcNow.AddMilliseconds(Delay); note(); }
            else if(notification==0x400 && DateTime.UtcNow>=suppressUntil) { single.Stop(); single.Start(); }
        }
        public void Dispose() { single.Stop(); }
    }
}
