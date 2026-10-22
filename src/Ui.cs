using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopWidgets
{
    internal static class UiChoices
    {
        internal static readonly string[] ThemeKeys={"system","light","dark"};
        internal static readonly string[] ThemeLabels={"跟随系统","浅色","深色"};
        internal static readonly string[] ShortcutKinds={"software","file","folder"};
        internal static readonly string[] ShortcutLabels={"软件","文件","文件夹"};
        internal static readonly string[] FilterKeys=new[] {"all"}.Concat(ShortcutKinds).ToArray();
        internal static readonly string[] FilterLabels=new[] {"全部"}.Concat(ShortcutLabels).ToArray();
    }
    public static class Ui
    {
        public static bool? VerificationConfirmation;
        public static bool Confirm(Window owner,string question)
        {
            if(VerificationConfirmation.HasValue) return VerificationConfirmation.Value;
            var w=new Window { Title=L.T("整理位置"),Owner=owner,Width=340,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner };
            w.SetResourceReference(Window.BackgroundProperty,"SolidCard"); w.SetResourceReference(Control.ForegroundProperty,"Ink"); Native.StyleWindow(w);
            var panel=new StackPanel { Margin=new Thickness(20) }; panel.Children.Add(Label(question,16)); var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0) };
            actions.Children.Add(LocalButton("重置位置","确认重置位置",delegate { w.DialogResult=true; })); var cancel=LocalButton("取消","保留原位置",delegate { w.DialogResult=false; }); cancel.IsCancel=true; cancel.IsDefault=true; actions.Children.Add(cancel); panel.Children.Add(actions); w.Content=panel; w.Loaded+=delegate { cancel.Focus(); }; return w.ShowDialog()==true;
        }
        public static TextBlock Text(string value, double size)
        { return new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) }; }
        public static Button Button(string label, string help, Action action)
        {
            var b = new Button { Content = label, ToolTip = help };
            AutomationProperties.SetName(b, help);
            b.Click += delegate { action(); };
            return b;
        }
        public static TextBox Input(string help)
        { var t = new TextBox { ToolTip = help, Tag = help }; AutomationProperties.SetName(t, help); return t; }
        public static TextBlock Label(string key,double size)
        { var text=Text("",size); L.Bind(text,TextBlock.TextProperty,key); return text; }
        public static Button LocalButton(string label,string help,Action action)
        { var button=Button("","",action); L.Bind(button,ContentControl.ContentProperty,label); L.Bind(button,FrameworkElement.ToolTipProperty,help); L.Bind(button,AutomationProperties.NameProperty,help); return button; }
        public static TextBox LocalInput(string help)
        { var input=Input(""); L.Bind(input,FrameworkElement.ToolTipProperty,help); L.Bind(input,FrameworkElement.TagProperty,help); L.Bind(input,AutomationProperties.NameProperty,help); return input; }
        public static MenuItem LocalMenu(string key)
        { var item=new MenuItem(); L.Bind(item,HeaderedItemsControl.HeaderProperty,key); return item; }
        public static ScrollViewer Scroll(UIElement child)
        { return new ScrollViewer { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; }
        public static string Prompt(Window owner, string title, string initial, int maxLength=1000)
        {
            var w = new Window { Title = L.T(title), Width = 360, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, ShowInTaskbar = false, FontFamily = new FontFamily("Microsoft YaHei UI") };
            w.SetResourceReference(Window.BackgroundProperty, "SolidCard");
            w.SetResourceReference(Control.ForegroundProperty, "Ink");
            Native.StyleWindow(w);
            var body = new StackPanel { Margin = new Thickness(18) };
            body.Children.Add(Label(title, 16));
            var input = LocalInput(title); input.Text = initial; input.MaxLength = maxLength; body.Children.Add(input);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var okay = LocalButton("确定", "确定", delegate { if (String.IsNullOrWhiteSpace(input.Text)) return; w.DialogResult = true; }); okay.IsDefault = true;
            var cancel = LocalButton("取消", "取消", delegate { w.DialogResult = false; }); cancel.IsCancel = true;
            buttons.Children.Add(okay); buttons.Children.Add(cancel); body.Children.Add(buttons); w.Content = body;
            w.Loaded += delegate { input.Focus(); input.SelectAll(); };
            return w.ShowDialog() == true ? input.Text.Trim() : null;
        }
    }
}
