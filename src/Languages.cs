using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Data;

namespace DesktopWidgets
{
    // Only application-owned UI properties are bound. User text is never a key.
    public static class L
    {
        public static readonly string[] Codes = { "system", "zh-CN", "zh-TW", "en-US" };
        public static readonly string[] NativeNames = { "跟随系统", "简体中文", "繁體中文", "English" };
        static readonly Dictionary<string, Dictionary<string, string>> catalogs = Load();
        static readonly LanguageSource source = new LanguageSource();
        public static string Code { get; private set; }
        public static string Preference { get; private set; }
        static L() { Code="zh-CN"; Preference="system"; }
        public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(Code); } }
        [DllImport("kernel32.dll")] static extern ushort GetUserDefaultUILanguage();
        static Dictionary<string, Dictionary<string, string>> Load()
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string,string>), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            foreach (string code in Codes.Skip(1))
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Languages." + code + ".json"))
                {
                    if (stream == null) throw new InvalidOperationException("Missing embedded language: " + code);
                    result.Add(code, (Dictionary<string,string>)serializer.ReadObject(stream));
                }
            return result;
        }
        public static string ResolveSystem(CultureInfo culture)
        {
            string name = culture.Name;
            if (!name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "en-US";
            return name.IndexOf("Hant",StringComparison.OrdinalIgnoreCase)>=0 || name.EndsWith("-TW",StringComparison.OrdinalIgnoreCase) || name.EndsWith("-HK",StringComparison.OrdinalIgnoreCase) || name.EndsWith("-MO",StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "zh-CN";
        }
        public static string SystemCode()
        {
            try { return ResolveSystem(CultureInfo.GetCultureInfo((int)GetUserDefaultUILanguage())); }
            catch (CultureNotFoundException) { return ResolveSystem(CultureInfo.CurrentUICulture); }
        }
        public static void Select(string preference)
        {
            Preference = Array.IndexOf(Codes, preference)>=0 ? preference : "system";
            string next=Preference=="system" ? SystemCode() : Preference;
            if(Code==next) return; Code=next; source.Invalidate();
        }
        public static string T(string key, params object[] args)
        {
            string text;
            if (!catalogs[Code].TryGetValue(key,out text) && !catalogs["en-US"].TryGetValue(key,out text)) text=key;
            return args.Length==0 ? text : String.Format(Culture,text,args);
        }
        public static void Bind(DependencyObject target, DependencyProperty property, string key, params object[] args)
        {
            var current=BindingOperations.GetBinding(target,property);
            var translation=current==null?null:current.Converter as Translation;
            if(translation!=null && translation.Matches(key,args)) return;
            BindingOperations.SetBinding(target,property,new Binding("Revision") { Source=source, Mode=BindingMode.OneWay, Converter=new Translation(key,args) });
        }
        sealed class Translation : IValueConverter
        {
            readonly string key; readonly object[] args;
            public Translation(string value,object[] values) { key=value; args=values; }
            public bool Matches(string value,object[] values) { return key==value && args.SequenceEqual(values); }
            public object Convert(object value,Type type,object parameter,CultureInfo culture) { return T(key,args); }
            public object ConvertBack(object value,Type type,object parameter,CultureInfo culture) { return Binding.DoNothing; }
        }
        sealed class LanguageSource : INotifyPropertyChanged
        {
            public int Revision { get; private set; }
            public event PropertyChangedEventHandler PropertyChanged;
            public void Invalidate() { Revision++; var handler=PropertyChanged; if(handler!=null) handler(this,new PropertyChangedEventArgs("Revision")); }
        }
    }
}
