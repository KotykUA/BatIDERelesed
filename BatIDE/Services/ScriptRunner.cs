using System;
using System.Diagnostics;
using System.IO;

namespace BatIDE.Services;

public static class ScriptRunner
{
    public static void Run(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Script not found", path);

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (OperatingSystem.IsWindows())
        {
            // UseShellExecute=true hands the file to the shell; Process is disposable.
            using var proc = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        // --- Linux / macOS ---
        if (ext is ".bat" or ".cmd")
            throw new InvalidOperationException(
                $"{ext} files can only run on Windows.\n\n" +
                "Options:\n" +
                "  • Convert to .sh and run it here.\n" +
                "  • Run via Wine:  wine cmd /c \"<file>\"\n" +
                "  • Execute on a Windows machine.");

        // .sh or no extension — try to execute with /bin/sh.
        // ArgumentList handles spaces/quotes in `path` safely.
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false
        };
        psi.ArgumentList.Add(path);

        using var shellProc = Process.Start(psi);
    }

    public static void OpenUrl(string url)
    {
        // Primary: let the OS resolve the URL (uses default browser).
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            if (proc is not null) return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenUrl] UseShellExecute failed for '{url}': {ex}");
        }

        // Fallback: explicit platform opener.
        try
        {
            var cmd = OperatingSystem.IsLinux() ? "xdg-open"
                    : OperatingSystem.IsMacOS() ? "open"
                    : null;

            if (cmd is null)
                throw new InvalidOperationException("No fallback URL opener on this platform.");

            var psi = new ProcessStartInfo(cmd) { UseShellExecute = false };
            psi.ArgumentList.Add(url);

            using var proc = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenUrl] Fallback failed for '{url}': {ex}");
            throw;
        }
    }
}
