using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopWidgets
{
    // Verification uses a fresh adjacent directory and skips login autostart/tray changes.
    public static partial class Diagnostics
    {
        public static bool Failed;
        static readonly List<string> report = new List<string>();
        static void Check(bool condition, string label) { report.Add((condition ? "PASS " : "FAIL ") + label); if (!condition) Failed = true; }
        static void Click(System.Windows.Controls.Primitives.ButtonBase b) { b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        public static void Schedule(WidgetApp app, bool measure)
        {
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async delegate
            {
                string root = AppDomain.CurrentDomain.BaseDirectory;
                string evidence = Path.Combine(root, "verification"); Directory.CreateDirectory(evidence);
                try
                {
                    if (measure) await Measure(app);
                    else await Verify(app, evidence);
                }
                catch (Exception ex) { Failed = true; report.Add("FAIL unexpected: " + ex); }
                finally
                {
                    report.Add("Data: " + app.Storage.DirectoryPath);
                    File.WriteAllLines(Path.Combine(evidence, measure ? "performance.txt" : "results.txt"), report, System.Text.Encoding.UTF8);
                    app.Quit();
                }
            }));
        }
        static async Task Verify(WidgetApp app, string evidence)
        {
            app.SetLanguage("zh-CN"); // Keep existing Chinese assertions independent of the host display language.
            Check(Environment.Is64BitProcess, "x64 process");
            Check(app.Widgets.Count == 6 && app.Widgets.All(w => w.IsVisible), "six windows visible");
            Check(app.Theme.Status == "UISettings 强调色", "Windows UISettings color adapter: " + app.Theme.Status);
            Check(app.Theme.ColorEvents, "Windows color event subscription");
            Check(app.Widgets.All(w => !w.Topmost), "not pinned by default");
            app.Todo.Entry.Text = "验收：整理桌面"; Click(app.Todo.AddButton);
            Check(app.Data.Todos.Count == 1 && app.Todo.Entry.Text == "", "todo add through actual button");
            TodoItem first = app.Data.Todos[0]; app.Todo.Edit(first, "验收：完成整理"); app.Todo.Complete(first, true);
            Check(first.Done && first.Text == "验收：完成整理", "todo edit and complete");
            app.Todo.Add("验收：未完成事项"); app.Settings.CollapseDone = false; app.Todo.Refresh();
            Check(app.Data.Todos.Count == 2, "todo completed expand and pending item");
            app.Note.Editor.Text = "便签自动保存验收\n中文与 emoji：你好 🌿\n第二行";
            await Task.Delay(850);
            var read = Storage.Read<BusinessData>(Path.Combine(app.Storage.DirectoryPath, "business.json"), BusinessData.Valid);
            Check(read.Note == app.Note.Editor.Text && app.Note.Status.Text.StartsWith("已保存"), "500ms note debounce persisted Unicode");
            app.Shortcuts.Add("验收文件夹", app.Storage.DirectoryPath);
            string sample = Path.Combine(app.Storage.DirectoryPath, "示例.txt"); File.WriteAllText(sample, "测试"); app.Shortcuts.Add("验收文件", sample);
            var link = app.Data.Shortcuts[1]; app.Shortcuts.Rename(link, "更名文件"); app.Shortcuts.Move(link, -1);
            Check(app.Data.Shortcuts[0] == link && link.Name == "更名文件", "shortcut add rename reorder");
            Check(!app.Shortcuts.Open(new ShortcutItem { Name = "失效入口", Path = Path.Combine(app.Storage.DirectoryPath, "missing.exe") }) && app.LastError.Contains("目标不存在"), "invalid shortcut understandable error");
            app.Shortcuts.Remove(link); Check(app.Data.Shortcuts.Count == 1, "shortcut remove");
            app.Note.SetPinned(true); Check(app.Note.Topmost && app.Note.State.Pinned, "pin window"); app.Note.SetPinned(false);
            app.Note.Height = 380; app.Note.Left = -50000; app.Note.Top = -50000; app.Note.Clamp();
            Check(app.Note.Left > -50000 && app.Note.Top > -50000 && app.Note.Height <= app.Note.MaxHeight, "off-screen location corrected");
            app.Countdown.SetDuration(60); Click(app.Countdown.StartButton); Check(app.Countdown.Timer.Running, "countdown start button");
            Click(app.Countdown.PauseButton); Check(!app.Countdown.Timer.Running, "countdown pause button"); Click(app.Countdown.StartButton); Check(app.Countdown.Timer.Running, "countdown resume button");
            Click(app.Countdown.ResetButton); Check(!app.Countdown.Timer.Running && app.Countdown.Timer.Remaining == 60, "countdown reset button");
            Check(!app.Countdown.SetDuration(0) && !app.Countdown.Timer.Running && app.Countdown.Timer.Duration==60, "invalid countdown input leaves duration unchanged"); app.Countdown.SetDuration(60);
            app.Countdown.Timer.Reset(1); app.Countdown.Timer.Start(DateTime.UtcNow); app.Countdown.SetVisible(false); app.Clock.SetVisible(false); app.Status.SetVisible(false);
            await Task.Delay(350); int refreshes = app.Clock.Refreshes, samples = app.Status.Sampler.Samples;
            await Task.Delay(2400);
            Check(app.Countdown.Notifications == 1 && !app.Countdown.Timer.Running, "hidden countdown fires once");
            Check(app.Clock.Refreshes == refreshes && app.Status.Sampler.Samples == samples, "hidden clock/status stop refresh/sampling");
            app.RestoreAll(); await Task.Delay(500);
            Check(app.Widgets.All(w => w.IsVisible) && app.Status.Sampler.Samples > samples, "restore all resumes sampling");
            app.HideAll(); Check(app.Widgets.All(w => !w.IsVisible), "hide all");
            using (var duplicate = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--verify") { UseShellExecute = false, CreateNoWindow = true }))
            {
                bool finished = await Task.Run(delegate { return duplicate.WaitForExit(5000); });
                await Task.Delay(300);
                report.Add("DETAIL duplicate finished=" + finished + ", exit=" + (finished ? duplicate.ExitCode.ToString() : "pending") + ", visible=" + app.Widgets.Count(w => w.IsVisible));
                Check(finished && duplicate.ExitCode == 0 && app.Widgets.All(w => w.IsVisible), "second process exits and restores existing hidden windows");
                if (!finished) duplicate.Kill();
            }
            app.RestoreAll();
            app.SaveData(); app.SaveSettings();
            Check(Storage.Read<Settings>(Path.Combine(app.Storage.DirectoryPath, "settings.json"), Settings.Valid).Windows.Count == 6, "layout and visibility JSON saved");
            var loaded = new Storage(app.Storage.DirectoryPath);
            Check(loaded.Load("business.json", BusinessData.Valid, delegate { return new BusinessData(); }).Note == app.Note.Editor.Text, "data reopen");
            Check(!new Countdown(app.Data.CountdownSeconds).Running, "restart countdown not running");
            StorageChecks(Path.Combine(app.Storage.DirectoryPath, "storage-tests"));
            CountdownChecks();
            try { AutoStartChecks(); }
            catch (UnauthorizedAccessException ex) { report.Add("NOT VERIFIED: login autostart registry write denied by execution environment: " + ex.Message); }
            // Trigger a real file-sharing failure, then recover without leaving data damaged.
            string target = Path.Combine(app.Storage.DirectoryPath, "business.json");
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!app.SaveData() && app.LastError.Contains("保存失败"), "actual save failure reported");
            Check(app.SaveData(), "save recovers after sharing lock removed");
            using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.None))
            { app.Note.Editor.Text="延迟保存失败再恢复"; await Task.Delay(800); Check(app.Note.Status.Text.Contains("保存失败"),"debounced save failure keeps visible note status"); }
            Check(app.SaveData() && app.Note.Status.Text.StartsWith("已保存") && Storage.Read<BusinessData>(target,BusinessData.Valid).Note==app.Note.Editor.Text,"debounced save recovery persists content and clears failure status");
            app.Settings.Theme = "light"; app.Theme.Refresh();
            Capture(app, Path.Combine(evidence, "actual-system-light.png"));
            app.Settings.Theme = "dark"; app.Theme.Refresh(); Capture(app, Path.Combine(evidence, "actual-system-dark.png"));
            int revision = app.Theme.Revision; app.Theme.QueueRefresh(); await Task.Delay(100); Check(app.Theme.Revision > revision, "color event dispatch refresh under 1 second");
            foreach (Color c in new[] { Colors.RoyalBlue, Colors.Crimson, Colors.SeaGreen })
            {
                foreach (bool dark in new[] { false, true })
                {
                    app.Theme.ApplyPalette(c, Theme.Blend(c, dark ? Colors.Black : Colors.White, .55), dark, false, true);
                    Check(((SolidColorBrush)app.Resources["Card"]).Color.A == 217 && ((SolidColorBrush)app.Resources["Ink"]).Color.A == 255, "alpha only background " + c + "/" + dark);
                    Capture(app, Path.Combine(evidence, "palette-" + c.R + "-" + (dark ? "dark" : "light") + ".png"));
                }
            }
            app.Theme.ApplyPalette(Colors.Blue, Colors.LightBlue, false, false, false); Check(((SolidColorBrush)app.Resources["Card"]).Color.A == 255, "transparency disabled yields solid card");
            app.Theme.ApplyPalette(Colors.Blue, Colors.LightBlue, false, true, true); Check(((SolidColorBrush)app.Resources["Card"]).Color == SystemColors.WindowColor && ((SolidColorBrush)app.Resources["Ink"]).Color == SystemColors.WindowTextColor, "high contrast uses system colors");
            Capture(app, Path.Combine(evidence, "high-contrast-simulation.png")); app.Settings.Theme = "system"; app.Theme.Refresh();
            app.Todo.Delete(first); Check(app.Data.Todos.Count == 1, "todo delete");
            app.Note.Editor.Text = String.Join("\n", Enumerable.Repeat("长便签：滚动与自动保存验收。", 50)); await Task.Delay(800);
            Check(app.Note.Editor.ExtentHeight > app.Note.Editor.ViewportHeight, "long note scrolls");
            app.Note.Editor.Text = "便签自动保存验收\n中文与 emoji：你好 🌿\n第二行"; await Task.Delay(800);
            await ManagerChecks(app, evidence);
            await V12Checks(app, evidence); await V13Checks(app,evidence);
            report.Add("LIMIT: palette and high contrast were simulated; physical Windows theme, sleep/resume, DPI and multi-monitor changes require manual acceptance.");
            report.Add("LIMIT: file/folder picker and real target launch require interactive acceptance.");
        }
        static void StorageChecks(string dir)
        {
            var store = new Storage(dir); var data = new BusinessData { Note = "旧有效内容" }; store.Save("business.json", data); data.Note = "新内容"; store.Save("business.json", data);
            string path = Path.Combine(dir, "business.json"); File.WriteAllText(path, "broken");
            var restored = store.Load("business.json", BusinessData.Valid, delegate { return new BusinessData(); });
            Check(restored.Note == "旧有效内容" && Directory.GetFiles(dir, "*.corrupt-*").Length == 1, "corrupt primary restored from last valid backup and retained");
            File.WriteAllText(path, "broken2"); File.WriteAllText(path + ".bak", "broken-backup"); bool rejected = false;
            try { store.Load("business.json", BusinessData.Valid, delegate { return new BusinessData(); }); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == "broken2", "both corrupt rejected without overwriting original");
            string migrated = dir + "-moved"; Directory.Move(dir, migrated); var moved = new Storage(migrated); moved.Save("business.json", new BusinessData { Note = "迁移内容" });
            Check(moved.Load("business.json", BusinessData.Valid, delegate { return new BusinessData(); }).Note == "迁移内容", "data directory move and reopen");
            Check(!BusinessData.Valid(new BusinessData { Version = 2 }) && !Settings.Valid(new Settings { Opacity = Double.NaN }), "invalid version and nonfinite settings rejected");
        }
        static async Task ManagerChecks(WidgetApp app, string evidence)
        {
            app.ShowManager(); var manager = app.Manager;
            Check(manager != null && manager.IsVisible, "manager opened on demand"); app.ShowManager(); Check(Object.ReferenceEquals(manager, app.Manager), "manager reuses existing page");
            Click(manager.VisibilityButtons[3]); Check(!app.Note.IsVisible && (string)manager.VisibilityButtons[3].Content == "显示", "manager hides widget and synchronizes label");
            app.Note.SetVisible(true); Check((string)manager.VisibilityButtons[3].Content == "隐藏", "widget change synchronizes manager");
            manager.PinBoxes[3].IsChecked = true; Check(app.Note.Topmost, "manager pin action"); manager.PinBoxes[3].IsChecked = false;
            Click(manager.HideAllButton); Check(app.Widgets.All(w => !w.IsVisible), "manager hide all"); Click(manager.ShowAllButton); Check(app.Widgets.All(w => w.IsVisible), "manager show all");
            manager.ThemeOptions[2].IsChecked = true; Check(app.Settings.Theme == "dark", "manager dark theme");
            manager.OpacitySlider.Value = 70; Check(Math.Abs(app.Settings.Opacity - .7) < .001, "manager opacity slider");
            manager.AutoStart.IsChecked = false; Check(!app.Settings.AutoStart, "manager autostart toggle state"); manager.AutoStart.IsChecked = true;
            Ui.VerificationConfirmation=true; Click(manager.ArrangeButton); Ui.VerificationConfirmation=null; Check(app.Widgets.All(w => w.Left >= SystemParameters.WorkArea.Left && w.Top >= SystemParameters.WorkArea.Top), "manager arranges without losing data");
            await Task.Delay(600); CaptureWindow(manager, Path.Combine(evidence, "manager-dark.png"));
            manager.ThemeOptions[1].IsChecked = true; await Task.Delay(100); CaptureWindow(manager, Path.Combine(evidence, "manager-light.png"));
            using (var p = Process.GetCurrentProcess()) report.Add("Manager open working set MB: " + (p.WorkingSet64 / 1048576.0).ToString("F2"));
            manager.Close(); Check(app.Manager == null && app.Widgets.All(w => w.IsVisible), "manager close releases reference and leaves widgets running");
            app.ShowManager(); Check(!Object.ReferenceEquals(app.Manager, manager), "manager recreates after closing"); app.Manager.Close();
            var menu = app.CreateTrayMenu(); Check(menu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "管理组件…") && menu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "退出"), "tray management and exit actions present");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "System.Windows.Forms" || a.GetName().Name == "System.Drawing"), "second UI framework not loaded");
            app.SetTheme("system"); app.SetOpacity(.85);
        }
        static void CountdownChecks()
        {
            DateTime now = DateTime.UtcNow; var timer = new Countdown(300); timer.Start(now); timer.Tick(now.AddSeconds(30)); timer.Pause(now.AddSeconds(30));
            Check(timer.Remaining == 270 && !timer.Running, "deadline pause preserves remaining"); timer.Start(now.AddHours(1));
            Check(timer.Tick(now.AddHours(2)) && !timer.Tick(now.AddHours(2).AddSeconds(1)), "sleep/wake deadline catches up exactly once");
        }
        static async Task V13Checks(WidgetApp app,string evidence)
        {
            var old2=new Settings { Version=2,Windows=new List<WindowState> { new WindowState { Id="note",Left=123,Top=234,Width=321,Height=345,Visible=false,Pinned=true } } }; old2.Migrate();
            Check(old2.Version==4 && old2.Windows[0].Mode==1 && old2.Windows[0].Left==123 && old2.Windows[0].Width==321 && old2.Windows[0].Pinned && !old2.Windows[0].Visible,"v2 migration retains layout visibility pin and defaults mode one");
            var w=app.Note; double width=w.Width, height=w.Height;
            w.Left=150; w.Top=160; w.FinishPlacement(); var stored=Storage.Read<Settings>(Path.Combine(app.Storage.DirectoryPath,"settings.json"),Settings.Valid).Windows.Find(v=>v.Id=="note"); Check(stored.Left==w.Left && stored.Top==w.Top && stored.Width==width,"final placement saved immediately without dispatcher delay");
            w.SetMode(2); w.ResizeFromEdge("rightbottom",50,50); Check(w.Width==width && w.Height==height && w.State.Mode==2,"mode two blocks resize"); w.SetPinned(true); Check(w.Topmost,"mode two retains pin action");
            var snap=Layout.Snap(new Rect(109,103,80,100),new[] { new Rect(200,100,80,100) },false); Check(snap.X==109 && snap.Y==103,"snap threshold rejects beyond ten DIP");
            snap=Layout.Snap(new Rect(115,103,80,100),new[] { new Rect(200,100,80,100) },false); Check(snap.X==120 && snap.Y==100,"snap zero gap with edge alignment");
            Check(Layout.Snap(new Rect(115,103,80,100),new[] { new Rect(200,100,80,100) },true).X==115,"Alt bypass preserves placement");
            var blockers=new[] { new Rect(200,100,80,100),new Rect(197,102,2,10) }; snap=Layout.Snap(new Rect(115,103,80,100),blockers,false); Check(blockers.All(r=> !(snap.X<r.Right && snap.X+80>r.Left && snap.Y<r.Bottom && snap.Y+100>r.Top)),"snap rejects collision with third widget");
            var positions=app.Widgets.Select(v=>new Point(v.Left,v.Top)).ToArray(); app.ShowManager(); Ui.VerificationConfirmation=false; Click(app.Manager.ArrangeButton); Check(app.Widgets.Select((v,i)=>v.Left==positions[i].X && v.Top==positions[i].Y).All(v=>v),"arrange cancel preserves every position");
            Ui.VerificationConfirmation=true; double lockedX=w.Left,lockedY=w.Top; var hidden=app.Clock; hidden.SetVisible(false); double hiddenX=hidden.Left,hiddenY=hidden.Top; Click(app.Manager.ArrangeButton); Ui.VerificationConfirmation=null; Check(w.Left==lockedX && w.Top==lockedY && hidden.Left==hiddenX && hidden.Top==hiddenY,"arrange excludes locked and hidden widgets"); hidden.SetVisible(true);
            Check(!w.Rename(" ") && !w.Rename(new string('x',33)) && w.Rename("  临时便签  ") && w.DisplayName=="临时便签" && w.Title.Contains("临时便签") && app.CreateTrayMenu().Items.OfType<MenuItem>().Any(m=>(string)m.Header=="临时便签"),"rename validation trim title and tray sync"); w.Rename(null); Check(w.DisplayName=="便签","restore default widget name");
            app.Manager.Close();
            string folder=Path.Combine(app.Storage.DirectoryPath,"folder"); Directory.CreateDirectory(folder); var shortcut=new ShortcutItem { Name="图标验收",Path=folder }; app.Data.Shortcuts.Add(shortcut);
            string png=Path.Combine(app.Storage.DirectoryPath,"source.png"); using(var stream=File.Create(png)) { var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Icons.Brand)); encoder.Save(stream); }
            app.Shortcuts.SetIcon(shortcut,png); Check(shortcut.CustomIcon.StartsWith("icons/") && File.Exists(Path.Combine(app.Storage.DirectoryPath,shortcut.CustomIcon)),"custom folder icon validated and copied with relative reference");
            string customBefore=shortcut.CustomIcon; string broken=Path.Combine(app.Storage.DirectoryPath,"broken.png"); File.WriteAllText(broken,"broken"); bool rejected=false; try { app.Shortcuts.SetIcon(shortcut,broken); } catch { rejected=true; } Check(rejected && shortcut.CustomIcon==customBefore,"invalid icon import preserves existing reference");
            string jpg=Path.Combine(app.Storage.DirectoryPath,"test.jpg"); using(var stream=File.Create(jpg)) { var encoder=new JpegBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Icons.Brand)); encoder.Save(stream); } Check(Icons.Import(jpg,app.Storage.DirectoryPath).EndsWith(".jpg"),"JPEG custom icon import"); Check(Icons.Import(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","assets","app.ico"),app.Storage.DirectoryPath).EndsWith(".ico"),"ICO custom icon import");
            string moved=Path.Combine(app.Storage.DirectoryPath,"moved"); Directory.CreateDirectory(Path.Combine(moved,"icons")); File.Copy(Path.Combine(app.Storage.DirectoryPath,shortcut.CustomIcon),Path.Combine(moved,shortcut.CustomIcon)); Icons.ClearTargets(); Check(!Object.ReferenceEquals(Icons.Shortcut(shortcut,moved),Icons.Folder),"icon remains readable after whole data relocation");
            File.WriteAllText(Path.Combine(moved,shortcut.CustomIcon),"broken"); Icons.ClearTargets(); Check(Object.ReferenceEquals(Icons.Shortcut(shortcut,moved),Icons.Folder),"corrupt custom icon falls back to built-in folder"); shortcut.CustomIcon="../outside.png"; Check(Object.ReferenceEquals(Icons.Shortcut(shortcut,moved),Icons.Folder),"custom icon rejects paths outside icons directory"); app.Data.Shortcuts.Remove(shortcut); app.SaveData();
            var duration=new DurationDialog(app.Countdown,1500); Check(duration.Paste("99:59:59") && duration.Validate() && duration.Result==359999,"duration paste maximum validates exact seconds");
            Check(duration.Paste("00:00:01") && duration.Validate() && duration.Result==1,"duration minimum one second"); duration.Paste("00:00:00"); Check(!duration.Validate() && duration.ErrorText.Text.Length>0,"zero duration inline error");
            Check(!duration.Paste("00:60:00") && !duration.Paste("xx:00:00") && !duration.Paste("1:02:03"),"invalid full time paste rejected"); duration.Hours.Input.Text="99"; duration.Hours.Step(1); duration.Minutes.Input.Text="00"; duration.Minutes.Step(-1); Check(duration.Hours.Input.Text=="99" && duration.Minutes.Input.Text=="00","column boundary clamps without carry");
            duration.Paste("01:02:03"); duration.Seconds.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,120) { RoutedEvent=UIElement.PreviewMouseWheelEvent }); Check(duration.Hours.Input.Text=="01" && duration.Minutes.Input.Text=="02" && duration.Seconds.Input.Text=="04","wheel changes hovered column only");
            duration.Seconds.Drag.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,-16)); Check(duration.Seconds.Input.Text=="06","duration drag two steps"); duration.Show(); await Task.Delay(80); CaptureWindow(duration,Path.Combine(evidence,"duration-picker.png"));
            duration.Hours.Input.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(duration),Environment.TickCount,System.Windows.Input.Key.Up) { RoutedEvent=UIElement.PreviewKeyDownEvent }); Check(duration.Hours.Input.Text=="02","duration arrow key changes focused column");
            app.Settings.Theme="dark"; app.Theme.Refresh(); Check(Native.CaptionMode(duration)==1,"native duration titlebar follows application dark mode"); CaptureWindow(duration,Path.Combine(evidence,"duration-picker-dark.png")); app.Settings.Theme="light"; app.Theme.Refresh(); Check(Native.CaptionMode(duration)==0,"native titlebar restores application light mode"); duration.Close();
            int previous=app.Countdown.Timer.Duration; var cancel=new DurationDialog(app.Countdown,previous); var cancelOperation=app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate { cancel.Paste("00:01:23"); Click(cancel.CancelButton); })); bool cancelled=cancel.ShowDialog()!=true && cancelOperation.Status==DispatcherOperationStatus.Completed; Check(cancelled && app.Countdown.Timer.Duration==previous,"duration cancel leaves countdown unchanged"); var confirm=new DurationDialog(app.Countdown,previous); var confirmOperation=app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate { confirm.Paste("00:00:01"); Click(confirm.ConfirmButton); })); bool accepted=confirm.ShowDialog()==true && confirmOperation.Status==DispatcherOperationStatus.Completed && app.Countdown.SetDuration(confirm.Result); Check(accepted && app.Data.CountdownSeconds==1,"one-second duration actual dialog confirmation persisted"); Click(app.Countdown.StartButton); Check(!app.Countdown.SetDuration(2) && app.Countdown.Timer.Duration==1,"running duration cannot change"); Click(app.Countdown.PauseButton); Check(app.Countdown.SetDuration(83) && !app.Countdown.Timer.Running && app.Countdown.Timer.Remaining==83,"paused edit resets to unstarted exact seconds");
            int manager=0,note=0; using(var clicks=new TrayClicks(delegate {manager++;},delegate {note++;})) { clicks.Notify(0x400); clicks.Notify(0x203); clicks.Notify(0x400); await Task.Delay(clicks.Delay+80); Check(manager==0 && note==1,"tray double cancels pending single and trailing click"); clicks.Notify(0x400); await Task.Delay(clicks.Delay+80); Check(manager==1 && note==1,"tray single deferred through system double-click interval"); clicks.Notify(0x401); Check(manager==2,"tray keyboard activation immediate manager"); }
            w.SetVisible(false); string content=app.Data.Note; app.BringNoteForward(); Check(w.IsVisible && w.Topmost && w.TemporaryFront && app.Data.Note==content,"tray recall existing hidden note without content mutation"); w.RestoreFront(); Check(w.Topmost==w.State.Pinned && w.State.Visible,"temporary focus restores pin and keeps visibility");
            bool attached=w.SetMode(3); report.Add("Desktop host HWND="+w.Desktop.Parent+"; child="+w.Desktop.Handle);
            Check(attached && w.Desktop.Attached && DesktopHost.GetParent(w.Desktop.Handle)==w.Desktop.Parent && w.IsVisible && !w.Topmost && w.State.Pinned,"real Explorer child HWND with preserved pin preference");
            if(attached)
            {
                Check(w.Desktop.TransparentSurface,"desktop native target actually honors per-pixel transparency");
                Check(w.Desktop.AboveIcons,"desktop child is above Explorer icon input surface");
                app.ShowManager(); await Task.Delay(100);
                object shell=null; bool toggled=false;
                try
                {
                    shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); shell.GetType().InvokeMember("ToggleDesktop",System.Reflection.BindingFlags.InvokeMethod,null,shell,null); toggled=true; await Task.Delay(700);
                    report.Add("Show Desktop detail: attached="+w.Desktop.Attached+"; child visible="+w.IsVisible+"; manager state="+app.Manager.WindowState+"; manager visible="+app.Manager.IsVisible+"; foreground="+DesktopHost.ForegroundClass()+"; "+w.Desktop.Snapshot());
                    if(DesktopHost.DesktopForeground()) { Check(w.Desktop.Attached && w.IsVisible,"real Show Desktop activates desktop while Explorer child remains visible"); Check(w.Desktop.TransparentSurface && w.Desktop.AboveIcons,"Show Desktop preserves real alpha target and input sibling order"); }
                    else { report.Add("NOT VERIFIED real Show Desktop: Shell did not enter desktop; foreground="+DesktopHost.ForegroundClass()+"; manager="+app.Manager.WindowState); Check(w.Desktop.Attached && w.IsVisible,"desktop child remains attached and visible after Shell request"); }
                }
                finally { if(toggled && shell!=null) shell.GetType().InvokeMember("ToggleDesktop",System.Reflection.BindingFlags.InvokeMethod,null,shell,null); if(shell!=null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
                await Task.Delay(300);
                app.Manager.Close();
                app.BringNoteForward(); Check(!w.Desktop.Attached && w.TemporaryFront && w.Topmost && w.State.Mode==3,"desktop note detaches for temporary editing"); w.RestoreFront(); Check(w.Desktop.Attached && w.IsVisible && w.State.Mode==3,"desktop note reattaches at focus restore");
                w.SetVisible(false); Check(!w.IsVisible,"desktop child hides"); w.SetVisible(true); Check(w.IsVisible,"desktop child restores"); w.Desktop.Dispose(); app.RestoreDesktops(); Check(w.Desktop.Attached,"desktop host reattachment handler recreates child");
            }
            w.SetMode(1); Check(!w.Desktop.Attached && w.Topmost && w.State.Mode==1,"leaving desktop restores top-level pin"); w.SetPinned(false); app.Countdown.SetDuration(1500);
            w.SetVisible(false); DesktopHost.FailForVerification=true; bool recovered; try { recovered=!w.SetMode(3); } finally { DesktopHost.FailForVerification=false; } Check(recovered && w.State.Mode==2 && w.IsVisible && app.LastError.Contains("第 2 档"),"desktop attachment failure recovers visible locked window"); w.SetMode(1);
            app.Settings.Theme="dark"; app.Theme.Refresh(); var menu=app.CreateTrayMenu(); menu.IsOpen=true; await Task.Delay(100); CaptureElement(menu,Path.Combine(evidence,"tray-dark.png")); var submenu=menu.Items.OfType<MenuItem>().First(m=>(string)m.Header=="主题"); submenu.IsSubmenuOpen=true; await Task.Delay(100); if(submenu.Items.Count>0) CaptureElement((FrameworkElement)((System.Windows.Controls.Primitives.Popup)submenu.Template.FindName("PART_Popup",submenu)).Child,Path.Combine(evidence,"tray-dark-submenu.png")); menu.IsOpen=false;
            Check(menu.Style!=null && submenu.Style!=null,"tray and submenu use complete application theme templates"); app.Settings.Theme="system"; app.Theme.Refresh();
            await V131Checks(app,evidence); await V144Checks(app,evidence); await V143Checks(app,evidence); await V142Checks(app,evidence); await V14Checks(app,evidence); await LanguageChecks(app,evidence);
        }
        static async Task V144Checks(WidgetApp app,string evidence)
        {
            var w=app.Note; string theme=app.Settings.Theme; double opacity=app.Settings.Opacity;
            w.SetMode(1); w.SetVisible(true); app.SetTheme("light"); app.SetOpacity(.85); w.SetMode(3); await Task.Delay(80);
            var root=(Grid)((Grid)w.Desktop.Surface).Children[0]; var card=(Border)root.Children[0]; var light=((SolidColorBrush)card.Background).Color;
            app.SetTheme("dark"); await Task.Delay(80);
            Color expected=((SolidColorBrush)app.Resources["Card"]).Color,actual=((SolidColorBrush)card.Background).Color;
            report.Add("Live desktop theme: initial="+light+"; actualDark="+actual+"; expectedDark="+expected);
            Check(actual==expected && actual!=light,"theme switches light to dark after desktop is already attached");
            Check(((SolidColorBrush)w.Editor.Foreground).Color==((SolidColorBrush)app.Resources["Ink"]).Color && ((SolidColorBrush)w.Editor.Background).Color==((SolidColorBrush)app.Resources["Field"]).Color,"already-attached editor foreground and field follow dark theme");
            Check(((SolidColorBrush)card.BorderBrush).Color==((SolidColorBrush)app.Resources["CardLine"]).Color,"already-attached outline follows dark theme");
            SaveBitmap(RenderElement(root),Path.Combine(evidence,"live-desktop-dark.png"));
            app.SetOpacity(.35); await Task.Delay(60); Check(((SolidColorBrush)card.Background).Color.A==89 && ((SolidColorBrush)card.BorderBrush).Color.A==89,"opacity slider updates already-attached desktop card and outline");
            SaveBitmap(RenderElement(root),Path.Combine(evidence,"live-desktop-dark35.png"));
            app.SetTheme("light"); await Task.Delay(60); Check(((SolidColorBrush)card.Background).Color==((SolidColorBrush)app.Resources["Card"]).Color && ((SolidColorBrush)w.Editor.Foreground).Color==((SolidColorBrush)app.Resources["Ink"]).Color,"already-attached desktop returns to light theme");
            app.Theme.ApplyPalette(Colors.Blue,Colors.LightBlue,true,true,true); await Task.Delay(60); Check(((SolidColorBrush)card.Background).Color==SystemColors.WindowColor && ((SolidColorBrush)w.Editor.Foreground).Color==SystemColors.WindowTextColor,"already-attached desktop reacts to high-contrast palette refresh");
            w.SetMode(1); app.SetOpacity(opacity); app.SetTheme(theme);
        }
        static async Task V143Checks(WidgetApp app,string evidence)
        {
            var w=app.Note; double opacity=app.Settings.Opacity; string theme=app.Settings.Theme; string note=app.Data.Note;
            foreach(bool dark in new[] {false,true}) foreach(double alpha in new[] {.35,.85,1.0})
            {
                app.SetTheme(dark?"dark":"light"); app.SetOpacity(alpha); w.SetMode(1); w.SetVisible(true); await Task.Delay(50);
                var content=(FrameworkElement)w.Content; var before=RenderElement(content); var beforePixel=Pixel(before,8,80);
                Check(w.SetMode(3) && w.Desktop.TransparentSurface,"fixed desktop real alpha surface "+dark+"/"+alpha); await Task.Delay(60);
                var after=RenderElement(content); var afterPixel=Pixel(after,8,80);
                Check(before.PixelWidth==after.PixelWidth && before.PixelHeight==after.PixelHeight && beforePixel.SequenceEqual(afterPixel),"desktop card color and alpha match normal mode "+dark+"/"+alpha);
                Check(w.Desktop.AboveIcons && w.Desktop.Surface.IsVisible && content.IsVisible && content.IsEnabled,"desktop surface visible enabled and above icon hit plane "+dark+"/"+alpha);
                var point=w.ModeButton.TranslatePoint(new Point(w.ModeButton.ActualWidth/2,w.ModeButton.ActualHeight/2),w.Desktop.Surface);
                DependencyObject hit=w.Desktop.Surface.InputHitTest(point) as DependencyObject; while(hit!=null && hit!=w.ModeButton) hit=VisualTreeHelper.GetParent(hit);
                Check(hit==w.ModeButton,"desktop layout hit-test reaches actual mode button "+dark+"/"+alpha);
                Check(w.Desktop.FocusContent() && w.Editor.Focus() && System.Windows.Input.Keyboard.FocusedElement==w.Editor,"desktop native HWND and WPF editor accept focus "+dark+"/"+alpha);
                if(alpha==.35) {
                    var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) { dc.DrawRectangle(new SolidColorBrush(dark?Color.FromRgb(20,42,60):Color.FromRgb(38,136,217)),null,new Rect(0,0,before.PixelWidth*2+48,before.PixelHeight+32)); dc.DrawImage(before,new Rect(16,16,before.PixelWidth,before.PixelHeight)); dc.DrawImage(after,new Rect(before.PixelWidth+32,16,after.PixelWidth,after.PixelHeight)); }
                    var comparison=new RenderTargetBitmap(before.PixelWidth*2+48,before.PixelHeight+32,96,96,PixelFormats.Pbgra32); comparison.Render(visual); SaveBitmap(comparison,Path.Combine(evidence,"normal-vs-desktop35-"+(dark?"dark":"light")+".png"));
                }
                w.SetMode(1); await Task.Delay(80); w.UpdateLayout();
                report.Add("Detach detail "+dark+"/"+alpha+": sameContent="+ReferenceEquals(w.Content,content)+"; surfaceReleased="+(w.Desktop.Surface==null)+"; windowVisible="+w.IsVisible+"; editorVisible="+w.Editor.IsVisible);
                Check(ReferenceEquals(w.Content,content) && w.Desktop.Surface==null && w.Editor.IsVisible,"desktop detach restores visible content and releases surface after layout "+dark+"/"+alpha);
            }
            Check(app.Data.Note==note,"desktop focus and render checks preserve note content");
            app.SetOpacity(opacity); app.SetTheme(theme);
            report.Add("LIMIT v1.4.3: native alpha, sibling Z order, WPF hit testing and HWND/editor focus verified; physical mouse click/type and desktop screenshot remain interactive acceptance, not replaced by these checks.");
        }
        static RenderTargetBitmap RenderElement(FrameworkElement element,double scale=1)
        { element.UpdateLayout(); var image=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(element.ActualWidth*scale)),Math.Max(1,(int)Math.Ceiling(element.ActualHeight*scale)),96*scale,96*scale,PixelFormats.Pbgra32); image.Render(element); return image; }
        static byte[] Pixel(BitmapSource bitmap,int x,int y)
        { var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0); byte[] value=new byte[4]; converted.CopyPixels(new Int32Rect(x,y,1,1),value,4,0); return value; }
        static async Task V142Checks(WidgetApp app,string evidence)
        {
            var empty=new Rect[0]; var work=new Rect(0,0,1000,760); var start=new Rect(300,300,200,120);
            Func<Rect,Point> snap=raw=>new SnapGesture(start,empty,work).Apply(raw,empty,false,work);
            Check(snap(new Rect(5,100,200,120))==new Point(0,100),"desktop left edge capture within six DIP");
            Check(snap(new Rect(795,100,200,120))==new Point(800,100),"desktop right edge capture");
            Check(snap(new Rect(100,5,200,120))==new Point(100,0),"desktop top edge capture");
            Check(snap(new Rect(100,635,200,120))==new Point(100,640),"desktop bottom edge capture");
            Check(snap(new Rect(5,5,200,120))==new Point(0,0) && snap(new Rect(795,635,200,120))==new Point(800,640),"desktop opposite corners capture both axes");
            Check(snap(new Rect(-5,100,200,120))==new Point(0,100),"desktop capture from slightly outside edge");
            Check(snap(new Rect(7,100,200,120))==new Point(7,100),"desktop does not attract from seven DIP");
            var session=new SnapGesture(new Rect(0,100,200,120),empty,work);
            Check(session.Apply(new Rect(1,100,200,120),empty,false,work).X==1,"desktop attached card detaches after one DIP");
            Check(session.Apply(new Rect(9,100,200,120),empty,false,work).X==9 && session.Apply(new Rect(5,100,200,120),empty,false,work).X==0,"desktop edge rearms after eight-DIP release zone");
            session=new SnapGesture(new Rect(0,0,200,120),empty,work); Check(session.Apply(new Rect(1,1,200,120),empty,false,work)==new Point(1,1),"desktop corner initially releases both axes");
            session=new SnapGesture(start,empty,work); Check(session.Apply(new Rect(5,5,200,120),empty,true,work)==new Point(5,5),"Alt bypasses desktop corner attraction");
            var leftMonitor=new Rect(-1280,-720,1280,680); session=new SnapGesture(new Rect(-1000,-500,200,120),empty,leftMonitor);
            Check(session.Apply(new Rect(-1275,-715,200,120),empty,false,leftMonitor)==new Point(-1280,-720),"desktop negative monitor coordinates preserve corner");
            session=new SnapGesture(new Rect(0,100,200,120),empty,work); var nextMonitor=new Rect(1000,0,1000,760);
            Check(session.Apply(new Rect(1005,100,200,120),empty,false,nextMonitor).X==1000,"monitor transition discards old edge exemptions");
            var taskbarWork=new Rect(0,0,1920,1040); session=new SnapGesture(start,empty,taskbarWork);
            Check(session.Apply(new Rect(100,915,200,120),empty,false,taskbarWork).Y==920,"desktop snaps above reserved taskbar work area");
            var scaledWork=new Rect(-1920/1.5,0,1920/1.5,1040/1.5); session=new SnapGesture(new Rect(-900,200,200,120),empty,scaledWork);
            Check(session.Apply(new Rect(scaledWork.Left+5,100,200,120),empty,false,scaledWork).X==scaledWork.Left,"desktop six-DIP capture after 150-percent coordinate normalization");
            var peer=new[] {new Rect(200,100,200,120)}; session=new SnapGesture(new Rect(100,400,195,120),peer,work);
            Check(session.Apply(new Rect(5,105,195,120),peer,false,work)==new Point(0,100),"desktop and component alignment combine without overlap");
            var blocker=new[] {new Rect(0,100,3,120)}; session=new SnapGesture(start,blocker,work);
            Check(session.Apply(new Rect(5,100,200,120),blocker,false,work).X>=3,"desktop attraction refuses to overlap another component");
            var cornerBlocker=new[] {new Rect(0,0,3,3)}; session=new SnapGesture(start,cornerBlocker,work); var corner=session.Apply(new Rect(5,5,200,120),cornerBlocker,false,work);
            Check((corner.X>=3 || corner.Y>=3) && (corner.X==0 || corner.Y==0),"occupied desktop corner retains valid single-axis snap");
            session=new SnapGesture(start,empty,null); Check(session.Apply(new Rect(5,100,200,120),empty,false,null).X==5,"missing monitor work area leaves raw motion usable");
            var huge=new Rect(3,3,1100,800); Check(Layout.Snap(huge,empty,false,null,work,0)==huge.TopLeft,"oversized card does not snap outside smaller work area");
            Native.Rect bounds; Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(app.Note).Handle,out bounds); var nativeWork=Native.WorkingArea(bounds); var windowWork=Native.WorkingArea(app.Note);
            Check(nativeWork.Left==windowWork.Left && nativeWork.Top==windowWork.Top && nativeWork.Right==windowWork.Right && nativeWork.Bottom==windowWork.Bottom,"real proposed-rectangle monitor work area matches window monitor");
            var border=(Border)((Grid)app.Note.Content).Children[0]; double previousOpacity=app.Settings.Opacity;
            foreach(bool dark in new[] {false,true}) {
                app.Settings.Opacity=.35; app.Theme.ApplyPalette(Colors.DodgerBlue,Theme.Blend(Colors.DodgerBlue,dark?Colors.Black:Colors.White,.55),dark,false,true);
                border.SetResourceReference(Border.BorderBrushProperty,"Line"); await Task.Delay(40); app.Note.UpdateLayout(); var before=RenderElement(app.Note);
                border.SetResourceReference(Border.BorderBrushProperty,"CardLine"); await Task.Delay(40); var after=RenderElement(app.Note);
                var stroke=((SolidColorBrush)border.BorderBrush).Color; var card=((SolidColorBrush)app.Resources["Card"]).Color;
                Check(stroke.A==card.A && stroke.A==89 && ((SolidColorBrush)app.Resources["Line"]).Color.A==255 && ((SolidColorBrush)app.Resources["Ink"]).Color.A==255,"35-percent outline follows background while controls and ink stay opaque "+dark);
                var board=new DrawingVisual(); using(var dc=board.RenderOpen()) { dc.DrawRectangle(new SolidColorBrush(dark?Color.FromRgb(20,42,60):Color.FromRgb(38,136,217)),null,new Rect(0,0,before.PixelWidth*2+48,before.PixelHeight+32)); dc.DrawImage(before,new Rect(16,16,before.PixelWidth,before.PixelHeight)); dc.DrawImage(after,new Rect(before.PixelWidth+32,16,after.PixelWidth,after.PixelHeight)); }
                var bitmap=new RenderTargetBitmap(before.PixelWidth*2+48,before.PixelHeight+32,96,96,PixelFormats.Pbgra32); bitmap.Render(board); SaveBitmap(bitmap,Path.Combine(evidence,"opacity35-before-after-"+(dark?"dark":"light")+".png"));
                foreach(double opacity in new[] {.55,.85,1.0}) { app.Settings.Opacity=opacity; app.Theme.ApplyPalette(Colors.DodgerBlue,Colors.LightBlue,dark,false,true); Check(((SolidColorBrush)border.BorderBrush).Color.A==((SolidColorBrush)app.Resources["Card"]).Color.A,"outline tracks opacity "+opacity+"/"+dark); }
            }
            app.Settings.Opacity=.35; app.Theme.ApplyPalette(Colors.Blue,Colors.LightBlue,false,false,false); Check(((SolidColorBrush)border.BorderBrush).Color.A==255,"outline stays solid when Windows transparency is disabled");
            app.Theme.ApplyPalette(Colors.Blue,Colors.LightBlue,true,true,true); Check(((SolidColorBrush)border.BorderBrush).Color==SystemColors.WindowTextColor,"high contrast outline preserves system window text color");
            app.Settings.Opacity=previousOpacity; app.Theme.Refresh();
        }
        static void SaveBitmap(BitmapSource bitmap,string file)
        { var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(file)) encoder.Save(stream); }
        static async Task V14Checks(WidgetApp app,string evidence)
        {
            var target=new Rect(200,100,80,100); var targets=new[] {target}; var session=new SnapGesture(new Rect(120,100,80,100),targets);
            Check(session.Apply(new Rect(119,100,80,100),targets,false).X==119,"already attached card detaches after one DIP");
            Check(session.Apply(new Rect(111,100,80,100),targets,false).X==111 && session.Apply(new Rect(115,100,80,100),targets,false).X==120,"magnet rearms after leaving zone and approaching again");
            session=new SnapGesture(new Rect(90,100,80,100),targets); Check(session.Apply(new Rect(115,100,80,100),targets,false).X==120 && session.Apply(new Rect(127,100,80,100),targets,false).X==127,"raw pointer movement escapes magnet without accumulated feedback");
            Check(Layout.Snap(new Rect(113,100,80,100),targets,false).X==113,"weaker six-DIP capture does not attract from seven DIP");
            session=new SnapGesture(new Rect(90,100,80,100),targets); Check(session.Apply(new Rect(115,100,80,100),targets,true).X==115,"Alt bypass in drag session");
            var legacy=new Settings { Version=3,Windows=new List<WindowState> { new WindowState { Id="shortcuts",Left=99,Top=88,Width=280,Height=220,DisplayName="原有入口",Mode=2,Pinned=true } } }; legacy.Migrate(); Check(legacy.Version==4 && legacy.Windows[0].Mode==2 && legacy.Windows[0].DisplayName=="原有入口" && legacy.Windows[0].Left==99 && legacy.Windows[0].Pinned,"version three migration preserves names locks layout and pin");
            app.ShowManager(); var manager=app.Manager; Check(manager.RenameButtons.Length==app.Widgets.Count && manager.RenameButtons.All(b=>(string)b.Content=="改名"),"direct rename actions for every widget");
            string originalNote=app.Data.Note; var renameOperation=app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate { var dialog=app.Windows.OfType<Window>().Single(w=>w.Title=="组件名称（1–32 字）" && w.IsVisible); var body=(StackPanel)dialog.Content; body.Children.OfType<TextBox>().Single().Text="工作便签"; Click(((StackPanel)body.Children[body.Children.Count-1]).Children.OfType<Button>().First()); }));
            Click(manager.RenameButtons[3]); Check(renameOperation.Status==DispatcherOperationStatus.Completed && app.Note.DisplayName=="工作便签" && app.Data.Note==originalNote && System.Windows.Automation.AutomationProperties.GetName(manager.ModeButtons[3]).Contains("工作便签"),"actual manager rename dialog saves title and accessibility without note mutation"); app.Note.Rename(null);
            Check(app.Widgets.All(w=>w.ModeButton.Content is System.Windows.Shapes.Path) && manager.ModeButtons.All(b=>b.Content is System.Windows.Shapes.Path),"placement icons replace numbers in widgets and manager");
            string file=Path.Combine(app.Storage.DirectoryPath,"group-file.txt"); File.WriteAllText(file,"group test"); string folder=Path.Combine(app.Storage.DirectoryPath,"group-folder"); Directory.CreateDirectory(folder);
            int original=app.Shortcuts.Items.Count; var group=app.AddShortcutGroup("工作入口"); var second=app.AddShortcutGroup("生活入口"); Check(group!=null && second!=null && group.State.Id!=second.State.Id && manager.VisibilityButtons.Length==8,"multiple independent shortcut widgets and live manager rows");
            group.Add("文件",file,"file"); group.Add("软件",System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,"software"); second.Add("文件夹",folder,"folder");
            Check(group.Items.Count==2 && second.Items.Count==1 && app.Shortcuts.Items.Count==original && group.Items.All(s=>s.GroupId==group.State.Id),"shortcut group isolation preserves original entries");
            group.SetFilter("file"); Check(group.DisplayedCount==1 && second.DisplayedCount==1,"per-widget file filter excludes software and other groups"); group.SetFilter("software"); Check(group.DisplayedCount==1,"software filter"); group.SetFilter("folder"); Check(group.DisplayedCount==0,"empty folder filter does not mix other groups"); group.SetFilter("all"); Check(group.DisplayedCount==2,"all filter restores complete group");
            var before=group.Items[1]; group.Move(before,-1); Check(group.Items[0]==before && second.Items[0].Name=="文件夹","reorder only affects selected group"); group.Remove(second.Items[0]); Check(second.Items.Count==1,"cross-group removal refused");
            manager.SelectShortcutGroup(second); manager.ShortcutPanel.Types[2].IsChecked=true; Check(manager.ShortcutPanel.Selected==second && manager.ShortcutPanel.SelectedType=="folder","manager target group and add type selected independently from widget filter");
            group.Rename("项目资料"); group.SetMode(2); group.SetPinned(true); group.SetFilter("file"); app.SaveData(); app.SaveSettings(); var saved=Storage.Read<Settings>(Path.Combine(app.Storage.DirectoryPath,"settings.json"),Settings.Valid); var data=Storage.Read<BusinessData>(Path.Combine(app.Storage.DirectoryPath,"business.json"),BusinessData.Valid);
            Check(saved.Windows.Find(w=>w.Id==group.State.Id).DisplayName=="项目资料" && saved.Windows.Find(w=>w.Id==group.State.Id).ShortcutFilter=="file" && data.Shortcuts.Count(s=>s.GroupId==group.State.Id)==2,"group identity name filter and entries roundtrip persisted");
            bool desktop=group.SetMode(3); Check(desktop && group.Desktop.Attached && !group.Topmost && group.State.Pinned,"additional shortcut instance supports actual desktop host and pin preference"); group.Desktop.Dispose(); app.RestoreDesktops(); Check(group.Desktop.Attached,"additional shortcut desktop reattachment"); group.SetMode(2);
            app.SetTheme("light"); await Task.Delay(80); CaptureWindow(group,Path.Combine(evidence,"shortcut-group-filter-light.png")); CaptureWindow(manager,Path.Combine(evidence,"manager-groups-light.png")); app.SetTheme("dark"); CaptureWindow(manager,Path.Combine(evidence,"manager-groups-dark.png"));
            Check(Settings.Valid(saved) && BusinessData.Valid(data) && !WidgetApp.IsShortcutInstance("shortcuts-invalid") && !Settings.Valid(new Settings { Windows=new List<WindowState> { new WindowState { Id="shortcuts-invalid",Width=280 } } }),"dynamic IDs validated and business version one retained");
            manager.Close(); app.SetTheme("system");
        }
        static async Task V131Checks(WidgetApp app,string evidence)
        {
            Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.ProcessorPower))==24,"processor power native ABI 24 bytes");
            double? ghz=app.Status.Sampler.FrequencyGHz(); Check(ghz.HasValue && ghz.Value>0 && ghz.Value<20,"native current CPU frequency readable"); report.Add("CPU frequency GHz="+ghz);
            app.Status.Sample(); app.Status.UpdateLayout(); var frequency=app.Status.CpuFrequency; var memory=app.Status.MemoryDetail;
            Check(frequency.Text.EndsWith(" GHz") && frequency.FontSize==memory.FontSize && frequency.Margin==memory.Margin,"CPU frequency uses memory-detail size and spacing");
            Check(Math.Abs(frequency.TransformToAncestor(app.Status).Transform(new Point()).Y-memory.TransformToAncestor(app.Status).Transform(new Point()).Y)<.5,"CPU frequency and memory detail align on same row");
            foreach(string theme in new[] {"light","dark"})
            {
                app.SetTheme(theme); app.Countdown.Activate(); app.Countdown.EditButton.Focus(); await Task.Delay(80); app.Countdown.EditButton.ApplyTemplate();
                Check(app.Countdown.EditButton.Style==app.Resources["TimeReadout"] && app.Countdown.EditButton.BorderThickness==new Thickness(0) && app.Countdown.EditButton.FocusVisualStyle==null,"countdown dedicated frameless focus style "+theme);
                CaptureWindow(app.Countdown,Path.Combine(evidence,"countdown-frameless-focus-"+theme+".png")); app.Status.Sample(); CaptureWindow(app.Status,Path.Combine(evidence,"status-frequency-"+theme+".png"));
            }
            app.Status.Width=240; app.Status.Height=app.Status.MinHeight; app.Status.UpdateLayout(); await Task.Delay(60); CaptureWindow(app.Status,Path.Combine(evidence,"status-frequency-minimum.png"));
            Check(Math.Abs(frequency.TransformToAncestor(app.Status).Transform(new Point()).Y-memory.TransformToAncestor(app.Status).Transform(new Point()).Y)<.5,"frequency-detail row remains aligned at minimum size"); app.SetTheme("system");
        }
        public static void CaptureElement(FrameworkElement element,string file)
        { SaveBitmap(RenderElement(element),file); }
        static async Task V12Checks(WidgetApp app, string evidence)
        {
            var old = new Settings { Version = 1, Theme = "dark", Opacity = .7, AutoStart = false, Windows = new List<WindowState> { new WindowState { Id = "clock", Left = 100, Top = 200, Height = 310, Visible = false, Pinned = true }, new WindowState { Id = "note", Left = 400, Top = 200, Height = 320 } } };
            Check(Settings.Valid(old), "v1 settings accepted before migration"); old.Migrate();
            Check(old.Version == 4 && old.Windows[0].Width == 280 && old.Windows[0].Left == 100 && old.Windows[0].Height == 310 && !old.Windows[0].Visible && old.Windows[0].Pinned && old.Theme == "dark" && !old.AutoStart, "migration preserves old layout and preferences");
            Check(Settings.Valid(old) && BusinessData.Valid(app.Data) && app.Data.Version == 1, "settings v4 and business v1 compatibility");
            var fakeWork = new Rect(0,0,1920,1040); Point place = Layout.FreeNear(fakeWork, old.Windows[0], old.Windows, 280, 330);
            Check(!new Rect(place.X,place.Y,280,330).IntersectsWith(new Rect(400,200,280,320)), "new countdown finds unoccupied location");
            Check(NetworkSampler.NativeRowSize == 1352, "GetIfTable2 x64 ABI row size");
            var actualCounters = NetworkSampler.ReadCounters(); Check(actualCounters.Count > 0, "native interface table readable"); report.Add("Live eligible physical interfaces: " + actualCounters.Count(c => c.Eligible));
            Check(NetworkSampler.Eligible(6,0,5,1,1) && NetworkSampler.Eligible(71,0,5,1,1) && !NetworkSampler.Eligible(24,0,5,1,1) && !NetworkSampler.Eligible(6,0,4,1,1) && !NetworkSampler.Eligible(131,1,5,1,1), "physical wired/wifi and virtual/tunnel filtering");
            var sampler = new NetworkSampler(); var first = new NetworkCounter { Id = 1, Received = 1000, Sent = 500, Eligible = true };
            Check(sampler.Update(new[] { first },10).State == "采样中", "network initial baseline");
            var next = new NetworkCounter { Id = 1, Received = 9000, Sent = 4500, Eligible = true }; var rates=sampler.Update(new[] { next,next,new NetworkCounter { Id=2,Received=100000,Sent=50000,Eligible=false } },12);
            Check(rates.Download == 4000 && rates.Upload == 2000 && rates.Interfaces == 1, "network actual interval and no duplicate counting");
            rates=sampler.Update(new[] { new NetworkCounter { Id=1,Received=10,Sent=10,Eligible=true } },14); Check(rates.Upload==0 && rates.Download==0, "network counter reset avoids false peak");
            rates=sampler.Update(new[] { next },50); Check(rates.State=="采样中" && rates.Download==0, "network wake gap rebaselines");
            Check(sampler.Update(new NetworkCounter[0],52).State=="断网" && sampler.Update(new[] { next },54).State=="采样中", "network disconnect and reconnect");
            Check(NetworkSampler.Format(1000)=="1.0 KB/s" && NetworkSampler.Format(1000000)=="1.0 MB/s" && NetworkSampler.Format(1000000000)=="1.0 GB/s", "network decimal display units");
            for(int i=0;i<40;i++) app.Status.UploadTrend.Add(i); Check(app.Status.UploadTrend.Count==30, "trend bounded to thirty points");
            var live=app.Status.LastNetwork; Check(live!=null && live.State!="暂不可用", "live network sample succeeds");
            int netSamples=app.Status.Network.Samples; app.Status.SetVisible(false); await Task.Delay(2200); Check(app.Status.Network.Samples==netSamples, "hidden network sampling stops"); app.Status.SetVisible(true); Check(app.Status.LastNetwork.State=="采样中" || app.Status.LastNetwork.State=="断网", "network restore rebaselines immediately");
            var businessBefore=app.Data.Note; var savedSizes=app.Widgets.Select(w=>new Size(w.Width,w.Height)).ToArray();
            foreach(var w in app.Widgets)
            {
                w.Width=240; w.Height=Layout.MinimumHeight(w.State.Id); w.UpdateLayout(); Check(w.MinWidth==240 && w.MinHeight==Layout.MinimumHeight(w.State.Id), "minimum size "+w.State.Id);
                CaptureWindow(w,Path.Combine(evidence,"minimum-"+w.State.Id+".png"));
                if(w.State.Id=="status") { var view=(ScrollViewer)w.Body.Children[0]; var content=(Grid)view.Content; Check(content.ActualWidth<=view.ViewportWidth+1,"status narrow content fits viewport width"); Check(content.ColumnDefinitions.Sum(c=>c.ActualWidth)<=view.ViewportWidth+1,"status child columns fit narrow viewport"); report.Add("Status narrow viewport="+view.ViewportWidth+"; columns="+content.ColumnDefinitions.Sum(c=>c.ActualWidth)); }
                double x=w.Left, width=w.Width; w.ResizeFromEdge("rightbottom",60,45); Check(w.Width==width+60 && w.Height==w.MinHeight+45 && w.State.Width==w.Width, "corner resize persists "+w.State.Id);
                double right=w.Left+w.Width; w.ResizeFromEdge("left",20,0); Check(Math.Abs(w.Left+w.Width-right)<.1, "left-edge anchor "+w.State.Id);
            }
            app.SaveSettings(); Check(Storage.Read<Settings>(Path.Combine(app.Storage.DirectoryPath,"settings.json"),Settings.Valid).Windows.All(w=>w.Width>=240), "six window widths saved to version four");
            for(int i=0;i<app.Widgets.Count;i++) { app.Widgets[i].Width=savedSizes[i].Width; app.Widgets[i].Height=savedSizes[i].Height; }
            Check(app.Data.Note==businessBefore, "resizing preserves business content");
            Check(app.Countdown.SetDuration(1500) && app.Data.CountdownSeconds==1500, "countdown clickable edit model persists duration");
            Click(app.Countdown.StartButton); Check(!app.Countdown.EditButton.IsEnabled, "countdown editing disabled while running"); Click(app.Countdown.PauseButton); Check(app.Countdown.EditButton.IsEnabled, "countdown editing enabled when paused"); Click(app.Countdown.ResetButton);
            app.Countdown.Timer.Start(DateTime.UtcNow.AddSeconds(-300)); app.Countdown.Timer.Tick(DateTime.UtcNow); app.Countdown.Timer.Pause(DateTime.UtcNow); app.Countdown.Refresh(); Check(app.Countdown.Ring.Fraction>0 && app.Countdown.Ring.Fraction<1, "ring represents remaining fraction");
            app.ShowManager(); app.Manager.Width=480; await Task.Delay(100); Check(app.Manager.Columns==1, "manager narrow single column"); CaptureWindow(app.Manager,Path.Combine(evidence,"manager-narrow.png")); app.Manager.Width=660; await Task.Delay(100); Check(app.Manager.Columns==2, "manager wide double column"); app.Manager.Close();
            Check(Icons.Target(app.Storage.DirectoryPath)!=null && Icons.Brand!=null, "real shell icon and embedded brand available");
            app.Settings.Theme="light"; app.Theme.Refresh(); app.Status.Sample(); await Task.Delay(2200); app.Status.Sample(); Capture(app,Path.Combine(evidence,"six-components-light.png"));
            app.Settings.Theme="dark"; app.Theme.Refresh(); Capture(app,Path.Combine(evidence,"six-components-dark.png"));
            app.Settings.Theme="system"; app.Theme.ApplyPalette(Colors.SeaGreen, Theme.Blend(Colors.SeaGreen,Colors.White,.55),false,false,true); Capture(app,Path.Combine(evidence,"six-components-reference-green.png"));
            app.ShowManager(); CaptureWindow(app.Manager,Path.Combine(evidence,"manager-reference-green.png")); app.Manager.Close(); app.Settings.Theme="system"; app.Theme.Refresh();
            foreach(var w in app.Widgets) { CaptureScaled(w,Path.Combine(evidence,"scale-150-"+w.State.Id+".png"),1.5); CaptureScaled(w,Path.Combine(evidence,"scale-200-"+w.State.Id+".png"),2); }
            Check(true,"150/200 percent control rendering exported (physical monitor DPI not changed)");
            report.Add("LIMIT v1.2: real mouse resizing, actual network upload/download and reconnect, physical DPI/multi-monitor/wake remain interactive checks; SVG icons are embedded static MIT assets.");
        }
        static void AutoStartChecks()
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run", name = "ChineseDesktopWidgets";
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                object original = key.GetValue(name); RegistryValueKind kind = original == null ? RegistryValueKind.String : key.GetValueKind(name);
                try
                {
                    string current = Process.GetCurrentProcess().MainModule.FileName;
                    Native.SetAutoStart(true, current); Check((string)key.GetValue(name) == "\"" + current + "\"", "login autostart exact quoted path");
                    string moved = Path.Combine(Path.GetDirectoryName(current), "移动文件夹", "桌面小组件.exe"); Native.SetAutoStart(true, moved); Check((string)key.GetValue(name) == "\"" + moved + "\"", "autostart migrated path updated");
                    Native.SetAutoStart(false, current); Check(key.GetValue(name) == null, "autostart disabled");
                }
                finally { if (original == null) key.DeleteValue(name, false); else key.SetValue(name, original, kind); }
                Check(Object.Equals(key.GetValue(name), original), "autostart original value restored after verification");
            }
        }
        static async Task Measure(WidgetApp app)
        {
            await Task.Delay(5000); var process = Process.GetCurrentProcess(); TimeSpan before = process.TotalProcessorTime; var watch = Stopwatch.StartNew();
            long peak = 0; double sum = 0, privateSum = 0, managedSum = 0; int count = 0;
            for (int i = 0; i < 60; i++) { await Task.Delay(5000); process.Refresh(); long working = process.WorkingSet64; peak = Math.Max(peak, working); sum += working; privateSum += process.PrivateMemorySize64; managedSum += GC.GetTotalMemory(false); count++; }
            process.Refresh(); double cpu = (process.TotalProcessorTime - before).TotalSeconds / watch.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
            report.Add("Environment: " + Environment.OSVersion + "; CPUs=" + Environment.ProcessorCount + "; x64=" + Environment.Is64BitProcess);
            report.Add("Rendering: " + RenderOptions.ProcessRenderMode);
            report.Add("Average private MB: " + (privateSum / count / 1048576).ToString("F2"));
            report.Add("Average managed MB (no forced collection): " + (managedSum / count / 1048576).ToString("F2"));
            report.Add("Duration seconds: " + watch.Elapsed.TotalSeconds.ToString("F2")); report.Add("Average CPU percent (whole machine): " + cpu.ToString("F4"));
            report.Add("Average working set MB: " + (sum / count / 1048576).ToString("F2")); report.Add("Peak sampled working set MB: " + (peak / 1048576.0).ToString("F2"));
            Check(cpu < 1, "CPU target <1%"); Check(peak <= 120L * 1048576, "working set target <=120MB"); Check(app.Widgets.All(w => w.IsVisible), "all "+app.Widgets.Count+" remained visible");
        }
        public static void Capture(WidgetApp app, string file)
        {
            string[][] columns = { new[] { "clock", "todo", "shortcuts" }, new[] { "countdown", "note", "status" } };
            double[] widths = new double[2], heights = new double[2];
            for(int c=0;c<2;c++) foreach(string id in columns[c]) { var w=app.Widgets.Find(v=>v.State.Id==id); w.UpdateLayout(); widths[c]=Math.Max(widths[c],w.ActualWidth); heights[c]+=w.ActualHeight+16; }
            int width=(int)Math.Ceiling(widths[0]+widths[1]+48), height=(int)Math.Ceiling(Math.Max(heights[0],heights[1])+16);
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(230,236,227)),null,new Rect(0,0,width,height));
                for(int c=0;c<2;c++) { double y=16, x=c==0?16:widths[0]+32; foreach(string id in columns[c]) { Window w=app.Widgets.Find(v=>v.State.Id==id); var image=RenderElement(w); dc.DrawImage(image,new Rect(x,y,w.ActualWidth,w.ActualHeight)); y+=w.ActualHeight+16; } }
            }
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); SaveBitmap(bitmap,file);
        }
        public static void CaptureWindow(Window w, string file)
        { CaptureElement(w,file); }
        static void CaptureScaled(Window w,string file,double scale)
        { SaveBitmap(RenderElement(w,scale),file); }
    }
}
