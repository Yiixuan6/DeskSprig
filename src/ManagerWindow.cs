using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopWidgets
{
    public sealed class ManagerWindow : Window
    {
        readonly WidgetApp app;
        readonly TextBlock summary = Ui.Text("", 13), memory = Ui.Text("", 12), opacityValue = Ui.Text("", 12);
        readonly Grid widgetGrid = new Grid();
        FrameworkElement[] rows;
        public ToggleButton[] VisibilityButtons;
        public CheckBox[] PinBoxes;
        public Button[] ModeButtons;
        public Button[] RenameButtons;
        public readonly ShortcutManagerPanel ShortcutPanel;
        TextBlock[] widgetNames;
        public readonly RadioButton[] LanguageOptions=new RadioButton[4];
        public readonly RadioButton[] ThemeOptions = new RadioButton[3];
        public readonly Slider OpacitySlider;
        public readonly CheckBox AutoStart = new CheckBox { Margin = new Thickness(0,14,0,2) };
        public readonly Button ShowAllButton, HideAllButton, ArrangeButton;
        bool updating;
        public int Columns { get; private set; }
        public ManagerWindow(WidgetApp owner)
        {
            app = owner; Native.StyleWindow(this);
            L.Bind(this,Window.TitleProperty,"桌面小组件 · 管理"); Icon = Icons.Brand; Width = 660; Height = Math.Min(700, SystemParameters.WorkArea.Height - 24); MinWidth = 390; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Microsoft YaHei UI"); UseLayoutRounding = true;
            SetResourceReference(BackgroundProperty,"SolidCard"); SetResourceReference(ForegroundProperty,"Ink");
            var root = new StackPanel { Margin = new Thickness(24,22,24,20) };
            var heading = new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); heading.ColumnDefinitions.Add(new ColumnDefinition());
            var logo = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Margin = new Thickness(0,0,17,0), Child = new Image { Source = Icons.Brand, Margin = new Thickness(10) } };
            logo.SetResourceReference(Border.BackgroundProperty,"Field"); logo.SetResourceReference(Border.BorderBrushProperty,"Line"); heading.Children.Add(logo);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; var title = Ui.Label("让桌面保持顺手",28); title.FontWeight = FontWeights.SemiBold; titles.Children.Add(title); summary.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); titles.Children.Add(summary); Grid.SetColumn(titles,1); heading.Children.Add(titles); root.Children.Add(heading);
            var actions = new UniformGrid { Columns = 3, Margin = new Thickness(0,18,0,15) };
            ShowAllButton = Ui.LocalButton("全部显示", "显示全部组件",app.RestoreAll); HideAllButton = Ui.LocalButton("全部隐藏", "隐藏全部组件",app.HideAll); ArrangeButton = Ui.LocalButton("🔒 整理位置", "确认后整理自由摆放的可见组件",app.ArrangeWidgets);
            actions.Children.Add(ShowAllButton); actions.Children.Add(HideAllButton); actions.Children.Add(ArrangeButton); root.Children.Add(actions); root.Children.Add(Divider());
            RebuildWidgetRows();
            root.Children.Add(widgetGrid); root.Children.Add(Divider());
            ShortcutPanel=new ShortcutManagerPanel(app); root.Children.Add(ShortcutPanel); root.Children.Add(Divider());
            var appearance=new StackPanel { Margin=new Thickness(3,12,3,14) }; var section=Ui.Label("外观与启动",15); section.FontWeight=FontWeights.SemiBold; appearance.Children.Add(section);
            var themeGrid=new UniformGrid { Columns=3 }; string[] modes=UiChoices.ThemeKeys, labelsText=UiChoices.ThemeLabels;
            for(int i=0;i<3;i++) { string mode=modes[i]; var radio=new RadioButton { Content=L.T(labelsText[i]),GroupName="Theme",Style=(Style)app.Resources["ThemeTab"] }; radio.Checked+=delegate { if(!updating) app.SetTheme(mode); }; L.Bind(radio,ContentControl.ContentProperty,labelsText[i]); themeGrid.Children.Add(radio); ThemeOptions[i]=radio; }
            var themeBox=new Border { CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),Margin=new Thickness(0,10,0,13),Child=themeGrid }; themeBox.SetResourceReference(Border.BorderBrushProperty,"Line"); appearance.Children.Add(themeBox);
            var opacity=new Grid(); opacity.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(112) }); opacity.ColumnDefinitions.Add(new ColumnDefinition()); opacity.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(44) }); opacity.Children.Add(Ui.Label("背景不透明度",12));
            OpacitySlider=new Slider { Minimum=35,Maximum=100,TickFrequency=5,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0) }; OpacitySlider.ValueChanged+=delegate { if(!updating) app.SetOpacity(OpacitySlider.Value/100); }; Grid.SetColumn(OpacitySlider,1); opacity.Children.Add(OpacitySlider); Grid.SetColumn(opacityValue,2); opacityValue.TextAlignment=TextAlignment.Right; opacity.Children.Add(opacityValue); appearance.Children.Add(opacity);
            AutoStart.Checked+=delegate { if(!updating) { app.ChangeAutoStart(true); Refresh(); } }; AutoStart.Unchecked+=delegate { if(!updating) { app.ChangeAutoStart(false); Refresh(); } }; AutoStart.Content=Ui.Label("登录 Windows 后自动运行",12); appearance.Children.Add(AutoStart);
            appearance.Children.Add(Ui.Label("语言",15));
            var languages=new WrapPanel { Margin=new Thickness(0,5,0,3) };
            for(int i=0;i<L.Codes.Length;i++) { string code=L.Codes[i]; var option=new RadioButton { Content=L.NativeNames[i],GroupName="Language",Style=(Style)app.Resources["ThemeTab"],Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,4,4) }; if(i==0) L.Bind(option,ContentControl.ContentProperty,"跟随系统"); if(i==0) L.Bind(option,AutomationProperties.NameProperty,"跟随系统"); else AutomationProperties.SetName(option,L.NativeNames[i]); option.Checked+=delegate { if(!updating) app.SetLanguage(code); }; LanguageOptions[i]=option; languages.Children.Add(option); }
            appearance.Children.Add(languages); var languageHelp=Ui.Label("选择界面语言，立即生效；不会修改你的内容。",10); languageHelp.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); appearance.Children.Add(languageHelp); root.Children.Add(appearance); root.Children.Add(Divider());
            var foot=new Grid { Margin=new Thickness(3,13,3,0) }; foot.ColumnDefinitions.Add(new ColumnDefinition()); foot.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); memory.VerticalAlignment=VerticalAlignment.Center; foot.Children.Add(memory); var refresh=Ui.LocalButton("刷新占用", "查看当前程序内存占用",RefreshMemory); Grid.SetColumn(refresh,1); foot.Children.Add(refresh); root.Children.Add(foot);
            var help=Ui.Label("关闭此页面后，小组件继续运行。退出程序请使用托盘菜单。",10); help.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); root.Children.Add(help); Content=Ui.Scroll(root);
            SizeChanged+=delegate { Reflow(); }; app.WidgetsChanged+=Refresh; Closed+=delegate { app.WidgetsChanged-=Refresh; app.ManagerClosed(this); }; Loaded+=delegate { Reflow(); RefreshMemory(); }; Refresh();
        }
        void RebuildWidgetRows()
        {
            int count=app.Widgets.Count; VisibilityButtons=new ToggleButton[count]; PinBoxes=new CheckBox[count]; ModeButtons=new Button[count]; RenameButtons=new Button[count]; rows=new FrameworkElement[count]; widgetNames=new TextBlock[count]; widgetGrid.Children.Clear(); Columns=0;
            string[] descriptions = { "时钟与日期", "独立圆环计时", "记录事项", "随手记录", "CPU、内存与网速", "常用软件与文件" };
            for (int i=0;i<count;i++)
            {
                int index=i; var w=app.Widgets[i]; var row=new Grid { Margin=new Thickness(7,12,7,12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(48) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(28) });
                var icon=Icons.Glyph(w.Kind,22,"Ink"); icon.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(icon);
                var labels=new StackPanel { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(7,0,4,0) }; var name=Ui.Text(w.DisplayName,14); name.FontWeight=FontWeights.SemiBold; name.TextWrapping=TextWrapping.NoWrap; name.TextTrimming=TextTrimming.CharacterEllipsis; widgetNames[i]=name; labels.ContextMenu=w.RenameMenu(); var titleRow=new Grid(); titleRow.ColumnDefinitions.Add(new ColumnDefinition()); titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); titleRow.Children.Add(name); var rename=Ui.Button(L.T("改名"),L.T("修改“{0}”的名称",w.DisplayName),w.PromptRename); rename.Style=(Style)app.Resources["TextAction"]; rename.Margin=new Thickness(5,0,0,0); Grid.SetColumn(rename,1); titleRow.Children.Add(rename); RenameButtons[i]=rename; labels.Children.Add(titleRow); var description=Ui.Label(descriptions[Array.IndexOf(WidgetApp.Ids,w.Kind)],10); description.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); labels.Children.Add(description); Grid.SetColumn(labels,1); row.Children.Add(labels);
                var mode=Ui.LocalButton("", "切换固定档位",delegate { w.SetMode(w.State.Mode%3+1); }); mode.Style=(Style)app.Resources["IconButton"]; ModeButtons[i]=mode; Grid.SetColumn(mode,4); row.Children.Add(mode);
                var pin=new CheckBox { VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, ToolTip=L.T("切换{0}置顶",w.DisplayName) }; pin.Style=(Style)app.Resources["PinToggle"];
                AutomationProperties.SetName(pin,L.T("置顶{0}",w.DisplayName)); pin.Checked+=delegate { if(!updating) w.SetPinned(true); }; pin.Unchecked+=delegate { if(!updating) w.SetPinned(false); }; PinBoxes[i]=pin; Grid.SetColumn(pin,2); row.Children.Add(pin);
                var show=new ToggleButton { Style=(Style)app.Resources["VisibilitySwitch"],VerticalAlignment=VerticalAlignment.Center,ToolTip=L.T("显示/隐藏{0}",w.DisplayName) };
                AutomationProperties.SetName(show,L.T("显示/隐藏{0}",w.DisplayName)); show.Click+=delegate { if(!updating) w.SetVisible(!w.IsVisible); }; VisibilityButtons[index]=show; Grid.SetColumn(show,3); row.Children.Add(show); rows[i]=row; widgetGrid.Children.Add(row);
            }
            Reflow();
        }
        public void SelectShortcutGroup(ShortcutWidget group) { ShortcutPanel.Select(group); ShortcutPanel.BringIntoView(); }
        public void PromptAddShortcutGroup() { string name=Ui.Prompt(this,"快捷入口组件名称（1–32 字）",L.T("快捷入口 {0}",app.Widgets.OfType<ShortcutWidget>().Count()+1),32); if(name!=null) { var group=app.AddShortcutGroup(name); if(group!=null) SelectShortcutGroup(group); } }
        static Border Divider() { var line=new Border { Height=1,Margin=new Thickness(0,3,0,3) }; line.SetResourceReference(Border.BackgroundProperty,"Line"); return line; }
        void Reflow()
        {
            int columns=ActualWidth>=610?2:1; if(Columns==columns) return; Columns=columns; widgetGrid.RowDefinitions.Clear(); widgetGrid.ColumnDefinitions.Clear();
            for(int i=0;i<columns;i++) widgetGrid.ColumnDefinitions.Add(new ColumnDefinition()); for(int i=0;i<(rows.Length+columns-1)/columns;i++) widgetGrid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            for(int i=0;i<rows.Length;i++) { Grid.SetRow(rows[i],i/columns); Grid.SetColumn(rows[i],i%columns); }
        }
        public void Refresh()
        {
            if(VisibilityButtons.Length!=app.Widgets.Count) RebuildWidgetRows();
            updating=true;
            try { int visible=0; for(int i=0;i<app.Widgets.Count;i++) { var w=app.Widgets[i]; if(w.IsVisible) visible++; VisibilityButtons[i].IsChecked=w.IsVisible; VisibilityButtons[i].Content=L.T(w.IsVisible?"隐藏":"显示"); PinBoxes[i].IsChecked=w.State.Pinned; PinBoxes[i].IsEnabled=w.State.Mode!=3; PinBoxes[i].Content=Icons.Glyph("pin",16,w.State.Pinned?"Selected":"Muted"); widgetNames[i].Text=w.DisplayName; widgetNames[i].ToolTip=w.DisplayName; ModeButtons[i].Content=Icons.Placement(w.State.Mode,16); ModeButtons[i].ToolTip=L.T("{0}（点击切换）",WidgetWindow.ModeName(w.State.Mode)); RenameButtons[i].Content=L.T("改名"); PinBoxes[i].ToolTip=L.T("切换{0}置顶",w.DisplayName); VisibilityButtons[i].ToolTip=L.T("显示/隐藏{0}",w.DisplayName); RenameButtons[i].ToolTip=L.T("修改“{0}”的名称",w.DisplayName); AutomationProperties.SetName(RenameButtons[i],L.T("修改“{0}”的名称",w.DisplayName)); AutomationProperties.SetName(ModeButtons[i],w.DisplayName+" · "+ModeButtons[i].ToolTip); AutomationProperties.SetName(PinBoxes[i],L.T("置顶{0}",w.DisplayName)); AutomationProperties.SetName(VisibilityButtons[i],L.T("显示/隐藏{0}",w.DisplayName)); }
                summary.Text=L.T("{0} 个组件 · {1} 个显示在桌面",app.Widgets.Count,visible); for(int i=0;i<LanguageOptions.Length;i++) LanguageOptions[i].IsChecked=(app.Settings.Language??"system")==L.Codes[i]; string[] modes=UiChoices.ThemeKeys; for(int i=0;i<3;i++) ThemeOptions[i].IsChecked=app.Settings.Theme==modes[i]; OpacitySlider.Value=app.Settings.Opacity*100; opacityValue.Text=(app.Settings.Opacity*100).ToString("0")+"%"; AutoStart.IsChecked=app.Settings.AutoStart;
            } finally { updating=false; }
            if(ShortcutPanel!=null) ShortcutPanel.Refresh();
        }
        public void RefreshMemory() { using(var p=Process.GetCurrentProcess()) memory.Text=L.T("程序内存约 {0} MB",(p.WorkingSet64/1048576.0).ToString("F0")); }
    }
}
