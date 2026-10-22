using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DesktopWidgets;

public static class LanguageProbe
{
    [STAThread] public static void Main(string[] args)
    {
        var app=new WidgetApp { Verifying=true,ShutdownMode=ShutdownMode.OnExplicitShutdown };
        try
        {
            app.Initialize(args[2]);
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async delegate {
                try
                {
                    string code=args[1], resolved=code=="system"?L.SystemCode():code;
                    if(args[0]=="prepare")
                    {
                        app.Note.Editor.Text="开始\n用户内容 English 😀"; app.Note.Rename("全部显示"); app.Note.SetMode(3);
                        app.Countdown.SetDuration(75); app.SetLanguage(code); app.SaveData();
                    }
                    else
                    {
                        app.ShowManager();
                        string show=resolved=="en-US"?"Show all":resolved=="zh-TW"?"全部顯示":"全部显示";
                        bool passed=L.Code==resolved && (app.Settings.Language??"system")==code && (string)app.Manager.ShowAllButton.Content==show && app.Note.Editor.Text=="开始\n用户内容 English 😀" && app.Note.DisplayName=="全部显示" && app.Note.Desktop.Attached && app.Countdown.Timer.Duration==75 && !app.Countdown.Timer.Running && app.Data.Version==1 && app.Settings.Version==4;
                        File.WriteAllText(args[3],(passed?"PASS ":"FAIL ")+"separate process restores language="+code+"; effective="+L.Code+"; user text, custom name, desktop attachment and exact seconds preserved\n");
                        Diagnostics.CaptureWindow(app.Clock,Path.Combine(Path.GetDirectoryName(args[3]),"language-clock-"+code+".png"));
                        foreach(double width in new[] {660.0,390.0})
                        {
                            app.Manager.Width=width; app.SetTheme("dark"); await Task.Delay(60); var scroll=(ScrollViewer)app.Manager.Content;
                            scroll.SetResourceReference(Control.BackgroundProperty,"SolidCard"); scroll.ScrollToEnd(); await Task.Delay(60); scroll.UpdateLayout();
                            Diagnostics.CaptureElement(scroll,Path.Combine(Path.GetDirectoryName(args[3]),"language-settings-"+code+"-"+width+".png"));
                        }
                    }
                }
                catch(Exception ex) { File.WriteAllText(args[3],"FAIL "+ex); }
                finally { app.Quit(); }
            }));
            app.Run();
        }
        catch(Exception ex) { File.WriteAllText(args[3],"FAIL "+ex); }
    }
}
