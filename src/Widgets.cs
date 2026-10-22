using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopWidgets
{
    public sealed class ClockWidget : WidgetWindow
    {
        readonly TextBlock clock = Ui.Text("", 72), date = Ui.Text("", 13), seconds = Ui.Text("", 11);
        public int Refreshes { get; private set; }
        public ClockWidget(WidgetApp app, WindowState state) : base(app, state, "时间")
        {
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            clock.FontFamily = new FontFamily("Segoe UI"); clock.FontWeight = FontWeights.Light; clock.Margin = new Thickness(0);
            var face = new System.Windows.Controls.Viewbox { Stretch = Stretch.Uniform, Child = clock, HorizontalAlignment = HorizontalAlignment.Left };
            layout.Children.Add(face); Grid.SetRow(date,1); layout.Children.Add(date); seconds.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); Grid.SetRow(seconds,2); layout.Children.Add(seconds); Body.Children.Add(layout); Refresh(DateTime.Now);
        }
        public void Refresh(DateTime now) { clock.Text = now.ToString("HH:mm"); date.Text = now.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN")); seconds.Text = now.ToString("ss") + " 秒"; Refreshes++; }
    }
    public sealed class CountdownWidget : WidgetWindow
    {
        readonly TextBlock remaining = Ui.Text("", 40), hint = Ui.Text("点击时间设置时长", 10);
        public readonly Button StartButton, PauseButton, ResetButton, EditButton;
        public readonly Countdown Timer;
        public readonly CountdownRing Ring = new CountdownRing();
        public int Notifications { get; private set; }
        public CountdownWidget(WidgetApp app, WindowState state) : base(app, state, "倒计时")
        {
            Timer = new Countdown(app.Data.CountdownSeconds);
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var face = new Grid(); face.Children.Add(Ring);
            var text = new Viewbox { Stretch = Stretch.Uniform, MaxHeight = 48, Margin = new Thickness(26), Child = remaining };
            remaining.FontFamily = new FontFamily("Segoe UI"); remaining.FontWeight = FontWeights.SemiBold;
            EditButton = Ui.Button("", "编辑倒计时时长（点击或按 Enter）", delegate { var dialog=new DurationDialog(this,Timer.Duration); if(dialog.ShowDialog()==true) SetDuration(dialog.Result); });
            EditButton.Style=(Style)app.Resources["TimeReadout"]; EditButton.Content = text; EditButton.HorizontalAlignment = HorizontalAlignment.Center; EditButton.VerticalAlignment = VerticalAlignment.Center;
            face.Children.Add(EditButton); layout.Children.Add(face);
            hint.HorizontalAlignment = HorizontalAlignment.Center; hint.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); Grid.SetRow(hint,1); layout.Children.Add(hint);
            var buttons = new Grid { Margin = new Thickness(0,8,0,0) }; for (int i=0;i<3;i++) buttons.ColumnDefinitions.Add(new ColumnDefinition());
            StartButton = Ui.Button("开始", "开始或继续倒计时", Start); StartButton.Style = (Style)app.Resources["PrimaryButton"];
            PauseButton = Ui.Button("暂停", "暂停倒计时", delegate { Timer.Pause(DateTime.UtcNow); Refresh(); app.UpdateSchedule(); }); ResetButton = Ui.Button("重置", "重置倒计时", Reset);
            buttons.Children.Add(StartButton); Grid.SetColumn(PauseButton,1); buttons.Children.Add(PauseButton); Grid.SetColumn(ResetButton,2); buttons.Children.Add(ResetButton); Grid.SetRow(buttons,2); layout.Children.Add(buttons);
            Body.Children.Add(layout); Refresh();
        }
        public bool SetDuration(int seconds)
        {
            if(seconds<1 || seconds>359999 || Timer.Running) return false;
            Timer.Reset(seconds); App.Data.CountdownSeconds=seconds; App.SaveData(); Refresh(); App.UpdateSchedule(); return true;
        }
        void Start() { if (Timer.Running) return; Timer.Start(DateTime.UtcNow); Refresh(); App.UpdateSchedule(); }
        void Reset() { Timer.Reset(Timer.Duration); Refresh(); App.UpdateSchedule(); }
        public void Tick(DateTime now)
        {
            if (Timer.Tick(now)) { Notifications++; if (!App.Verifying) { System.Media.SystemSounds.Exclamation.Play(); App.Balloon("倒计时结束", "设定的时间已到。"); App.ShowTimerNotice(); } }
            if (IsVisible) Refresh();
        }
        public void Refresh()
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Ceiling(Timer.Remaining)); remaining.Text = ((int)span.TotalHours).ToString("00")+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");
            Ring.Fraction = Timer.Duration == 0 ? 0 : Timer.Remaining / Timer.Duration; Ring.InvalidateVisual();
            EditButton.IsEnabled = !Timer.Running; StartButton.IsEnabled = !Timer.Running; PauseButton.IsEnabled = Timer.Running;
            StartButton.Content = Timer.Remaining > 0 && Timer.Remaining < Timer.Duration ? "继续" : "开始"; hint.Text = Timer.Running ? "正在计时" : "点击时间设置时长";
        }
    }
    public sealed class TodoWidget : WidgetWindow
    {
        readonly StackPanel list = new StackPanel();
        readonly Button collapse;
        public readonly TextBox Entry = Ui.Input("输入待办，按回车添加");
        public readonly Button AddButton;
        public TodoWidget(WidgetApp app, WindowState state) : base(app, state, "待办")
        {
            var body = new Grid(); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var input = new Grid { Margin = new Thickness(0, 0, 0, 8) }; input.ColumnDefinitions.Add(new ColumnDefinition()); input.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Entry.MaxLength = 1000; Entry.Tag = "添加待办事项"; input.Children.Add(Entry); AddButton = Ui.Button("＋", "添加待办", AddEntry); Grid.SetColumn(AddButton, 1); input.Children.Add(AddButton);
            Entry.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddEntry(); e.Handled = true; } };
            Grid.SetRow(input,2); body.Children.Add(input); var scroll = Ui.Scroll(list); body.Children.Add(scroll);
            collapse = Ui.Button("", "展开或折叠已完成待办", delegate { App.Settings.CollapseDone = !App.Settings.CollapseDone; App.SaveSettings(); Refresh(); }); Grid.SetRow(collapse, 1); body.Children.Add(collapse);
            Body.Children.Add(body); Refresh();
        }
        void AddEntry() { if (String.IsNullOrWhiteSpace(Entry.Text)) return; Add(Entry.Text.Trim()); Entry.Clear(); Entry.Focus(); }
        public void Add(string text) { App.Data.Todos.Add(new TodoItem { Text = text }); App.SaveData(); Refresh(); }
        public void Edit(TodoItem item, string text) { item.Text = text; App.SaveData(); Refresh(); }
        public void Complete(TodoItem item, bool value) { item.Done = value; App.SaveData(); Refresh(); }
        public void Delete(TodoItem item) { App.Data.Todos.Remove(item); App.SaveData(); Refresh(); }
        public void Refresh()
        {
            list.Children.Clear();
            var pending = App.Data.Todos.Where(t => !t.Done).ToList(); var done = App.Data.Todos.Where(t => t.Done).ToList();
            if (pending.Count == 0) list.Children.Add(Ui.Text(done.Count == 0 ? "添加一件想完成的小事。" : "未完成事项已清空。", 12));
            foreach (var item in pending) AddRow(item);
            if (!App.Settings.CollapseDone) foreach (var item in done) AddRow(item);
            collapse.Content = (App.Settings.CollapseDone ? "▸" : "▾") + " 已完成（" + done.Count + "）"; collapse.IsEnabled = done.Count > 0;
        }
        void AddRow(TodoItem item)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 4) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = Ui.Text(item.Text, 13); if (item.Done) label.TextDecorations = TextDecorations.Strikethrough;
            if (item.Done) label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            var checkbox = new CheckBox { Content = label, IsChecked = item.Done, VerticalAlignment = VerticalAlignment.Center };
            checkbox.Checked += delegate { Complete(item, true); }; checkbox.Unchecked += delegate { Complete(item, false); }; row.Children.Add(checkbox);
            var menu = new ContextMenu(); menu.SetResourceReference(Control.BackgroundProperty, "Field"); menu.SetResourceReference(Control.ForegroundProperty, "Ink");
            var edit = new MenuItem { Header = "编辑" }; edit.Click += delegate { string text = Ui.Prompt(this, "编辑待办", item.Text); if (text != null) Edit(item, text); }; menu.Items.Add(edit);
            var delete = new MenuItem { Header = "删除" }; delete.Click += delegate { Delete(item); }; menu.Items.Add(delete);
            Button options = null; options = Ui.Button("⋯", "编辑或删除待办", delegate { menu.PlacementTarget = options; menu.IsOpen = true; }); options.Padding = new Thickness(6, 3, 6, 3); Grid.SetColumn(options, 1); row.Children.Add(options);
            list.Children.Add(row);
        }
    }
    public sealed class NoteWidget : WidgetWindow
    {
        public readonly TextBox Editor = Ui.Input("便签内容，停止输入后自动保存");
        public readonly TextBlock Status = Ui.Text("自动保存", 11);
        public NoteWidget(WidgetApp app, WindowState state) : base(app, state, "便签")
        {
            var body = new Grid(); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Editor.AcceptsReturn = true; Editor.TextWrapping = TextWrapping.Wrap; Editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; Editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; Editor.Text = app.Data.Note;
            Editor.FontSize = 16; Editor.Tag = "把重要的事情留在眼前。";
            Editor.TextChanged += delegate { App.Data.Note = Editor.Text; Status.Text = "等待保存…"; App.ScheduleNote(); };
            body.Children.Add(Editor); Grid.SetRow(Status, 1); body.Children.Add(Status); Body.Children.Add(body);
        }
        public void Saved(bool success) { Status.Text = success ? "已保存 · " + DateTime.Now.ToString("HH:mm") : "保存失败，请检查目录权限"; }
    }
    public sealed class StatusWidget : WidgetWindow
    {
        readonly TextBlock cpu = Ui.Text("采样中", 21), memory = Ui.Text("", 21), detail = Ui.Text("", 10), upload = Ui.Text("采样中", 14), download = Ui.Text("采样中", 14);
        public readonly TextBlock CpuFrequency = Ui.Text("采样中",10);
        public TextBlock MemoryDetail { get { return detail; } }
        readonly ProgressBar cpuBar = new ProgressBar { Minimum = 0, Maximum = 100, Margin = new Thickness(0, 5, 0, 5) }, memoryBar = new ProgressBar { Minimum = 0, Maximum = 100, Margin = new Thickness(0, 5, 0, 5) };
        public readonly SystemSampler Sampler = new SystemSampler();
        public readonly NetworkSampler Network = new NetworkSampler();
        public readonly Sparkline UploadTrend = new Sparkline { Height = 24, MinWidth = 45 }, DownloadTrend = new Sparkline { Height = 24, MinWidth = 45 };
        public NetworkReading LastNetwork { get; private set; }
        public StatusWidget(WidgetApp app, WindowState state) : base(app, state, "系统状态")
        {
            var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            CpuFrequency.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); detail.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); CpuFrequency.ToolTip="系统报告的当前频率（各逻辑处理器平均值）";
            var cpuPanel = new StackPanel { Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center }; cpuPanel.Children.Add(Ui.Text("CPU",12)); cpuPanel.Children.Add(cpu); cpuPanel.Children.Add(cpuBar); cpuPanel.Children.Add(CpuFrequency); grid.Children.Add(cpuPanel);
            var memPanel = new StackPanel { Margin = new Thickness(10,0,0,0), VerticalAlignment = VerticalAlignment.Center }; memPanel.Children.Add(Ui.Text("内存",12)); memPanel.Children.Add(memory); memPanel.Children.Add(memoryBar); memPanel.Children.Add(detail); Grid.SetColumn(memPanel,1); grid.Children.Add(memPanel);
            var line = new Border { Height = 1, Margin = new Thickness(0,10,0,10) }; line.SetResourceReference(Border.BackgroundProperty,"Line"); Grid.SetRow(line,1); Grid.SetColumnSpan(line,2); grid.Children.Add(line);
            var upPanel = NetworkPanel("up","上传",upload,UploadTrend); Grid.SetRow(upPanel,2); grid.Children.Add(upPanel);
            var downPanel = NetworkPanel("down","下载",download,DownloadTrend); Grid.SetRow(downPanel,2); Grid.SetColumn(downPanel,1); grid.Children.Add(downPanel);
            var viewport = Ui.Scroll(grid); grid.HorizontalAlignment = HorizontalAlignment.Left; Body.Children.Add(viewport);
            Action fit = delegate { if (viewport.ViewportWidth <= 0) return; grid.Width = viewport.ViewportWidth; double half = viewport.ViewportWidth / 2; foreach (var column in grid.ColumnDefinitions) { column.MaxWidth = half; column.Width = new GridLength(half); } };
            viewport.Loaded += delegate { fit(); }; viewport.ScrollChanged += delegate(object sender, ScrollChangedEventArgs e) { if (e.ViewportWidthChange != 0) fit(); };
        }
        static UIElement NetworkPanel(string icon, string title, TextBlock value, Sparkline trend)
        {
            var panel = new StackPanel { Margin = new Thickness(0,0,7,0), VerticalAlignment = VerticalAlignment.Center }; var heading = new StackPanel { Orientation = Orientation.Horizontal };
            heading.Children.Add(Icons.Glyph(icon,16,"Selected")); var text = Ui.Text(title,11); text.Margin = new Thickness(4,0,0,0); heading.Children.Add(text); panel.Children.Add(heading); panel.Children.Add(value); panel.Children.Add(trend); return panel;
        }
        public void ResetSampling() { Sampler.Reset(); Network.Reset(); UploadTrend.Clear(); DownloadTrend.Clear(); }
        public void Redraw() { UploadTrend.InvalidateVisual(); DownloadTrend.InvalidateVisual(); }
        public void Sample()
        {
            double? use = Sampler.Cpu(); cpu.Text = use.HasValue ? use.Value.ToString("F0")+"%" : "采样中"; cpuBar.Value = use ?? 0;
            double? frequency=Sampler.FrequencyGHz(); CpuFrequency.Text=frequency.HasValue?frequency.Value.ToString("F2")+" GHz":"暂不可用";
            try { var m = Sampler.Memory(); memory.Text = m.Load+"%"; memoryBar.Value = m.Load; detail.Text = ((m.Total-m.Available)/1073741824.0).ToString("F1")+" / "+(m.Total/1073741824.0).ToString("F1")+" GB"; } catch { memory.Text = "暂不可用"; }
            LastNetwork = Network.Sample();
            upload.Text = LastNetwork.State == "暂不可用" || LastNetwork.State == "采样中" ? LastNetwork.State : NetworkSampler.Format(LastNetwork.Upload);
            download.Text = LastNetwork.State == "暂不可用" || LastNetwork.State == "采样中" ? LastNetwork.State : NetworkSampler.Format(LastNetwork.Download);
            UploadTrend.Add(LastNetwork.Upload); DownloadTrend.Add(LastNetwork.Download);
        }
    }
    public sealed class ShortcutWidget : WidgetWindow
    {
        readonly WrapPanel list = new WrapPanel();
        public readonly RadioButton[] Filters=new RadioButton[4];
        public readonly Button ManageButton;
        bool refreshing;
        public string Filter { get { return State.ShortcutFilter??"all"; } }
        public System.Collections.Generic.List<ShortcutItem> Items { get { return App.Data.Shortcuts.Where(s=>(s.GroupId??"shortcuts")==State.Id).ToList(); } }
        public int DisplayedCount { get; private set; }
        public static string ItemKind(ShortcutItem item) { if(item.Kind!=null) return item.Kind; if(Directory.Exists(item.Path)) return "folder"; string ext=Path.GetExtension(item.Path).ToLowerInvariant(); return new[] {".exe",".lnk",".bat",".cmd",".com",".msi",".ps1"}.Contains(ext)?"software":"file"; }
        public ShortcutWidget(WidgetApp app, WindowState state) : base(app, state, "快捷入口")
        {
            var body = new Grid(); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(Ui.Scroll(list)); var footer=new StackPanel { Margin=new Thickness(0,6,0,0) }; var buttons=new System.Windows.Controls.Primitives.UniformGrid { Columns=4 };
            string[] keys=UiChoices.FilterKeys,labels=UiChoices.FilterLabels;
            for(int i=0;i<4;i++) { string key=keys[i]; var filter=new RadioButton { Content=labels[i],GroupName="filter-"+state.Id,Style=(Style)app.Resources["ThemeTab"],Padding=new Thickness(3,5,3,5),FontSize=10,ToolTip="只显示"+labels[i]+"类型" }; filter.Checked+=delegate { if(!refreshing) SetFilter(key); }; Filters[i]=filter; buttons.Children.Add(filter); }
            footer.Children.Add(buttons); ManageButton=Ui.Button("添加 / 管理快捷方式","在管理页为“"+DisplayName+"”添加快捷方式",delegate { app.ManageShortcuts(this); }); ManageButton.Style=(Style)app.Resources["TextAction"]; ManageButton.HorizontalAlignment=HorizontalAlignment.Center; ManageButton.Margin=new Thickness(0,5,0,0); footer.Children.Add(ManageButton); Grid.SetRow(footer,1); body.Children.Add(footer); Body.Children.Add(body); Refresh();
        }
        public void ChooseTarget(string kind,Window owner)
        {
            if(kind=="folder") { try { string path=FolderPicker.Choose(owner); if(path!=null) Add(new DirectoryInfo(path).Name,path,kind); } catch(Exception ex) { App.Error("无法选择文件夹。\n"+ex.Message); } return; }
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = kind=="software" ? "选择软件或快捷方式" : "选择文件", Filter = kind=="software" ? "软件与快捷方式|*.exe;*.lnk;*.bat;*.cmd|所有文件|*.*" : "所有文件|*.*" };
            if (dialog.ShowDialog(owner) == true) Add(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName,kind);
        }
        public void SetFilter(string filter) { if(filter!="all" && filter!="software" && filter!="file" && filter!="folder") return; State.ShortcutFilter=filter=="all"?null:filter; App.SaveSettings(); Refresh(); }
        public void Add(string name, string path) { Add(name,path,null); }
        public void Add(string name, string path,string kind) { App.Data.Shortcuts.Add(new ShortcutItem { Name = String.IsNullOrWhiteSpace(name) ? path : name, Path = Path.GetFullPath(path),GroupId=State.Id=="shortcuts"?null:State.Id,Kind=kind }); App.SaveData(); Refresh(); App.NotifyWidgetsChanged(); }
        public void Rename(ShortcutItem item, string name) { item.Name = name; App.SaveData(); Refresh(); }
        public void Move(ShortcutItem item, int delta)
        { var group=Items; int index=group.IndexOf(item),next=index+delta; if(index<0 || next<0 || next>=group.Count) return; int target=App.Data.Shortcuts.IndexOf(group[next]); App.Data.Shortcuts.Remove(item); App.Data.Shortcuts.Insert(target,item); App.SaveData(); Refresh(); }
        public void Remove(ShortcutItem item) { if(!Items.Contains(item)) return; App.Data.Shortcuts.Remove(item); App.SaveData(); Refresh(); App.NotifyWidgetsChanged(); }
        public bool Open(ShortcutItem item)
        {
            try
            {
                if (!File.Exists(item.Path) && !Directory.Exists(item.Path)) throw new FileNotFoundException("目标不存在，可能已移动或删除。请移除此入口后重新添加。", item.Path);
                Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); return true;
            }
            catch (Exception ex) { App.Error("无法打开“" + item.Name + "”。\n" + ex.Message); return false; }
        }
        public void Refresh()
        {
            refreshing=true; string[] keys=UiChoices.FilterKeys; for(int n=0;n<4;n++) Filters[n].IsChecked=Filter==keys[n]; refreshing=false;
            var items=Items; var shown=items.Where(s=>Filter=="all" || ItemKind(s)==Filter).ToList(); DisplayedCount=shown.Count;
            list.Children.Clear(); if (shown.Count == 0) list.Children.Add(Ui.Text(items.Count==0?"在管理页添加快捷方式。":"此类型暂无快捷方式。", 12));
            foreach (ShortcutItem value in shown)
            {
                ShortcutItem item = value;
                var row = new Grid { Width = 75, Margin = new Thickness(2) };
                var content = new StackPanel(); content.Children.Add(new Image { Source = Icons.Shortcut(item,App.Storage.DirectoryPath), Width = 32, Height = 32, Margin = new Thickness(0,3,0,7) });
                content.Children.Add(new TextBlock { Text = item.Name, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, ToolTip = item.Name });
                var open = Ui.Button("", "打开 " + item.Name + "\n" + item.Path, delegate { Open(item); }); open.Content = content; open.Padding = new Thickness(5,8,5,7); row.Children.Add(open);
                var menu = new ContextMenu(); menu.SetResourceReference(Control.BackgroundProperty, "Field"); menu.SetResourceReference(Control.ForegroundProperty, "Ink");
                AddAction(menu, "改名", delegate { string name = Ui.Prompt(this, "快捷入口名称", item.Name); if (name != null) Rename(item, name); }, true);
                if(Directory.Exists(item.Path))
                {
                    AddAction(menu,"更换图标",delegate { var picker=new Microsoft.Win32.OpenFileDialog { Title="选择文件夹图标",Filter="图标和图片|*.ico;*.png;*.jpg;*.jpeg",CheckFileExists=true }; if(picker.ShowDialog(this)==true) try { SetIcon(item,picker.FileName); } catch(Exception ex) { App.Error("无法使用此图标。\n"+ex.Message); } },true);
                    AddAction(menu,"恢复默认图标",delegate { item.CustomIcon=null; Icons.ClearTargets(); App.SaveData(); Refresh(); },true);
                }
                int i = items.IndexOf(item); AddAction(menu, "上移", delegate { Move(item, -1); }, i > 0); AddAction(menu, "下移", delegate { Move(item, 1); }, i + 1 < items.Count); AddAction(menu, "移除", delegate { Remove(item); }, true);
                Button options = null; options = Ui.Button("⋯", "改名、调整顺序或移除", delegate { menu.PlacementTarget = options; menu.IsOpen = true; }); options.Style = (Style)App.Resources["IconButton"]; options.Width = 19; options.Height = 20; options.MinHeight = 20; options.HorizontalAlignment = HorizontalAlignment.Right; options.VerticalAlignment = VerticalAlignment.Top; row.Children.Add(options); list.Children.Add(row);
            }
        }
        static void AddAction(ContextMenu menu, string text, Action action, bool enabled)
        { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        public void SetIcon(ShortcutItem item,string file) { if(!Directory.Exists(item.Path)) throw new InvalidOperationException("仅文件夹支持更换图标。"); string reference=Icons.Import(file,App.Storage.DirectoryPath); item.CustomIcon=reference; Icons.ClearTargets(); App.SaveData(); Refresh(); }
    }
}
