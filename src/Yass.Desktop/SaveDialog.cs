using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Yass.Desktop;

/// <summary>
/// The OS save dialog, opened directly. Photino's takes a starting folder only, so it can neither
/// fill in the name nor, on Windows, select the format and add its extension to a typed name.
/// Must run on the window's thread.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a display and the platform's own dialog.")]
internal static partial class SaveDialog
{
    /// <summary><paramref name="name"/> without the extension; empty leaves the name field empty. <c>null</c> when cancelled.</summary>
    public static string? Show(IntPtr owner, string title, string folder, string name, string extension)
    {
        string fileName = name.Length == 0 ? "" : name + extension;
        string? start = Directory.Exists(folder) ? folder : null;
        if (OperatingSystem.IsWindows())
        {
            return Windows.Show(owner, title, start, fileName, extension);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Mac.Show(title, start, fileName, extension);
        }

        if (OperatingSystem.IsLinux())
        {
            return Gtk.Show(title, start, fileName, extension);
        }

        throw new PlatformNotSupportedException("No save dialog on this platform.");
    }

    private static string Label(string extension) => extension.TrimStart('.').ToUpperInvariant();

    [SupportedOSPlatform("windows")]
    private static class Windows
    {
        private const uint OverwritePrompt = 0x2;
        private const uint StrictFileTypes = 0x4;
        private const uint NoChangeDir = 0x8;
        private const uint ForceFileSystem = 0x40;
        private const uint FileSystemPath = 0x80058000;
        private const int Cancelled = unchecked((int)0x800704C7);

        public static string? Show(IntPtr owner, string title, string? folder, string fileName, string extension)
        {
            var dialog = (IFileDialog)new FileSaveDialog();
            try
            {
                dialog.SetTitle(title);
                dialog.GetOptions(out uint options);
                dialog.SetOptions(options | OverwritePrompt | StrictFileTypes | NoChangeDir | ForceFileSystem);
                if (extension.Length > 0)
                {
                    dialog.SetFileTypes(1, [new FilterSpec { Name = Label(extension), Spec = "*" + extension }]);
                    dialog.SetFileTypeIndex(1);
                    // Added to a name typed without it.
                    dialog.SetDefaultExtension(extension.TrimStart('.'));
                }

                if (folder is not null && SHCreateItemFromParsingName(folder, IntPtr.Zero, typeof(IShellItem).GUID, out IShellItem? item) == 0)
                {
                    dialog.SetFolder(item);
                    _ = Marshal.ReleaseComObject(item);
                }

                dialog.SetFileName(fileName);
                int hr = dialog.Show(owner);
                if (hr == Cancelled)
                {
                    return null;
                }

                Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out IShellItem result);
                try
                {
                    result.GetDisplayName(FileSystemPath, out IntPtr path);
                    try
                    {
                        return Marshal.PtrToStringUni(path);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(path);
                    }
                }
                finally
                {
                    _ = Marshal.ReleaseComObject(result);
                }
            }
            finally
            {
                _ = Marshal.ReleaseComObject(dialog);
            }
        }

#pragma warning disable SYSLIB1054 // LibraryImport cannot marshal a built-in COM interface.
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
#pragma warning restore SYSLIB1054

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FilterSpec
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Name;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string Spec;
        }

        [ComImport]
        [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
        [ClassInterface(ClassInterfaceType.None)]
        private class FileSaveDialog;

        // In vtable order, up to the last method called; nothing past it is needed.
        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);

            void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);

            void SetFileTypeIndex(uint index);

            void GetFileTypeIndex(out uint index);

            void Advise(IntPtr events, out uint cookie);

            void Unadvise(uint cookie);

            void SetOptions(uint options);

            void GetOptions(out uint options);

            void SetDefaultFolder(IShellItem item);

            void SetFolder(IShellItem item);

            void GetFolder(out IShellItem item);

            void GetCurrentSelection(out IShellItem item);

            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

            void GetFileName(out IntPtr name);

            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

            void GetResult(out IShellItem item);

            void AddPlace(IShellItem item, int place);

            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        }

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);

            void GetParent(out IShellItem parent);

            void GetDisplayName(uint kind, out IntPtr name);
        }
    }

    /// <summary>GTK 3, the toolkit Photino's window runs on; the native chooser goes through the desktop portal where there is one.</summary>
    [SupportedOSPlatform("linux")]
    private static partial class Gtk
    {
        private const string LibGtk = "libgtk-3.so.0";
        private const int ActionSave = 1;
        private const int ResponseAccept = -3;

        public static string? Show(string title, string? folder, string fileName, string extension)
        {
            IntPtr dialog = gtk_file_chooser_native_new(title, IntPtr.Zero, ActionSave, "_Save", "_Cancel");
            try
            {
                gtk_native_dialog_set_modal(dialog, true);
                gtk_file_chooser_set_do_overwrite_confirmation(dialog, true);
                if (folder is not null)
                {
                    _ = gtk_file_chooser_set_current_folder(dialog, folder);
                }

                if (extension.Length > 0)
                {
                    // The chooser takes ownership of the filter.
                    IntPtr filter = gtk_file_filter_new();
                    gtk_file_filter_set_name(filter, Label(extension));
                    gtk_file_filter_add_pattern(filter, "*" + extension.ToLowerInvariant());
                    gtk_file_filter_add_pattern(filter, "*" + extension.ToUpperInvariant());
                    gtk_file_chooser_add_filter(dialog, filter);
                    gtk_file_chooser_set_filter(dialog, filter);
                }

                gtk_file_chooser_set_current_name(dialog, fileName);
                if (gtk_native_dialog_run(dialog) != ResponseAccept)
                {
                    return null;
                }

                IntPtr path = gtk_file_chooser_get_filename(dialog);
                try
                {
                    return Marshal.PtrToStringUTF8(path);
                }
                finally
                {
                    g_free(path);
                }
            }
            finally
            {
                g_object_unref(dialog);
            }
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr gtk_file_chooser_native_new(string title, IntPtr parent, int action, string acceptLabel, string cancelLabel);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial void gtk_native_dialog_set_modal(IntPtr dialog, [MarshalAs(UnmanagedType.Bool)] bool modal);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial int gtk_native_dialog_run(IntPtr dialog);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial void gtk_file_chooser_set_do_overwrite_confirmation(IntPtr chooser, [MarshalAs(UnmanagedType.Bool)] bool confirm);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk, StringMarshalling = StringMarshalling.Utf8)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool gtk_file_chooser_set_current_folder(IntPtr chooser, string folder);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk, StringMarshalling = StringMarshalling.Utf8)]
        private static partial void gtk_file_chooser_set_current_name(IntPtr chooser, string name);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial IntPtr gtk_file_chooser_get_filename(IntPtr chooser);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial void gtk_file_chooser_add_filter(IntPtr chooser, IntPtr filter);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial void gtk_file_chooser_set_filter(IntPtr chooser, IntPtr filter);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk)]
        private static partial IntPtr gtk_file_filter_new();

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk, StringMarshalling = StringMarshalling.Utf8)]
        private static partial void gtk_file_filter_set_name(IntPtr filter, string name);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibGtk, StringMarshalling = StringMarshalling.Utf8)]
        private static partial void gtk_file_filter_add_pattern(IntPtr filter, string pattern);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("libgobject-2.0.so.0")]
        private static partial void g_object_unref(IntPtr instance);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("libglib-2.0.so.0")]
        private static partial void g_free(IntPtr memory);
    }

    /// <summary>NSSavePanel through the Objective-C runtime.</summary>
    [SupportedOSPlatform("macos")]
    private static partial class Mac
    {
        private const string LibObjC = "/usr/lib/libobjc.A.dylib";
        private const long ModalResponseOk = 1;

        public static string? Show(string title, string? folder, string fileName, string extension)
        {
            IntPtr panel = Send(Class("NSSavePanel"), Sel("savePanel"));
            SendVoid(panel, Sel("setTitle:"), NSString(title));
            SendBool(panel, Sel("setCanCreateDirectories:"), true);
            if (folder is not null)
            {
                SendVoid(panel, Sel("setDirectoryURL:"), Send(Class("NSURL"), Sel("fileURLWithPath:"), NSString(folder)));
            }

            if (extension.Length > 0)
            {
                // Deprecated for allowedContentTypes, which needs UTType; still honoured, and adds the extension.
                SendVoid(panel, Sel("setAllowedFileTypes:"), Send(Class("NSArray"), Sel("arrayWithObject:"), NSString(extension.TrimStart('.'))));
            }

            SendVoid(panel, Sel("setNameFieldStringValue:"), NSString(fileName));
            if (SendLong(panel, Sel("runModal")) != ModalResponseOk)
            {
                return null;
            }

            IntPtr path = Send(Send(panel, Sel("URL")), Sel("path"));
            return Marshal.PtrToStringUTF8(Send(path, Sel("UTF8String")));
        }

        private static IntPtr NSString(string value)
        {
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return Send(Class("NSString"), Sel("stringWithUTF8String:"), utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        private static IntPtr Class(string name) => objc_getClass(name);

        private static IntPtr Sel(string name) => sel_registerName(name);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr objc_getClass(string name);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr sel_registerName(string name);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static partial void SendVoid(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static partial void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool argument);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static partial long SendLong(IntPtr receiver, IntPtr selector);
    }
}
