using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace BatIDE.Services;

/// <summary>
/// Запускає скрипт і стрімить stdout/stderr у реальному часі.
/// Підтримує Wine на Linux/macOS для .bat/.cmd.
/// </summary>
public class TerminalService
{
    private Process? _currentProcess;
    private StringBuilder _output = new();

    public event Action<string>? OnOutput;
    public event Action<int>? OnExit;
    public event Action? OnStarted;

    public bool IsRunning => _currentProcess is { HasExited: false };

    public async Task RunAsync(string path, string? workingDir = null)
    {
        if (IsRunning) Stop();

        if (!File.Exists(path))
        {
            OnOutput?.Invoke($"[ERROR] File not found: {path}\n");
            return;
        }

        _output.Clear();
        _currentProcess = CreateProcess(path, workingDir);
        if (_currentProcess == null) return;

        _currentProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            _output.AppendLine(e.Data);
            OnOutput?.Invoke(e.Data + Environment.NewLine);
        };
        _currentProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            _output.AppendLine(e.Data);
            OnOutput?.Invoke("[ERR] " + e.Data + Environment.NewLine);
        };

        try
        {
            OnOutput?.Invoke($"[CMD] {path}\n");
            OnStarted?.Invoke();
            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            await _currentProcess.WaitForExitAsync();
            OnOutput?.Invoke($"\n[EXIT] Code {_currentProcess.ExitCode}\n");
            OnExit?.Invoke(_currentProcess.ExitCode);
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke($"[ERROR] {ex.Message}\n");
        }
        finally
        {
            _currentProcess?.Dispose();
            _currentProcess = null;
        }
    }

    public void Stop()
    {
        try
        {
            if (_currentProcess is { HasExited: false })
            {
                _currentProcess.Kill(entireProcessTree: true);
                OnOutput?.Invoke("\n[STOPPED] User terminated.\n");
            }
        }
        catch { /* ignore */ }
    }

    public string GetOutput() => _output.ToString();

    private Process? CreateProcess(string path, string? workingDir)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (OperatingSystem.IsWindows())
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? Path.GetDirectoryName(path) ?? ""
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(path);
            return new Process { StartInfo = psi };
        }

        // Linux / macOS
        if (ext is ".bat" or ".cmd")
        {
            if (IsWineAvailable())
            {
                var psi = new ProcessStartInfo("wine")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir ?? Path.GetDirectoryName(path) ?? ""
                };
                psi.ArgumentList.Add("cmd");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(path);
                return new Process { StartInfo = psi };
            }

            OnOutput?.Invoke($"[WARN] Wine not found. Cannot run {ext} on this OS.\n");
            return null;
        }

        // .sh or no extension
        var shellPsi = new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir ?? Path.GetDirectoryName(path) ?? ""
        };
        shellPsi.ArgumentList.Add(path);
        return new Process { StartInfo = shellPsi };
    }

    private static bool IsWineAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("which", "wine")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            p?.WaitForExit(1000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
