using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopWidgets
{
    public static partial class Diagnostics
    {
        static async Task LanguageChecks(WidgetApp app,string evidence)
        {
            Check(L.ResolveSystem(CultureInfo.GetCultureInfo("zh-CN"))=="zh-CN" && L.ResolveSystem(CultureInfo.GetCultureInfo("zh-SG"))=="zh-CN", "system language maps simplified Chinese regions");
            Check(new[] {"zh-TW","zh-HK","zh-MO","zh-Hant"}.All(c=>L.ResolveSystem(CultureInfo.GetCultureInfo(c))=="zh-TW"), "system language maps traditional Chinese regions and script");
            Check(new[] {"en-GB","fr-FR","ja-JP"}.All(c=>L.ResolveSystem(CultureInfo.GetCultureInfo(c))=="en-US"), "unsupported system languages fall back to English");
            Check(L.Codes.SequenceEqual(new[] {"system","zh-CN","zh-TW","en-US"}),"language choices contain system and three built-in languages only");
            string oldName=app.Note.State.DisplayName, oldText=app.Note.Editor.Text;
            int oldMode=app.Note.State.Mode, oldDuration=app.Countdown.Timer.Duration;
            app.ShowManager(); var manager=app.Manager; manager.Width=660;
            app.Note.Rename("开始"); app.Note.SetMode(3);
            var editor=app.Note.Editor; var hwnd=app.Note.Desktop.Handle; var surface=app.Note.Desktop.Surface;
            editor.Text="全部显示\n开始 · custom English 😀"; editor.Select(2,3);
            app.Todo.Entry.Text="开始";
            app.Countdown.SetDuration(83); Click(app.Countdown.StartButton);
            var timer=app.Countdown.Timer;
            // SetDuration saves business data; create a new pending edit afterwards.
            editor.AppendText("\npending"); editor.Select(2,3);
            string text=editor.Text; byte[] before=File.ReadAllBytes(Path.Combine(app.Storage.DirectoryPath,"business.json"));
            string layout=String.Join("|",app.Widgets.Select(w=>w.State.Id+":"+w.State.Left+","+w.State.Top+","+w.Width+","+w.Height+","+w.State.Mode+","+w.State.Pinned+","+w.State.Visible+","+w.State.DisplayName));
            foreach(string code in new[] {"en-US","zh-TW","zh-CN"})
            {
                int index=Array.IndexOf(L.Codes,code); manager.LanguageOptions[index].IsChecked=true; await Task.Delay(35);
                string expectedShow=code=="en-US"?"Show all":code=="zh-TW"?"全部顯示":"全部显示";
                Check(L.Code==code && (string)manager.ShowAllButton.Content==expectedShow && (string)app.Countdown.PauseButton.Content==(code=="en-US"?"Pause":code=="zh-TW"?"暫停":"暂停"),"actual language selector updates manager and running timer "+code);
                Check(Object.ReferenceEquals(editor,app.Note.Editor) && editor.Text==text && editor.SelectionStart==2 && editor.SelectionLength==3 && app.Todo.Entry.Text=="开始" && app.Note.DisplayName=="开始", "language switch preserves user text exact selection and custom name "+code);
                Check(app.Note.Desktop.Attached && app.Note.Desktop.Handle==hwnd && Object.ReferenceEquals(surface,app.Note.Desktop.Surface),"already attached desktop changes language without reattaching "+code);
                Check(Object.ReferenceEquals(timer,app.Countdown.Timer) && timer.Running && timer.Duration==83 && timer.Remaining>80,"language switch preserves running countdown "+code);
                Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(app.Storage.DirectoryPath,"business.json"))) ,"language switch does not flush pending business data "+code);
                Check(layout==String.Join("|",app.Widgets.Select(w=>w.State.Id+":"+w.State.Left+","+w.State.Top+","+w.Width+","+w.Height+","+w.State.Mode+","+w.State.Pinned+","+w.State.Visible+","+w.State.DisplayName)),"language switch preserves layout visibility pin and names "+code);
                var menu=app.CreateTrayMenu(); var named=menu.Items.OfType<MenuItem>().Single(i=>(string)i.Header=="开始");
                Check((string)named.Header=="开始" && menu.Items.OfType<MenuItem>().Any(i=>(string)i.Header==(code=="en-US"?"Quit":code=="zh-TW"?"結束":"退出")),"tray translates actions and preserves custom name matching a translation key "+code);

            }
            Check(editor.Text==text && app.Note.Status.Text.Contains("等待保存"),"language switch keeps pending note status without false saved message");
            app.SaveData(); app.Note.Saved(false); app.SetLanguage("en-US");
            Check(app.Note.Status.Text.Contains("Save failed"),"language switch preserves failure status");
            foreach(string code in new[] {"en-US","zh-TW","zh-CN"})
            {
                app.SetLanguage(code);
                app.Settings.Theme="dark"; app.Theme.Refresh(); await Task.Delay(20); manager.UpdateLayout();
                CaptureLanguageManager(manager,Path.Combine(evidence,"language-manager-"+code+"-dark.png"));
                CaptureElement(surface,Path.Combine(evidence,"language-desktop-"+code+".png"));
                using(var duration=new DisposableDuration(app.Note,83))
                { duration.Window.Show(); await Task.Delay(15); CaptureWindow(duration.Window,Path.Combine(evidence,"language-duration-"+code+".png")); }
            }
            app.SetLanguage("en-US");
            var loaded=Storage.Read<Settings>(Path.Combine(app.Storage.DirectoryPath,"settings.json"),Settings.Valid);
            Check(loaded.Language=="en-US" && loaded.Version==4 && app.Data.Version==1,"language selection persists with existing data versions");
            manager.LanguageOptions[0].IsChecked=true;
            Check(app.Settings.Language==null && L.Preference=="system" && L.Code==L.SystemCode(),"system selector persists automatic preference and uses user display language");
            var unknown=new Settings { Language="removed-language" }; unknown.Migrate();
            Check(unknown.Language==null && Settings.Valid(unknown),"unknown language preference recovers without rejecting settings");
            app.SetLanguage("en-US"); manager.Width=390; await Task.Delay(30); manager.UpdateLayout();
            CaptureLanguageManager(manager,Path.Combine(evidence,"language-manager-en-US-narrow.png"));
            Check(manager.Columns==1 && manager.LanguageOptions.All(o=>o.ActualWidth>0 && o.ActualHeight>0),"English narrow manager keeps four language options available");
            app.SetLanguage("zh-CN"); app.Note.SetMode(oldMode); app.Note.Rename(oldName); editor.Text=oldText; app.Todo.Entry.Clear();
            Click(app.Countdown.PauseButton); app.Countdown.SetDuration(oldDuration); app.SaveData(); manager.Close();
            report.Add("LIMIT languages: system culture mapping and the current user display language are covered; changing Windows display language, notification delivery/sign-out, mouse/IME and mixed DPI remain interactive acceptance.");
        }
        static void CaptureLanguageManager(ManagerWindow manager,string file)
        {
            // Capture both ends of the real scroll viewport so settings remain reviewable.
            var scroll=(ScrollViewer)manager.Content; scroll.SetResourceReference(Control.BackgroundProperty,"SolidCard");
            manager.UpdateLayout(); scroll.ScrollToTop(); scroll.UpdateLayout(); CaptureElement(scroll,file);
            scroll.ScrollToEnd(); scroll.UpdateLayout(); CaptureElement(scroll,Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+"-settings.png"));
            scroll.ScrollToTop();

        }
        sealed class DisposableDuration : IDisposable
        {
            public readonly DurationDialog Window;
            public DisposableDuration(Window owner,int seconds) { Window=new DurationDialog(owner,seconds); }
            public void Dispose() { Window.Close(); }
        }
    }
}
