using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DesktopWidgets
{
    public sealed class ShortcutManagerPanel : StackPanel
    {
        readonly WidgetApp app;
        readonly WrapPanel groups=new WrapPanel();
        readonly TextBlock summary=Ui.Text("",11);
        public readonly RadioButton[] Types=new RadioButton[3];
        public readonly Button AddButton,NewGroupButton;
        public ShortcutWidget Selected { get; private set; }
        public string SelectedType { get; private set; }
        bool refreshing;
        public ShortcutManagerPanel(WidgetApp owner)
        {
            app=owner; Margin=new Thickness(3,12,3,14); Selected=app.Shortcuts; SelectedType="software";
            var heading=new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); var title=Ui.Text("快捷入口管理",15); title.FontWeight=FontWeights.SemiBold; heading.Children.Add(title);
            NewGroupButton=Ui.Button("新建组件","新建独立的快捷入口组件",delegate { app.Manager.PromptAddShortcutGroup(); }); Grid.SetColumn(NewGroupButton,1); heading.Children.Add(NewGroupButton); Children.Add(heading);
            var help=Ui.Text("先选择目标组件，再选择要添加的类型。",11); help.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); Children.Add(help); Children.Add(groups);
            var types=new UniformGrid { Columns=3,Margin=new Thickness(0,8,0,6) }; string[] keys=UiChoices.ShortcutKinds,labels=UiChoices.ShortcutLabels;
            for(int i=0;i<3;i++) { string key=keys[i]; var button=new RadioButton { Content=labels[i],GroupName="add-shortcut-type",Style=(Style)app.Resources["ThemeTab"] }; button.Checked+=delegate { if(!refreshing) SelectedType=key; }; Types[i]=button; types.Children.Add(button); } Children.Add(types);
            var bottom=new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); summary.VerticalAlignment=VerticalAlignment.Center; summary.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); bottom.Children.Add(summary); AddButton=Ui.Button("选择并添加…","为所选组件添加快捷方式",delegate { if(Selected!=null) Selected.ChooseTarget(SelectedType,app.Manager); }); Grid.SetColumn(AddButton,1); bottom.Children.Add(AddButton); Children.Add(bottom); Refresh();
        }
        public void Select(ShortcutWidget group) { Selected=group; Refresh(); }
        public void Refresh()
        {
            refreshing=true;
            try
            {
                if(Selected==null || !app.Widgets.Contains(Selected)) Selected=app.Shortcuts;
                groups.Children.Clear(); foreach(var widget in app.Widgets.OfType<ShortcutWidget>()) { var group=widget; var radio=new RadioButton { Content=new TextBlock { Text=group.DisplayName,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=150 },ToolTip=group.DisplayName,GroupName="shortcut-component",Style=(Style)app.Resources["ThemeTab"],Padding=new Thickness(10,6,10,6),IsChecked=Selected==group }; radio.Checked+=delegate { if(!refreshing) Select(group); }; groups.Children.Add(radio); }
                string[] keys=UiChoices.ShortcutKinds; for(int i=0;i<3;i++) Types[i].IsChecked=SelectedType==keys[i]; summary.Text=Selected.DisplayName+" · "+Selected.Items.Count+" 个快捷方式";
            }
            finally { refreshing=false; }
        }
    }
}
