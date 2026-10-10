using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Yass.Desktop;

/// <summary>
/// Where the desktop app's output goes when nobody is watching a console. On Windows the app is a
/// windowless executable, so a failure at startup would otherwise leave no trace at all: the
/// window just closes. There the log is a file, and a fatal error is also shown in a message box.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Process-wide console redirection and a native message box.")]
internal static partial class DesktopLog
{
    /// <summary><c>logs/yass-desktop.log</c> in the per-user <see cref="DesktopDefaults.DataDirectory"/>.</summary>
    public static string FilePath { get; } = Path.Combine(DesktopDefaults.DataDirectory(), "logs", "yass-desktop.log");

    /// <summary>On Windows, sends console output, and so the console logger, to <see cref="FilePath"/>.</summary>
    public static void RedirectWhenWindowless()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
#pragma warning disable CA2000 // The writer is the process's console from here on; it lives as long as the process.
            var writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
#pragma warning restore CA2000
        }
#pragma warning disable CA1031 // Without a log the app still runs; it only cannot say why it fails.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>Records a fatal error and, on Windows, shows it, since there is no console to read it from.</summary>
    public static void Fatal(string message)
    {
        Console.Error.WriteLine(message);
        if (OperatingSystem.IsWindows())
        {
            _ = MessageBox(IntPtr.Zero, $"{message}\n\nDetails: {FilePath}", "YASS could not start", MbIconError);
        }
    }

    private const uint MbIconError = 0x10;

    [SupportedOSPlatform("windows")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr owner, string text, string caption, uint type);
}
