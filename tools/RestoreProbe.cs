using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DesktopWidgets;
public static class RestoreProbe
{
    [STAThread] public static void Main(string[] args)
    {
        var app=new WidgetApp { Verifying=true,ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"restore-probe-data"));
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async delegate {
            try
            {
                if(args[0]=="prepare") { app.Note.Left=330; app.Note.Top=240; app.Note.Width=310; app.Note.Height=345; app.Note.FinishPlacement(); app.Note.Rename("重启恢复便签"); app.Note.SetPinned(true); app.Note.SetMode(3); app.Countdown.SetDuration(83); app.Note.Editor.Text="模式3重启内容\n中文 😀"; app.SaveData(); app.SaveSettings(); }
                else { await Task.Delay(200); var w=app.Note; bool passed=w.State.Mode==3 && w.Desktop.Attached && w.IsVisible && !w.Topmost && w.State.Pinned && w.DisplayName=="重启恢复便签" && w.State.Left==330 && w.State.Top==240 && w.Width==310 && w.Height==345 && app.Countdown.Timer.Duration==83 && !app.Countdown.Timer.Running && app.Note.Editor.Text=="模式3重启内容\n中文 😀"; File.WriteAllText(args[1],(passed?"PASS ":"FAIL ")+"separate process startup restores desktop mode layout title pin preference exact seconds and note\n"+w.Desktop.Snapshot()); }
            }
            finally { app.Quit(); }
        })); app.Run();
    }
}
