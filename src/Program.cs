using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DesktopWidgets
{
    public sealed class WidgetApp : Application
    {
        public static readonly string[] Ids = { "clock", "countdown", "todo", "note", "status", "shortcuts" };
        public static readonly string[] Names = { "时间", "倒计时", "待办", "便签", "系统状态", "快捷入口" };
        public static bool IsShortcutInstance(string id) { Guid value; return id!=null && id.StartsWith("shortcuts-",StringComparison.Ordinal) && id.Length==42 && Guid.TryParseExact(id.Substring(10),"N",out value); }
        public static string KindOf(string id) { return IsShortcutInstance(id)?"shortcuts":id; }
        public Settings Settings;
        public BusinessData Data;
        public Storage Storage;
        public Theme Theme;
        public readonly List<WidgetWindow> Widgets = new List<WidgetWindow>();
        public ClockWidget Clock;
        public CountdownWidget Countdown;
        public TodoWidget Todo;
        public NoteWidget Note;
        public StatusWidget Status;
        public ShortcutWidget Shortcuts;
        public bool Exiting, Verifying, Measuring;
        public string LastError;
        NativeTray tray;
        public ManagerWindow Manager { get; private set; }
        public event Action WidgetsChanged;
        DispatcherTimer scheduler;
        DateTime noteDue = DateTime.MaxValue, settingsDue = DateTime.MaxValue;
        DateTime lastClock = DateTime.MinValue, lastSample = DateTime.MinValue;
        bool dataDirty, settingsDirty, saveErrorShown, previousStatusVisible;
        Window timerNotice;
        readonly string executable = Process.GetCurrentProcess().MainModule.FileName;

        [STAThread] public static int Main(string[] args)
        {
            L.Select("system");
            bool measure = args.Length > 0 && args[0].StartsWith("--measure");
            bool verification = args.Length > 0 && (args[0] == "--verify" || args[0] == "--measure" || args[0] == "--measure-hardware" || args[0]=="--measure-desktop" || args[0]=="--measure-groups");
            bool manage = args.Length > 0 && args[0] == "--manage";
            if (args.Length > 0 && !verification && !manage) { MessageBox.Show(L.T("未知参数。请直接运行程序。"), L.T("桌面小组件")); return 2; }
            // Small, mostly static cards need no GPU scene resources; compare with --measure-hardware in diagnostics.
            RenderOptions.ProcessRenderMode = args.Length > 0 && args[0] == "--measure-hardware" ? RenderMode.Default : RenderMode.SoftwareOnly;
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string dataPath = Path.Combine(basePath, "data");
            if (verification) dataPath = Path.Combine(basePath, "verification-data-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            bool created;
            string mutexName = @"Local\ChineseDesktopWidgets-v1" + (verification ? (measure ? "-measure-" + Guid.NewGuid().ToString("N") : "-verification") : "");
            using (var mutex = new Mutex(true, mutexName, out created))
            using (var restore = new EventWaitHandle(false, EventResetMode.AutoReset, mutexName + "-restore"))
            {
                if (!created)
                {
                    restore.Set();
                    return 0;
                }
                try
                {
                    var app = new WidgetApp { Verifying = verification, Measuring = verification && measure, ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Initialize(dataPath);
                    if(args.Length>0 && args[0]=="--measure-desktop") foreach(var w in app.Widgets) w.SetMode(3);
                    if(args.Length>0 && args[0]=="--measure-groups") { app.AddShortcutGroup("工作入口").Add("程序",Process.GetCurrentProcess().MainModule.FileName,"software"); app.AddShortcutGroup("资料入口").Add("资料",dataPath,"folder"); }
                    var registration = ThreadPool.RegisterWaitForSingleObject(restore, delegate
                    {
                        if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(delegate { app.RestoreAll(); if (!app.Verifying) app.ShowManager(); }));
                    }, null, Timeout.Infinite, false);
                    try
                    {
                        if (verification) Diagnostics.Schedule(app, measure);
                        if (manage) app.ShowManager();
                        app.Run();
                    }
                    finally { registration.Unregister(null); }
                    return Diagnostics.Failed ? 1 : 0;
                }
                catch (Exception ex)
                {
                    string message = L.T("启动失败：{0}\n请确认程序所在目录可写，且已安装 .NET Framework 4.8。数据不会改存其他目录。",ex.Message);
                    if (verification) File.WriteAllText(Path.Combine(basePath, "verification-fatal.txt"), ex.ToString()); else MessageBox.Show(message, L.T("桌面小组件"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        public void Initialize(string directory)
        {
            Storage = new Storage(directory);
            Data = Storage.Load("business.json", BusinessData.Valid, delegate { return new BusinessData(); });
            Settings = Storage.Load("settings.json", Settings.Valid, delegate { return new Settings(); });
            Settings.Migrate(); L.Select(Settings.Language);
            foreach(string group in Data.Shortcuts.Where(s=>s.GroupId!=null).Select(s=>s.GroupId).Distinct()) if(!Settings.Windows.Any(w=>w.Id==group)) { Settings.Windows.Add(NewShortcutState(group,L.T("恢复的快捷入口"))); Storage.RecoveryNotice+=L.T("已恢复缺失布局的快捷入口组件，内容保留。\n"); }
            Theme = new Theme(this);
            var work = SystemParameters.WorkArea;
            for (int i = 0; i < Ids.Length; i++)
            {
                WindowState state = Settings.Windows.Find(w => w.Id == Ids[i]);
                if (state == null)
                {
                    var anchor = Settings.Windows.Find(w => w.Id == "clock") ?? new WindowState { Width = 280, Height = 185, Left = work.Left + 12, Top = work.Top + 12 };
                    var point = Layout.FreeNear(work, anchor, Settings.Windows, 280, Layout.DefaultHeight(Ids[i]));
                    state = new WindowState { Id = Ids[i], Width = 280, Height = Layout.DefaultHeight(Ids[i]), Left = point.X, Top = point.Y };
                    if (Ids[i] == "countdown") { state.Visible = anchor.Visible; state.Pinned = anchor.Pinned; }
                    Settings.Windows.Add(state);
                }
                state.Height = Math.Max(Layout.MinimumHeight(state.Id), Math.Min(state.Height, work.Height));
                state.Width = Math.Max(240, Math.Min(state.Width, work.Width));
            }
            Clock = new ClockWidget(this, Settings.Windows.Find(w => w.Id == "clock")); Widgets.Add(Clock);
            Countdown = new CountdownWidget(this, Settings.Windows.Find(w => w.Id == "countdown")); Widgets.Add(Countdown);
            Todo = new TodoWidget(this, Settings.Windows.Find(w => w.Id == "todo")); Widgets.Add(Todo);
            Note = new NoteWidget(this, Settings.Windows.Find(w => w.Id == "note")); Widgets.Add(Note);
            Status = new StatusWidget(this, Settings.Windows.Find(w => w.Id == "status")); Widgets.Add(Status);
            Shortcuts = new ShortcutWidget(this, Settings.Windows.Find(w => w.Id == "shortcuts")); Widgets.Add(Shortcuts);
            foreach(var state in Settings.Windows.Where(w=>IsShortcutInstance(w.Id))) { state.Width=Math.Max(240,Math.Min(state.Width,work.Width)); state.Height=Math.Max(Layout.MinimumHeight(state.Id),Math.Min(state.Height,work.Height)); Widgets.Add(new ShortcutWidget(this,state)); }
            BuildTray();
            foreach (var window in Widgets)
            {
                // Create handles to restore placement and receive Windows display/theme notifications.
                new WindowInteropHelper(window).EnsureHandle();
                if (window.State.Visible) window.Show();
            }
            foreach(var window in Widgets) if(window.State.Mode==3) window.SetMode(3);
            SystemEvents.DisplaySettingsChanged += DisplayChanged; SystemEvents.UserPreferenceChanged += LanguagePreferenceChanged;
            DispatcherUnhandledException += Unhandled;
            scheduler = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            scheduler.Tick += Tick; UpdateSchedule();
            if (!Verifying)
            {
                try { Native.SetAutoStart(Settings.AutoStart, executable); }
                catch (Exception ex) { Error(L.T("无法更新登录自启。\n") + ex.Message); }
            }
            Settings.Initialized = true;
            SaveData(); SaveSettings();
            if (Storage.RecoveryNotice.Length > 0) Error(Storage.RecoveryNotice);
        }
        void Tick(object sender, EventArgs args)
        {
            DateTime now = DateTime.UtcNow;
            Countdown.Tick(now);
            if (Clock.IsVisible && (now - lastClock).TotalSeconds >= 1) { Clock.Refresh(DateTime.Now); lastClock = now; }
            if (Status.IsVisible)
            {
                if (!previousStatusVisible) { Status.ResetSampling(); lastSample = DateTime.MinValue; }
                if ((now - lastSample).TotalSeconds >= 2) { Status.Sample(); lastSample = now; }
            }
            previousStatusVisible = Status.IsVisible;
            if (dataDirty && now >= noteDue && !SaveData()) Note.Saved(false);
            if (settingsDirty && now >= settingsDue) SaveSettings();
            UpdateSchedule();
        }
        public void ScheduleNote() { dataDirty = true; noteDue = DateTime.UtcNow.AddMilliseconds(500); UpdateSchedule(); }
        public void ScheduleSettings() { settingsDirty = true; settingsDue = DateTime.UtcNow.AddMilliseconds(500); UpdateSchedule(); }
        public void UpdateSchedule()
        {
            if (scheduler == null || Clock == null || Status == null || Exiting) return;
            double interval = dataDirty || settingsDirty ? .1 : Clock.IsVisible || Countdown.IsVisible || Countdown.Timer.Running ? 1 : Status.IsVisible ? 2 : 0;
            if (interval == 0) { scheduler.Stop(); return; }
            TimeSpan wanted = TimeSpan.FromSeconds(interval); if (scheduler.Interval != wanted) scheduler.Interval = wanted;
            if (!scheduler.IsEnabled) scheduler.Start();
        }
        public bool SaveData()
        {
            try { Storage.Save("business.json", Data); dataDirty = false; noteDue = DateTime.MaxValue; saveErrorShown = false; if (Note != null) Note.Saved(true); return true; }
            catch (Exception ex) { dataDirty = true; noteDue = DateTime.UtcNow.AddSeconds(30); SaveError(ex); return false; }
        }
        public bool SaveSettings()
        {
            try { Storage.Save("settings.json", Settings); settingsDirty = false; settingsDue = DateTime.MaxValue; saveErrorShown = false; return true; }
            catch (Exception ex) { settingsDirty = true; settingsDue = DateTime.UtcNow.AddSeconds(30); SaveError(ex); return false; }
        }
        void SaveError(Exception ex)
        { if (!saveErrorShown) { saveErrorShown = true; Error(L.T("保存失败，修改暂时只在内存中。请恢复目录写入权限后再退出。\n") + ex.Message); } }
        public void Error(string message)
        { LastError = message; if (!Verifying) MessageBox.Show(message, L.T("桌面小组件"), MessageBoxButton.OK, MessageBoxImage.Information); }
        public void Balloon(string title, string message) { if (tray != null) tray.Balloon(title, message); }
        public void ShowTimerNotice()
        {
            if (timerNotice != null) return;
            timerNotice = new Window { Title = L.T("倒计时结束"), Width = 300, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = true };
            L.Bind(timerNotice,Window.TitleProperty,"倒计时结束");
            timerNotice.SetResourceReference(Window.BackgroundProperty, "SolidCard");
            timerNotice.SetResourceReference(Control.ForegroundProperty, "Ink");
            Native.StyleWindow(timerNotice);
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) }; panel.Children.Add(Ui.Label("倒计时结束", 20)); panel.Children.Add(Ui.Label("设定的时间已到。", 14)); panel.Children.Add(Ui.LocalButton("知道了", "关闭提示", delegate { timerNotice.Close(); })); timerNotice.Content = panel;
            timerNotice.Closed += delegate { timerNotice = null; }; timerNotice.Show();
        }
        public void RestoreAll() { foreach (var window in Widgets) window.SetVisible(true); }
        public void HideAll() { foreach (var window in Widgets) window.SetVisible(false); }
        public void NotifyWidgetsChanged()
        {
            if (Status != null && Status.IsVisible && !previousStatusVisible) { Status.ResetSampling(); Status.Sample(); lastSample = DateTime.UtcNow; }
            if (Status != null) previousStatusVisible = Status.IsVisible;
            UpdateSchedule(); if (WidgetsChanged != null) WidgetsChanged();
        }
        public void ShowManager()
        {
            if (Manager == null) Manager = new ManagerWindow(this);
            Manager.Show(); if (Manager.WindowState == System.Windows.WindowState.Minimized) Manager.WindowState = System.Windows.WindowState.Normal; Manager.Activate();
        }
        public void ManagerClosed(ManagerWindow window) { if (Object.ReferenceEquals(Manager, window)) Manager = null; }
        WindowState NewShortcutState(string id,string name)
        {
            var work=SystemParameters.WorkArea; var anchor=Settings.Windows.Find(w=>w.Id=="shortcuts")??new WindowState { Left=work.Left+12,Top=work.Top+12,Width=280,Height=220 };
            var point=Layout.FreeNear(work,anchor,Settings.Windows,280,220); return new WindowState { Id=id,DisplayName=name,Left=point.X,Top=point.Y,Width=280,Height=220 };
        }
        public ShortcutWidget AddShortcutGroup(string name)
        {
            name=(name??"").Trim(); if(name.Length<1 || name.Length>32) return null;
            var state=NewShortcutState("shortcuts-"+Guid.NewGuid().ToString("N"),name); Settings.Windows.Add(state); var widget=new ShortcutWidget(this,state); Widgets.Add(widget); new WindowInteropHelper(widget).EnsureHandle(); widget.Show(); widget.Clamp(); SaveSettings(); NotifyWidgetsChanged(); return widget;
        }
        public void ManageShortcuts(ShortcutWidget group) { ShowManager(); Manager.SelectShortcutGroup(group); }
        public void SetLanguage(string code)
        {
            if(Array.IndexOf(L.Codes,code)<0) return;
            Settings.Language=code=="system"?null:code; L.Select(code); RefreshLanguage(); SaveSettings();
        }
        void LanguagePreferenceChanged(object sender,UserPreferenceChangedEventArgs e)
        {
            if(Settings.Language!=null || Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(delegate { if(Exiting || Settings.Language!=null) return; string before=L.Code; L.Select("system"); if(L.Code!=before) RefreshLanguage(); }));
        }
        void RefreshLanguage()
        {
            foreach(var widget in Widgets) widget.RefreshLanguage();
            Clock.Refresh(DateTime.Now); Countdown.Refresh(); Todo.Refresh();
            foreach(var widget in Widgets.OfType<ShortcutWidget>()) widget.Refresh();
            if(Manager!=null) { Manager.Refresh(); Manager.RefreshMemory(); }
            if(tray!=null) tray.RefreshLanguage();
        }
        public void SetTheme(string mode) { Settings.Theme = mode; Theme.Refresh(); SaveSettings(); NotifyWidgetsChanged(); }
        public void SetOpacity(double value) { Settings.Opacity = Math.Max(.35, Math.Min(1, value)); Theme.Refresh(); ScheduleSettings(); NotifyWidgetsChanged(); }
        public void ArrangeWidgets()
        {
            if(!Widgets.Any(w=>w.IsVisible && w.State.Mode==1)) { Error(L.T("没有可整理组件，请先显示组件并解除锁定。")); return; }
            if(!Ui.Confirm(Manager,"是否重置组件位置？")) return;
            ArrangeConfirmed();
        }
        public void ArrangeConfirmed()
        {
            Rect work = SystemParameters.WorkArea; var placed = Settings.Windows.Where(w=>w.Visible && w.Mode!=1).ToList(); var changes=new List<WindowState>(); var anchor = new WindowState { Left = work.Left + 12, Top = work.Top + 12, Width = 280, Height = 185 };
            foreach (string id in new[] { "clock", "todo", "shortcuts", "countdown", "note", "status" }.Concat(Widgets.Where(w=>IsShortcutInstance(w.State.Id)).Select(w=>w.State.Id)))
            {
                var w = Widgets.Find(v => v.State.Id == id); if(!w.IsVisible || w.State.Mode!=1) continue; var point = Layout.FreeNear(work, anchor, placed, w.Width, w.Height);
                var rect=new Rect(point.X,point.Y,w.Width,w.Height); if(placed.Any(v=>v.Visible && new Rect(v.Left,v.Top,v.Width,v.Height).IntersectsWith(rect))) { Error(L.T("屏幕空间不足，未重置位置。请先隐藏部分组件或调整尺寸。")); return; }
                var state=new WindowState { Id=id,Left=point.X,Top=point.Y,Width=w.Width,Height=w.Height,Visible=true }; placed.Add(state); changes.Add(state);
            }
            foreach(var state in changes) { var w=Widgets.Find(v=>v.State.Id==state.Id); w.Left=state.Left; w.Top=state.Top; w.Clamp(); }
            SaveSettings(); NotifyWidgetsChanged();
        }
        public bool ChangeAutoStart(bool enabled)
        {
            try { if (!Verifying) Native.SetAutoStart(enabled, executable); Settings.AutoStart = enabled; return SaveSettings(); }
            catch (Exception ex) { Error(L.T("无法修改登录自启。\n") + ex.Message); return false; }
        }
        void BuildTray()
        {
            if (Verifying && !Measuring) return;
            tray = new NativeTray(ShowManager, BringNoteForward, CreateTrayMenu, RestoreDesktops);
        }
        public void BringNoteForward() { Note.BringForward(); Note.Editor.Focus(); System.Windows.Input.Keyboard.Focus(Note.Editor); }
        public void RestoreDesktops() { foreach(var w in Widgets) w.RestoreDesktop(); }
        public void RefreshTrayTheme() { if(tray!=null) tray.RefreshIcon(); }
        public ContextMenu CreateTrayMenu()
        {
            var menu = new ContextMenu(); menu.SetResourceReference(Control.BackgroundProperty, "Field"); menu.SetResourceReference(Control.ForegroundProperty, "Ink");
            MenuItem manageItem = Menu("管理组件…", ShowManager); manageItem.FontWeight = FontWeights.SemiBold; menu.Items.Add(manageItem); menu.Items.Add(new Separator());
            for (int i = 0; i < Widgets.Count; i++) { WidgetWindow w = Widgets[i]; var item = Menu(w.DisplayName, delegate { w.SetVisible(!w.IsVisible); },false); item.IsChecked = w.IsVisible; menu.Items.Add(item); }
            menu.Items.Add(new Separator()); menu.Items.Add(Menu("新建快捷入口组件…",delegate { ShowManager(); Manager.PromptAddShortcutGroup(); })); menu.Items.Add(Menu("全部显示", RestoreAll)); menu.Items.Add(Menu("全部隐藏", HideAll));
            var themes = new MenuItem { Header = L.T("主题") }; string[] keys = UiChoices.ThemeKeys, labels = UiChoices.ThemeLabels;
            for (int i = 0; i < keys.Length; i++) { string key = keys[i]; var item = Menu(labels[i], delegate { SetTheme(key); }); item.IsChecked = Settings.Theme == key; themes.Items.Add(item); } menu.Items.Add(themes);
            var opacity = new MenuItem { Header = L.T("背景不透明度") }; foreach (double value in new[] { .35, .55, .70, .85, 1.0 }) { double v = value; var item = Menu((v * 100).ToString("0") + "%", delegate { SetOpacity(v); }); item.IsChecked = Math.Abs(Settings.Opacity - v) < .001; opacity.Items.Add(item); } menu.Items.Add(opacity);
            var auto = Menu("登录自启", delegate { ChangeAutoStart(!Settings.AutoStart); NotifyWidgetsChanged(); }); auto.IsChecked = Settings.AutoStart; menu.Items.Add(auto);
            menu.Items.Add(new Separator()); menu.Items.Add(Menu("退出", delegate { Quit(); })); return menu;
        }
        static MenuItem Menu(string title, Action action,bool localize=true) { var item = localize?Ui.LocalMenu(title):new MenuItem { Header=title }; item.Click += delegate { action(); }; return item; }
        void DisplayChanged(object sender, EventArgs e) { Dispatcher.BeginInvoke(new Action(delegate { foreach (var w in Widgets) { if(w.State.Mode==3 && !w.TemporaryFront) w.RestoreDesktop(); else w.Clamp(); } SaveSettings(); })); }
        void Unhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        { Error(L.T("程序发生错误：") + e.Exception.Message); e.Handled = true; }
        public bool Quit()
        {
            bool saved = SaveData(); saved = SaveSettings() && saved;
            if (!saved && !Verifying && MessageBox.Show(L.T("仍有内容保存失败，直接退出会丢失未保存修改。是否仍要退出？"), L.T("桌面小组件"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            Exiting = true; scheduler.Stop(); SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.UserPreferenceChanged -= LanguagePreferenceChanged;
            foreach (var w in Widgets) w.Close(); if (timerNotice != null) timerNotice.Close();
            if (Manager != null) Manager.Close();
            if (tray != null) tray.Dispose();
            Theme.Dispose(); Shutdown(); return true;
        }
    }
}
