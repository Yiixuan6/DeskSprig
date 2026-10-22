using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopWidgets
{
    // Native folder dialog is activated only when requested; no WinForms dependency.
    public static class FolderPicker
    {
        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")] class FileOpenDialog { }
        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr types); void SetFileTypeIndex(uint index); void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie); void Unadvise(uint cookie); void SetOptions(uint options); void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder); void SetFolder(IShellItem folder); void GetFolder(out IShellItem folder); void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name); void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title); void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text); void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetResult(out IShellItem item); void AddPlace(IShellItem item, uint placement); void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result); void SetClientGuid(ref Guid guid); void ClearClientData(); void SetFilter(IntPtr filter);
        }
        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr bind, ref Guid handler, ref Guid id, out IntPtr value); void GetParent(out IShellItem parent);
            void GetDisplayName(uint type, out IntPtr text); void GetAttributes(uint mask, out uint attributes); void Compare(IShellItem other, uint hint, out int order);
        }
        public static string Choose(Window owner)
        {
            var dialog = (IFileDialog)new FileOpenDialog(); IShellItem item = null;
            try
            {
                uint options; dialog.GetOptions(out options); dialog.SetOptions(options | 0x20 | 0x40 | 0x800 | 8); dialog.SetTitle("选择快捷入口文件夹");
                int hr = dialog.Show(new WindowInteropHelper(owner).Handle);
                if (hr == unchecked((int)0x800704c7)) return null;
                Marshal.ThrowExceptionForHR(hr); dialog.GetResult(out item);
                IntPtr path; item.GetDisplayName(0x80058000, out path);
                try { return Marshal.PtrToStringUni(path); } finally { Marshal.FreeCoTaskMem(path); }
            }
            finally { if (item != null) Marshal.ReleaseComObject(item); Marshal.ReleaseComObject(dialog); }
        }
    }
}
