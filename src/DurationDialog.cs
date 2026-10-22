using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DesktopWidgets
{
    public sealed class DurationColumn : StackPanel
    {
        public readonly TextBox Input;
        public readonly Thumb Drag;
        public readonly int Maximum;
        double dragRemainder;
        public DurationColumn(string name,int maximum,int initial)
        {
            Maximum=maximum; Margin=new Thickness(5);
            Children.Add(Ui.Label(name,12)); Input=Ui.LocalInput(name); Input.Text=initial.ToString("00"); Input.FontSize=28; Input.TextAlignment=TextAlignment.Center; Children.Add(Input);
            var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center };
            buttons.Children.Add(Ui.Button("−",L.T("减少{0}",L.T(name)),delegate { Step(-1); })); buttons.Children.Add(Ui.Button("＋",L.T("增加{0}",L.T(name)),delegate { Step(1); })); Children.Add(buttons);
            Drag=new Thumb { Height=22,Cursor=Cursors.SizeNS,ToolTip=L.T("上下拖动选择{0}",L.T(name)) };
            var factory=new FrameworkElementFactory(typeof(Border)); factory.SetResourceReference(Border.BackgroundProperty,"Line"); factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));
            Drag.Template=new ControlTemplate(typeof(Thumb)) { VisualTree=factory }; Children.Add(Drag);
            Drag.DragStarted+=delegate { dragRemainder=0; };
            Drag.DragDelta+=delegate(object s,DragDeltaEventArgs e) { dragRemainder-=e.VerticalChange; int delta=(int)(dragRemainder/8); if(delta!=0) { Step(delta); dragRemainder-=delta*8; } };
            PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e) { Step(e.Delta>0?1:-1); e.Handled=true; };
            Input.PreviewKeyDown+=delegate(object s,KeyEventArgs e) { if(e.Key==Key.Up || e.Key==Key.Down) { Step(e.Key==Key.Up?1:-1); e.Handled=true; } };
            Input.LostKeyboardFocus+=delegate { int value; if(TryValue(out value)) Input.Text=value.ToString("00"); };
        }
        public bool TryValue(out int value) { return Int32.TryParse(Input.Text,NumberStyles.None,CultureInfo.InvariantCulture,out value) && value>=0 && value<=Maximum; }
        public void Step(int delta) { int value; if(!TryValue(out value)) return; Input.Text=Math.Max(0,Math.Min(Maximum,value+delta)).ToString("00"); }
    }
    public sealed class DurationDialog : Window
    {
        public readonly DurationColumn Hours,Minutes,Seconds;
        public readonly TextBlock ErrorText=Ui.Text("",12);
        public readonly Button ConfirmButton,CancelButton;
        public int Result { get; private set; }
        public DurationDialog(Window owner,int seconds)
        {
            L.Bind(this,Window.TitleProperty,"设置倒计时时长"); Owner=owner; Width=400; SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty,"SolidCard"); SetResourceReference(ForegroundProperty,"Ink"); Native.StyleWindow(this);
            var body=new StackPanel { Margin=new Thickness(18) }; body.Children.Add(Ui.Label("时 : 分 : 秒",18));
            var columns=new UniformGrid { Columns=3 }; Hours=new DurationColumn("时",99,seconds/3600); Minutes=new DurationColumn("分",59,seconds/60%60); Seconds=new DurationColumn("秒",59,seconds%60); columns.Children.Add(Hours); columns.Children.Add(Minutes); columns.Children.Add(Seconds); body.Children.Add(columns);
            foreach(var column in new[] {Hours,Minutes,Seconds}) DataObject.AddPastingHandler(column.Input,delegate(object s,DataObjectPastingEventArgs e) { var text=e.DataObject.GetData(DataFormats.UnicodeText) as string; if(text!=null && text.Contains(":")) { e.CancelCommand(); if(!Paste(text)) L.Bind(ErrorText,TextBlock.TextProperty,"请粘贴 HH:mm:ss（小时 00–99，分秒 00–59）。"); } });
            body.Children.Add(Ui.Label("可输入、滚轮、方向键或拖动下方横条。",11)); body.Children.Add(ErrorText);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0) };
            ConfirmButton=Ui.LocalButton("确定", "保存时长",delegate { if(Validate()) DialogResult=true; }); ConfirmButton.IsDefault=true;
            CancelButton=Ui.LocalButton("取消", "取消时长修改",delegate { DialogResult=false; }); CancelButton.IsCancel=true;
            actions.Children.Add(ConfirmButton); actions.Children.Add(CancelButton); body.Children.Add(actions); Content=body;
        }
        public static bool Parse(string text,out int seconds)
        {
            seconds=0; string[] parts=text.Trim().Split(':'); int h,m,s;
            if(parts.Length!=3 || parts[0].Length!=2 || parts[1].Length!=2 || parts[2].Length!=2 || !Int32.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out h) || !Int32.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out m) || !Int32.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out s) || h>99 || m>59 || s>59) return false;
            seconds=h*3600+m*60+s; return true;
        }
        public bool Paste(string text) { int value; if(!Parse(text,out value)) return false; Hours.Input.Text=(value/3600).ToString("00"); Minutes.Input.Text=(value/60%60).ToString("00"); Seconds.Input.Text=(value%60).ToString("00"); ErrorText.Text=""; return true; }
        public bool Validate()
        {
            int h,m,s; if(!Hours.TryValue(out h) || !Minutes.TryValue(out m) || !Seconds.TryValue(out s) || h+m+s==0) { L.Bind(ErrorText,TextBlock.TextProperty,"请填写有效时长（00:00:01–99:59:59）。"); return false; }
            Result=h*3600+m*60+s; ErrorText.Text=""; return true;
        }
    }
}
