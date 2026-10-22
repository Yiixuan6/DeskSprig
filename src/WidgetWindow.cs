using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Runtime.InteropServices;

namespace DesktopWidgets
{
    public class WidgetWindow : Window
    {
        protected readonly WidgetApp App;
        public readonly WindowState State;
        public readonly Grid Body = new Grid();
        readonly Button pin;
        readonly Grid header;
        public readonly Button ModeButton;
        readonly TextBlock name;
        readonly List<Thumb> grips = new List<Thumb>();
        public readonly DesktopHost Desktop;
        public bool TemporaryFront { get; private set; }
        bool moving;
        Native.Rect dragStart;
        Native.CursorPoint dragCursor;
        SnapGesture dragSnap;
        double dragScale;
        public new bool IsVisible { get { return Desktop != null && Desktop.Attached ? Desktop.Visible : base.IsVisible; } }
        public string Kind { get { return WidgetApp.KindOf(State.Id); } }
        public string DisplayName { get { return State.DisplayName ?? WidgetApp.Names[Array.IndexOf(WidgetApp.Ids,Kind)]; } }
        public static string ModeName(int mode) { return mode==1?"自由摆放":mode==2?"锁定位置与大小":"固定桌面"; }
        bool ready, clamping;
        public WidgetWindow(WidgetApp app, WindowState state, string title)
        {
            App = app; State = state; Desktop=new DesktopHost(this); Title = "桌面小组件 · " + DisplayName; Icon = Icons.Brand;
            Width = Math.Max(240, state.Width); Height = Math.Max(Layout.MinimumHeight(state.Id), state.Height); MinHeight = Layout.MinimumHeight(state.Id); MinWidth = 240;
            Left = state.Left; Top = state.Top; Topmost = state.Pinned;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; FontFamily = new FontFamily("Microsoft YaHei UI"); UseLayoutRounding = true;
            SetResourceReference(ForegroundProperty, "Ink");
            var root = new Grid();
            var card = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(15, 11, 15, 17) };
            card.SetResourceReference(Border.BackgroundProperty, "Card"); card.SetResourceReference(Border.BorderBrushProperty, "CardLine");
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition());
            header = new Grid { Margin = new Thickness(0, 0, 0, 10), Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var heading = new Grid { VerticalAlignment = VerticalAlignment.Center }; heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.Children.Add(Icons.Glyph(Kind, 20, "Ink"));
            name=Ui.Text(DisplayName,14); name.FontWeight=FontWeights.SemiBold; name.Margin=new Thickness(9,0,3,0); name.TextWrapping=TextWrapping.NoWrap; name.TextTrimming=TextTrimming.CharacterEllipsis; name.ToolTip=DisplayName+"\n双击标题改名，右键更多操作"; Grid.SetColumn(name,1); heading.Children.Add(name);
            header.ContextMenu=RenameMenu();
            header.Children.Add(heading);
            ModeButton=Ui.Button("", "切换组件固定档位", delegate { SetMode(State.Mode%3+1); }); ModeButton.Style=(Style)app.Resources["IconButton"]; Grid.SetColumn(ModeButton,1); header.Children.Add(ModeButton);
            pin = Ui.Button("", "切换" + title + "置顶", delegate { SetPinned(!State.Pinned); }); pin.Style = (Style)app.Resources["IconButton"]; Grid.SetColumn(pin, 2); header.Children.Add(pin);
            header.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (FindButton(e.OriginalSource as DependencyObject)) return;
                if(e.ClickCount==2) { e.Handled=true; PromptRename(); return; }
                if(State.Mode!=1) return;
                try { moving=true; BeginSnap(); DragMove(); FinishPlacement(); } catch (InvalidOperationException) { } finally { moving=false; dragSnap=null; }
            };
            layout.Children.Add(header); Grid.SetRow(Body, 1); layout.Children.Add(Body); card.Child = layout; root.Children.Add(card);
            AddGrip(root, "left", HorizontalAlignment.Left, VerticalAlignment.Stretch, 6, Double.NaN, Cursors.SizeWE);
            AddGrip(root, "right", HorizontalAlignment.Right, VerticalAlignment.Stretch, 6, Double.NaN, Cursors.SizeWE);
            AddGrip(root, "top", HorizontalAlignment.Stretch, VerticalAlignment.Top, Double.NaN, 6, Cursors.SizeNS);
            AddGrip(root, "bottom", HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Double.NaN, 6, Cursors.SizeNS);
            AddGrip(root, "lefttop", HorizontalAlignment.Left, VerticalAlignment.Top, 12, 12, Cursors.SizeNWSE);
            AddGrip(root, "righttop", HorizontalAlignment.Right, VerticalAlignment.Top, 12, 12, Cursors.SizeNESW);
            AddGrip(root, "leftbottom", HorizontalAlignment.Left, VerticalAlignment.Bottom, 12, 12, Cursors.SizeNESW);
            AddGrip(root, "rightbottom", HorizontalAlignment.Right, VerticalAlignment.Bottom, 18, 18, Cursors.SizeNWSE);
            Content = root;
            SourceInitialized += delegate { HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(Hook); };
            Loaded += delegate { ready = true; Clamp(); UpdatePin(); };
            LocationChanged += delegate { if (ready && !Desktop.Attached && !TemporaryFront) { State.Left = Left; State.Top = Top; App.ScheduleSettings(); } };
            SizeChanged += delegate { if (ready) { State.Width = Width; State.Height = Height; App.ScheduleSettings(); } };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!App.Exiting) { e.Cancel = true; SetVisible(false); } };
            Closed+=delegate { Desktop.Dispose(); };
            Deactivated+=delegate { if(TemporaryFront) RestoreFront(); };
            UpdatePin();
        }
        static bool FindButton(DependencyObject item)
        { while (item != null) { if (item is Button) return true; item = VisualTreeHelper.GetParent(item); } return false; }
        void AddGrip(Grid root, string edge, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height, Cursor cursor)
        {
            var grip = new Thumb { HorizontalAlignment = horizontal, VerticalAlignment = vertical, Width = width, Height = height, Cursor = cursor, ToolTip = "拖动调整宽高" };
            var factory = new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            if (edge == "rightbottom")
            {
                var canvas = new FrameworkElementFactory(typeof(Canvas));
                for (int y = 0; y < 3; y++) for (int x = 2 - y; x < 3; x++) { var dot = new FrameworkElementFactory(typeof(Ellipse)); dot.SetValue(WidthProperty, 2.5); dot.SetValue(HeightProperty, 2.5); dot.SetValue(Canvas.LeftProperty, 2.0 + x * 4); dot.SetValue(Canvas.TopProperty, 2.0 + y * 4); dot.SetResourceReference(Shape.FillProperty, "Muted"); canvas.AppendChild(dot); }
                factory.AppendChild(canvas);
            }
            grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
            grip.DragDelta += delegate(object sender, DragDeltaEventArgs e) { ResizeFromEdge(edge, e.HorizontalChange, e.VerticalChange); };
            grip.DragCompleted += delegate { FinishPlacement(); }; root.Children.Add(grip); grips.Add(grip);
        }
        public void ResizeFromEdge(string edge, double dx, double dy)
        {
            if(State.Mode!=1) return;
            double oldWidth = Width, oldHeight = Height;
            if (edge.Contains("left")) { Width = Math.Max(MinWidth, Math.Min(MaxWidth, Width - dx)); Left += oldWidth - Width; }
            if (edge.Contains("right")) Width = Math.Max(MinWidth, Math.Min(MaxWidth, Width + dx));
            if (edge.Contains("top")) { Height = Math.Max(MinHeight, Math.Min(MaxHeight, Height - dy)); Top += oldHeight - Height; }
            if (edge.Contains("bottom")) Height = Math.Max(MinHeight, Math.Min(MaxHeight, Height + dy));
            State.Width = Width; State.Height = Height; App.ScheduleSettings();
        }
        IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if(msg==0x216 && moving && State.Mode==1 && dragSnap!=null && l!=IntPtr.Zero)
            {
                Native.CursorPoint cursor;
                if(Native.GetCursorPos(out cursor))
                {
                    var rect=(Native.Rect)Marshal.PtrToStructure(l,typeof(Native.Rect)); int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
                    int left=dragStart.Left+cursor.X-dragCursor.X,top=dragStart.Top+cursor.Y-dragCursor.Y;
                    double scale=Native.Scale(h); var raw=new Rect(left/scale,top/scale,width/scale,height/scale); var targets=SnapTargets(scale);
                    var work=SnapWorkArea(new Native.Rect { Left=left,Top=top,Right=left+width,Bottom=top+height },scale);
                    if(Math.Abs(scale-dragScale)>.001) { dragScale=scale; dragSnap=new SnapGesture(raw,targets,work); }
                    var point=dragSnap.Apply(raw,targets,Native.AltDown,work); rect.Left=(int)Math.Round(point.X*scale); rect.Top=(int)Math.Round(point.Y*scale); rect.Right=rect.Left+width; rect.Bottom=rect.Top+height;
                    Marshal.StructureToPtr(rect,l,false); handled=true; return new IntPtr(1);
                }
            }
            if (msg == 0x1A || msg == 0x320 || msg == 0x31A) App.Theme.QueueRefresh();
            if (msg == 0x7E || msg == 0x2E0) Dispatcher.BeginInvoke(new Action(Clamp)); return IntPtr.Zero;
        }
        public void SetPinned(bool value) { if(State.Mode==3) return; State.Pinned = value; Topmost = TemporaryFront || value; UpdatePin(); App.SaveSettings(); App.NotifyWidgetsChanged(); }
        void UpdatePin() { pin.Content = Icons.Glyph("pin", 17, State.Pinned ? "Accent" : "Muted"); pin.IsEnabled=State.Mode!=3; header.Cursor=State.Mode==1?Cursors.SizeAll:Cursors.Arrow; ModeButton.Content=Icons.Placement(State.Mode,16); ModeButton.ToolTip=ModeName(State.Mode)+"（点击切换）"; AutomationProperties.SetName(ModeButton,DisplayName+" · "+ModeButton.ToolTip); foreach(var g in grips) g.Visibility=State.Mode==1?Visibility.Visible:Visibility.Collapsed; }
        public void SetVisible(bool value)
        { State.Visible = value; if(Desktop.Attached) Desktop.SetVisible(value); else if (value) { App.Theme.Refresh(); Show(); Clamp(); } else Hide(); App.SaveSettings(); App.NotifyWidgetsChanged(); }
        public bool SetMode(int mode)
        {
            if(mode<1 || mode>3) return false;
            if(TemporaryFront) RestoreFront();
            Desktop.Dispose(); State.Mode=mode; Topmost=mode!=3 && State.Pinned;
            if(State.Visible) Show(); Clamp();
            bool success=true;
            if(mode==3) try { Desktop.Attach(); } catch(Exception ex) { State.Mode=2; State.Visible=true; Topmost=State.Pinned; Show(); App.Error("无法固定到桌面，已恢复到第 2 档锁定。\n"+ex.Message); success=false; }
            UpdatePin(); App.SaveSettings(); App.NotifyWidgetsChanged(); return success;
        }
        public void RestoreDesktop() { if(State.Mode==3 && !TemporaryFront) SetMode(3); }
        public void BringForward()
        {
            if(TemporaryFront) { Activate(); return; }
            Desktop.Dispose(); TemporaryFront=true; State.Visible=true; Show(); Topmost=true; Activate(); App.SaveSettings(); App.NotifyWidgetsChanged();
        }
        public void RestoreFront()
        {
            TemporaryFront=false; Topmost=State.Mode!=3 && State.Pinned; Left=State.Left; Top=State.Top;
            if(State.Mode==3) SetMode(3);
        }
        public ContextMenu RenameMenu()
        {
            var menu=new ContextMenu(); var rename=new MenuItem { Header="重命名" }; rename.Click+=delegate { PromptRename(); }; menu.Items.Add(rename);
            var reset=new MenuItem { Header="恢复默认名称" }; reset.Click+=delegate { Rename(null); }; menu.Items.Add(reset); return menu;
        }
        public void PromptRename() { string value=Ui.Prompt(this,"组件名称（1–32 字）",DisplayName,32); if(value!=null) Rename(value); }
        public bool Rename(string value)
        {
            if(value!=null) { value=value.Trim(); if(value.Length<1 || value.Length>32) return false; }
            State.DisplayName=value; Title="桌面小组件 · "+DisplayName; name.Text=DisplayName; name.ToolTip=DisplayName+"\n双击标题改名，右键更多操作"; AutomationProperties.SetName(this,Title); pin.ToolTip="切换"+DisplayName+"置顶"; Desktop.RefreshName(); UpdatePin(); App.SaveSettings(); App.NotifyWidgetsChanged(); return true;
        }
        List<Rect> SnapTargets(double scale)
        {
            var targets=new List<Rect>(); foreach(var widget in App.Widgets.Where(w=>w!=this && w.IsVisible)) { Native.Rect r; IntPtr handle=widget.Desktop.Attached?widget.Desktop.Handle:new WindowInteropHelper(widget).Handle; if(Native.GetWindowRect(handle,out r)) targets.Add(new Rect(r.Left/scale,r.Top/scale,(r.Right-r.Left)/scale,(r.Bottom-r.Top)/scale)); } return targets;
        }
        static Rect? SnapWorkArea(Native.Rect raw,double scale)
        {
            try { var work=Native.WorkingArea(raw); return new Rect(work.Left/scale,work.Top/scale,(work.Right-work.Left)/scale,(work.Bottom-work.Top)/scale); }
            catch { return null; }
        }
        void BeginSnap() { IntPtr h=new WindowInteropHelper(this).Handle; if(!Native.GetWindowRect(h,out dragStart) || !Native.GetCursorPos(out dragCursor)) { dragSnap=null; return; } dragScale=Native.Scale(h); dragSnap=new SnapGesture(new Rect(dragStart.Left/dragScale,dragStart.Top/dragScale,(dragStart.Right-dragStart.Left)/dragScale,(dragStart.Bottom-dragStart.Top)/dragScale),SnapTargets(dragScale),SnapWorkArea(dragStart,dragScale)); }
        public void FinishPlacement() { if(State.Mode!=1) return; Clamp(); App.SaveSettings(); }
        public void Clamp()
        {
            if (clamping || Desktop.Attached || TemporaryFront) return; clamping = true;
            try { Native.Clamp(this); State.Left = Left; State.Top = Top; State.Width = Width; State.Height = Height; }
            finally { clamping = false; }
        }
    }
}
