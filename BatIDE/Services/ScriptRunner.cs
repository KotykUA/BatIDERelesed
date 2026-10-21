using System;
using System.Diagnostics;
using System.IO;

namespace BatIDE.Services;

/// <summary>
/// Legacy one-shot script launcher (kept for compatibility).
/// Prefer <see cref="TerminalService"/> which streams output in real time.
/// </summary>
public static class ScriptRunner
{
    public static void Run(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Script not found", path);

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (OperatingSystem.IsWindows())
        {
            using var proc = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        if (ext is ".bat" or ".cmd")
        {
            if (!IsWineAvailable())
            {
                throw new InvalidOperationException(
                    $"{ext} files can only run on Windows.\n\n" +
                    "Options:\n" +
                    "  • Convert to .sh and run it here.\n" +
                    "  • Install Wine (BatIDE will detect it automatically next time).\n" +
                    "  • Execute on a Windows machine.");
            }

            var psi = new ProcessStartInfo("wine") { UseShellExecute = false };
            psi.ArgumentList.Add("cmd");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(path);

            using var proc = Process.Start(psi);
            return;
        }

        var shellPsi = new ProcessStartInfo { FileName = "/bin/sh", UseShellExecute = false };
        shellPsi.ArgumentList.Add(path);

        using var shellProc = Process.Start(shellPsi);
    }

    private static bool IsWineAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("which", "wine")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            proc?.WaitForExit(1000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            if (proc is not null) return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenUrl] UseShellExecute failed for '{url}': {ex}");
        }

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
