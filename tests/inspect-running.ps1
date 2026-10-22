[CmdletBinding()]
param([Parameter(Mandatory=$true)][int]$ProcessId)
$ErrorActionPreference = 'Stop'
# Read-only Windows evidence; run on the same desktop as the application.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class RunningWidgetEvidence {
 public delegate bool Callback(IntPtr h, IntPtr param);
 [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr param);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder text, int max);
 public static string[] Read(uint process) {
  var rows = new List<string>();
  EnumWindows(delegate(IntPtr h, IntPtr p) {
   uint id; GetWindowThreadProcessId(h, out id);
   if (id == process) {
    var title = new StringBuilder(512); GetWindowText(h, title, 512);
    if (title.Length > 0) rows.Add(IsWindowVisible(h) + " | " + title);
   }
   return true;
  }, IntPtr.Zero);
  return rows.ToArray();
 }
}
'@
$taskRoot = Split-Path -Parent $PSScriptRoot
$rows = [RunningWidgetEvidence]::Read($ProcessId)
$report = @("ProcessId: $ProcessId") + @($rows)
$report | Set-Content -LiteralPath (Join-Path $taskRoot 'artifacts\正常启动窗口.txt') -Encoding UTF8
$report
if (@($rows | Where-Object { $_ -like 'True | 桌面小组件 · *' -and $_ -ne 'True | 桌面小组件 · 管理' }).Count -ne 6) { throw '未看到六个可见组件窗口；请确认脚本在程序所在桌面执行。' }
