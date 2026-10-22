using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DesktopWidgets;
public static class ExplorerRestartProbe
{
    [StructLayout(LayoutKind.Sequential)] struct Unique { public uint Pid; public System.Runtime.InteropServices.ComTypes.FILETIME Start; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Info
    {
        public Unique Process; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Name; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string Service;
        public int Type; public uint Status,Session; [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)] static extern uint RmStartSession(out uint session,uint flags,StringBuilder key);
    [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)] static extern uint RmRegisterResources(uint session,uint files,string[] names,uint processes,Unique[] apps,uint services,string[] serviceNames);
    [DllImport("rstrtmgr.dll")] static extern uint RmGetList(uint session,out uint needed,ref uint count,[In,Out] Info[] info,ref uint reasons);
    [DllImport("rstrtmgr.dll")] static extern uint RmShutdown(uint session,uint flags,IntPtr callback);
    [DllImport("rstrtmgr.dll")] static extern uint RmRestart(uint session,uint flags,IntPtr callback);
    [DllImport("rstrtmgr.dll")] static extern uint RmEndSession(uint session);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [STAThread] public static void Main(string[] args)
    {
        var log=new List<string>(); var app=new WidgetApp { Verifying=true,Measuring=true,ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Initialize(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"explorer-probe-data")); app.Note.SetMode(3);
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async delegate {
            uint session=0; bool started=false,shutdownRequested=false;
            try
            {
                uint pid; GetWindowThreadProcessId(app.Note.Desktop.Parent,out pid); log.Add("Explorer PID before="+pid); log.Add("Desktop before="+app.Note.Desktop.Snapshot());
                using(var p=Process.GetProcessById((int)pid))
                {
                    if(p.ProcessName!="explorer" || p.SessionId!=Process.GetCurrentProcess().SessionId) throw new InvalidOperationException("Unexpected desktop process/session");
                    long start=p.StartTime.ToFileTimeUtc(); var unique=new Unique { Pid=pid,Start=new System.Runtime.InteropServices.ComTypes.FILETIME { dwLowDateTime=(int)start,dwHighDateTime=(int)(start>>32) } };
                    uint result=RmStartSession(out session,0,new StringBuilder(33)); if(result!=0) throw new InvalidOperationException("RmStartSession="+result); started=true;
                    result=RmRegisterResources(session,0,null,1,new[] { unique },0,null); if(result!=0) throw new InvalidOperationException("RmRegisterResources="+result);
                    uint count=1,needed,reasons=0; var info=new Info[1]; result=RmGetList(session,out needed,ref count,info,ref reasons);
                    if(result!=0 || count!=1 || info[0].Process.Pid!=pid || !info[0].Restartable) { log.Add("NOT VERIFIED safe restart refused: list="+result+"; count="+count+"; reboot="+reasons+"; restartable="+info[0].Restartable); return; }
                    shutdownRequested=true; result=RmShutdown(session,0x10,IntPtr.Zero); log.Add("RmShutdown registered-only (no force)="+result);
                    uint restart=RmRestart(session,0,IntPtr.Zero); shutdownRequested=false; log.Add("RmRestart="+restart);
                    await Task.Delay(5000);
                    uint after; GetWindowThreadProcessId(DesktopHost.FindHost(),out after); log.Add("Explorer PID after="+after); log.Add("Desktop after="+app.Note.Desktop.Snapshot());
                    bool passed=result==0 && restart==0 && after!=0 && after!=pid && app.Note.Desktop.Attached && app.Note.IsVisible && app.Note.State.Mode==3;
                    log.Add((passed?"PASS ":"NOT VERIFIED ")+"actual Explorer restart and TaskbarCreated desktop reattachment");
                }
            }
            catch(Exception ex) { log.Add("NOT VERIFIED "+ex); }
            finally { if(started) { if(shutdownRequested) RmRestart(session,0,IntPtr.Zero); RmEndSession(session); } File.WriteAllLines(args[0],log,Encoding.UTF8); app.Quit(); }
        })); app.Run();
    }
}
