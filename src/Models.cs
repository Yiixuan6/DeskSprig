using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DesktopWidgets
{
    [DataContract] public sealed class TodoItem
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Text = "";
        [DataMember] public bool Done;
    }
    [DataContract] public sealed class ShortcutItem
    {
        [DataMember] public string Name = "";
        [DataMember] public string Path = "";
        [DataMember(EmitDefaultValue = false)] public string CustomIcon;
        [DataMember(EmitDefaultValue = false)] public string GroupId;
        [DataMember(EmitDefaultValue = false)] public string Kind;
    }
    [DataContract] public sealed class BusinessData
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<TodoItem> Todos = new List<TodoItem>();
        [DataMember] public string Note = "";
        [DataMember] public List<ShortcutItem> Shortcuts = new List<ShortcutItem>();
        [DataMember] public int CountdownSeconds = 300;
        public static bool Valid(BusinessData d)
        {
            if (d == null || d.Version != 1 || d.Todos == null || d.Shortcuts == null || d.Note == null || d.CountdownSeconds < 1 || d.CountdownSeconds > 359999) return false;
            foreach (TodoItem t in d.Todos) if (t == null || String.IsNullOrWhiteSpace(t.Text) || String.IsNullOrEmpty(t.Id)) return false;
            foreach (ShortcutItem s in d.Shortcuts) if (s == null || String.IsNullOrWhiteSpace(s.Name) || String.IsNullOrWhiteSpace(s.Path) || (s.GroupId!=null && !WidgetApp.IsShortcutInstance(s.GroupId)) || (s.Kind!=null && s.Kind!="software" && s.Kind!="file" && s.Kind!="folder")) return false;
            return true;
        }
    }
    [DataContract] public sealed class WindowState
    {
        [DataMember] public string Id;
        [DataMember] public double Left;
        [DataMember] public double Top;
        [DataMember] public double Height;
        [DataMember] public double Width;
        [DataMember] public bool Visible = true;
        [DataMember] public bool Pinned;
        [DataMember(EmitDefaultValue = false)] public string DisplayName;
        [DataMember] public int Mode = 1;
        [DataMember(EmitDefaultValue = false)] public string ShortcutFilter;
    }
    [DataContract] public sealed class Settings
    {
        [DataMember] public int Version = 4;
        [DataMember(EmitDefaultValue = false)] public string Language; // null follows the Windows display language
        [DataMember] public string Theme = "system";
        [DataMember] public double Opacity = 0.85;
        [DataMember] public bool AutoStart = true;
        [DataMember] public bool Initialized;
        [DataMember] public bool CollapseDone = true;
        [DataMember] public List<WindowState> Windows = new List<WindowState>();
        public static bool Valid(Settings s)
        {
            if (s == null || (s.Version < 1 || s.Version > 4) || s.Windows == null || (s.Theme != "system" && s.Theme != "light" && s.Theme != "dark") || !Finite(s.Opacity) || s.Opacity < .35 || s.Opacity > 1) return false;
            var ids = new HashSet<string>();
            foreach (WindowState w in s.Windows)
                if (w == null || !ids.Add(w.Id ?? "") || (Array.IndexOf(WidgetApp.Ids, w.Id) < 0 && !(s.Version==4 && WidgetApp.IsShortcutInstance(w.Id))) || !Finite(w.Left) || !Finite(w.Top) || !Finite(w.Height) || w.Height < 0 || w.Height > 10000 || !Finite(w.Width) || w.Width < 0 || w.Width > 10000 || (s.Version >= 2 && w.Width < 240) || (s.Version >= 3 && (w.Mode < 1 || w.Mode > 3 || (w.DisplayName != null && (w.DisplayName.Trim().Length < 1 || w.DisplayName.Length > 32)))) || (w.ShortcutFilter!=null && w.ShortcutFilter!="all" && w.ShortcutFilter!="software" && w.ShortcutFilter!="file" && w.ShortcutFilter!="folder")) return false;
            return true;
        }
        static bool Finite(double n) { return !Double.IsNaN(n) && !Double.IsInfinity(n); }
        public void Migrate()
        {
            foreach (var w in Windows) { if (w.Width == 0) { w.Width = 280; } w.Width = Math.Max(240, w.Width); w.Height = Math.Max(Layout.MinimumHeight(w.Id), w.Height); }
            foreach (var w in Windows) { if (Version < 3) w.Mode = 1; }
            if (Array.IndexOf(L.Codes, Language)<0 || Language=="system") Language=null;
            Version = 4;
        }
    }
    // UTC deadline makes sleep/resume independent of dispatcher ticks; no running state is persisted.
    public sealed class Countdown
    {
        public int Duration { get; private set; }
        public bool Running { get; private set; }
        public double Remaining { get; private set; }
        DateTime deadline;
        public Countdown(int seconds) { Reset(seconds); }
        public void Reset(int seconds) { Duration = seconds; Remaining = seconds; Running = false; }
        public void Start(DateTime now) { if (Remaining <= 0) Remaining = Duration; deadline = now.AddSeconds(Remaining); Running = true; }
        public void Pause(DateTime now) { if (Running) { Remaining = Math.Max(0, (deadline - now).TotalSeconds); Running = false; } }
        public bool Tick(DateTime now)
        {
            if (!Running) return false;
            Remaining = Math.Max(0, (deadline - now).TotalSeconds);
            if (Remaining > 0) return false;
            Running = false;
            return true;
        }
    }
}
