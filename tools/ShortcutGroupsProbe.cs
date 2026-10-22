using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DesktopWidgets;
public static class ShortcutGroupsProbe
{
    [STAThread] public static void Main(string[] args)
    {
        var app=new WidgetApp { Verifying=true,ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"groups-probe-data"));
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async delegate {
            try
            {
                string file=Path.Combine(app.Storage.DirectoryPath,"example.txt");
                if(args[0]=="prepare")
                {
                    File.WriteAllText(file,"contents"); app.Shortcuts.Add("原有组入口",app.Storage.DirectoryPath);
                    var group=app.AddShortcutGroup("工作"); app.AddShortcutGroup("空分组"); group.Add("文档",file,"file"); group.Add("程序",System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,"software"); group.SetFilter("file"); group.Rename("工作资料"); group.SetMode(2); group.SetPinned(true); group.SetVisible(false); app.SaveData(); app.SaveSettings();
                }
                else
                {
                    await Task.Delay(120); var group=app.Widgets.OfType<ShortcutWidget>().Single(w=>w.DisplayName=="工作资料"); var empty=app.Widgets.OfType<ShortcutWidget>().Single(w=>w.DisplayName=="空分组");
                    bool passed=app.Settings.Version==4 && app.Data.Version==1 && app.Widgets.Count==8 && group.Items.Count==2 && group.DisplayedCount==1 && group.Filter=="file" && group.State.Mode==2 && group.State.Pinned && !group.IsVisible && empty.Items.Count==0 && app.Shortcuts.Items.Count==1;
                    File.WriteAllText(args[1],(passed?"PASS ":"FAIL ")+"separate process restores multiple shortcut groups, empty group, entries, name, filter, lock, pin and visibility\n"+"Widgets="+app.Widgets.Count+"; group items="+group.Items.Count+"; original="+app.Shortcuts.Items.Count);
                }
            }
            catch(Exception ex) { File.WriteAllText(args[1],"FAIL "+ex); }
            finally { app.Quit(); }
        })); app.Run();
    }
}
