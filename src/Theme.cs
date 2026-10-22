using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace DesktopWidgets
{
    public sealed class Theme : IDisposable
    {
        readonly WidgetApp app;
        readonly WindowsColors colors;
        bool queued;
        public string Status { get; private set; }
        public bool Dark { get; private set; }
        public int Revision { get; private set; }
        public bool ColorEvents { get { return colors.Listening; } }
        public Theme(WidgetApp owner)
        {
            app = owner;
            colors = new WindowsColors(QueueRefresh);
            SystemEvents.UserPreferenceChanged += PreferenceChanged;
            ApplyStyles(); Refresh();
        }
        void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e) { QueueRefresh(); }
        public void QueueRefresh()
        {
            if (app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                if (queued) return; queued = true;
                app.Dispatcher.BeginInvoke(new Action(delegate { queued = false; Refresh(); }));
            }));
        }
        static bool RegistryFlag(string name, bool fallback)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            { object value = key == null ? null : key.GetValue(name); return value == null ? fallback : Convert.ToInt32(value) != 0; }
        }
        public void Refresh()
        {
            try
            {
                colors.TryConnect();
                Dark = app.Settings.Theme == "dark" || (app.Settings.Theme == "system" && !RegistryFlag("AppsUseLightTheme", true));
                bool transparent = RegistryFlag("EnableTransparency", true);
                Color accent, variant;
                try { accent = colors.Get(5); variant = colors.Get(Dark ? 2 : 8); Status = "UISettings 强调色"; }
                catch { accent = SystemColors.HighlightColor; variant = SystemColors.WindowColor; Status = "系统窗口颜色回退"; }
                ApplyPalette(accent, variant, Dark, SystemParameters.HighContrast, transparent);
            }
            catch (Exception ex)
            {
                Status = "系统配色读取失败：" + ex.Message;
                ApplyPalette(SystemColors.HighlightColor, SystemColors.WindowColor, false, SystemParameters.HighContrast, false);
            }
        }
        public static Color Blend(Color a, Color b, double amount)
        { return Color.FromRgb((byte)(a.R * (1 - amount) + b.R * amount), (byte)(a.G * (1 - amount) + b.G * amount), (byte)(a.B * (1 - amount) + b.B * amount)); }
        static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        void SetBrush(string name, Color color)
        { var old = app.Resources[name] as SolidColorBrush; if (old == null || old.Color != color) app.Resources[name] = Brush(color); }
        public void ApplyPalette(Color accent, Color variant, bool dark, bool highContrast, bool transparency)
        {
            Color card = Blend(variant, dark ? Color.FromRgb(19,26,25) : Color.FromRgb(245,248,239), .68);
            Color ink = dark ? Color.FromRgb(239,245,240) : Color.FromRgb(19,38,31);
            Color field = Blend(card, dark ? Colors.Black : Colors.White, dark ? .16 : .43);
            Color line = Blend(card, ink, .18);
            Color button = Blend(card, accent, .08);
            Color hover = Blend(card, accent, .16);
            Color selected = Blend(accent, Colors.Black, .22);
            if (highContrast)
            {
                card = field = button = SystemColors.WindowColor;
                ink = line = SystemColors.WindowTextColor;
                hover = selected = SystemColors.HighlightColor;
                accent = SystemColors.HighlightColor;
            }
            var solidCard = card;
            if (transparency && !highContrast) card.A = (byte)Math.Round(app.Settings.Opacity * 255);
            var cardLine = line; cardLine.A = card.A;
            SetBrush("CardLine", cardLine); SetBrush("Card", card); SetBrush("SolidCard", solidCard); SetBrush("Ink", ink); SetBrush("Field", field); SetBrush("Line", line);
            SetBrush("ButtonFill", button); SetBrush("Hover", hover); SetBrush("Selected", selected); SetBrush("Accent", accent);
            SetBrush("Muted", highContrast ? ink : Blend(ink, card, .38));
            SetBrush("SelectedInk", highContrast ? SystemColors.HighlightTextColor : (selected.R*.299 + selected.G*.587 + selected.B*.114 > 145 ? Colors.Black : Colors.White));
            Revision++;
            foreach(Window window in app.Windows) Native.ApplyCaption(window);
            app.RefreshTrayTheme();
            if (app.Countdown != null) app.Countdown.Ring.InvalidateVisual();
            if (app.Status != null) app.Status.Redraw();
        }
        void ApplyStyles()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='TextBlock'><Setter Property='FontSize' Value='13'/></Style>
<Style TargetType='Button'>
 <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource ButtonFill}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/>
 <Setter Property='Padding' Value='10,7'/><Setter Property='Margin' Value='2'/><Setter Property='Cursor' Value='Hand'/><Setter Property='MinHeight' Value='30'/><Setter Property='BorderThickness' Value='1'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7'><ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers>
 <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Hover}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Trigger>
 <Trigger Property='IsPressed' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Selected}'/><Setter Property='Foreground' Value='{DynamicResource SelectedInk}'/></Trigger>
 <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderThickness' Value='2'/></Trigger>
 <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger>
 </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='IconButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='3'/><Setter Property='MinHeight' Value='24'/><Setter Property='Width' Value='26'/><Setter Property='Height' Value='26'/></Style>
<Style x:Key='TimeReadout' TargetType='Button'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='0'/><Setter Property='Margin' Value='0'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/><Border x:Name='focusMark' Width='28' Height='2' CornerRadius='1' Background='{DynamicResource Accent}' VerticalAlignment='Bottom' Margin='0,0,0,10' Visibility='Collapsed'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='.8'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focusMark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Cursor' Value='Arrow'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='TextAction' TargetType='Button'><Setter Property='Foreground' Value='{DynamicResource Muted}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='10'/><Setter Property='Padding' Value='0'/><Setter Property='Margin' Value='0'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Foreground' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='PinToggle' TargetType='CheckBox'><Setter Property='Cursor' Value='Hand'/><Setter Property='Width' Value='26'/><Setter Property='Height' Value='26'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><Border x:Name='box' Background='Transparent' CornerRadius='4'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Ink}'/><Setter TargetName='box' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'><Setter Property='Background' Value='{DynamicResource Selected}'/><Setter Property='Foreground' Value='{DynamicResource SelectedInk}'/></Style>
<Style x:Key='ThemeTab' TargetType='RadioButton'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='14,10'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border x:Name='box' CornerRadius='7' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Selected}'/><Setter Property='Foreground' Value='{DynamicResource SelectedInk}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/><Setter TargetName='box' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='VisibilitySwitch' TargetType='ToggleButton'><Setter Property='Width' Value='44'/><Setter Property='Height' Value='25'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToggleButton'><Border x:Name='rail' CornerRadius='13' Background='{DynamicResource Line}'><Ellipse x:Name='knob' Width='19' Height='19' Fill='{DynamicResource Field}' HorizontalAlignment='Left' Margin='3'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='rail' Property='Background' Value='{DynamicResource Selected}'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='rail' Property='BorderBrush' Value='{DynamicResource Ink}'/><Setter TargetName='rail' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='CaretBrush' Value='{DynamicResource Ink}'/><Setter Property='SelectionBrush' Value='{DynamicResource Accent}'/><Setter Property='Padding' Value='9'/><Setter Property='FontSize' Value='13'/><Setter Property='MinHeight' Value='32'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8'><Grid><TextBlock x:Name='hint' Text='{TemplateBinding Tag}' Foreground='{DynamicResource Muted}' Margin='{TemplateBinding Padding}' IsHitTestVisible='False' Visibility='Collapsed' TextWrapping='Wrap'/><ScrollViewer x:Name='PART_ContentHost'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='Text' Value=''><Setter TargetName='hint' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Padding' Value='3'/></Style>
<Style TargetType='Slider'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid MinHeight='24'><Border Height='4' Background='{DynamicResource Line}' CornerRadius='2' VerticalAlignment='Center'/><Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Opacity='0'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='19' Height='19'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource Field}' BorderBrush='{DynamicResource Accent}' BorderThickness='1' CornerRadius='10'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Opacity='0'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ProgressBar'><Setter Property='Foreground' Value='{DynamicResource Selected}'/><Setter Property='Background' Value='{DynamicResource Line}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Height' Value='6'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ProgressBar'><Border Background='{TemplateBinding Background}' CornerRadius='3'><Grid x:Name='PART_Track'><Border x:Name='PART_Indicator' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left' CornerRadius='3'/></Grid></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ToolTip'><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Style>
<Style TargetType='ContextMenu'><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='7' Padding='4'><ScrollViewer CanContentScroll='True' MaxHeight='700'><ItemsPresenter/></ScrollViewer></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='Transparent'/><Setter Property='Padding' Value='7,6'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Grid><Border x:Name='item' Background='{TemplateBinding Background}' CornerRadius='4' Padding='{TemplateBinding Padding}'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='23'/><ColumnDefinition/><ColumnDefinition Width='20'/></Grid.ColumnDefinitions><TextBlock x:Name='tick' Text='✓' Foreground='{TemplateBinding Foreground}' Visibility='Collapsed' VerticalAlignment='Center'/><ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/><TextBlock x:Name='arrow' Grid.Column='2' Text='›' Foreground='{TemplateBinding Foreground}' Visibility='Collapsed' HorizontalAlignment='Right'/></Grid></Border><Popup x:Name='PART_Popup' IsOpen='{Binding IsSubmenuOpen,RelativeSource={RelativeSource TemplatedParent}}' Placement='Right' AllowsTransparency='True' Focusable='False' PopupAnimation='None'><Border Background='{DynamicResource Field}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='7' Padding='4'><ScrollViewer CanContentScroll='True' MaxHeight='700'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='HasItems' Value='True'><Setter TargetName='arrow' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='{DynamicResource Selected}'/><Setter Property='Foreground' Value='{DynamicResource SelectedInk}'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='item' Property='BorderBrush' Value='{DynamicResource Accent}'/><Setter TargetName='item' Property='BorderThickness' Value='1'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='{DynamicResource Muted}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Separator'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Separator'><Border Height='1' Margin='6,5' Background='{DynamicResource Line}'/></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='9'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource Line}' CornerRadius='4'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0'/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }
        public void Dispose() { SystemEvents.UserPreferenceChanged -= PreferenceChanged; colors.Dispose(); }
    }
}
