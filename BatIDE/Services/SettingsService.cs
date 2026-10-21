using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using BatIDE.Models;

namespace BatIDE.Services;

/// <summary>
/// Cross-platform settings storage.
///   Windows: %LOCALAPPDATA%\BatIDE\settings.json
///   Linux:   ~/.local/share/BatIDE/settings.json
///   macOS:   ~/Library/Application Support/BatIDE/settings.json
///
/// Writes are debounced (~500 ms) — rapid <see cref="Save"/> calls coalesce into one
/// disk write. Call <see cref="Flush"/> on app shutdown to guarantee a final write.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private bool _disposed;

    public AppSettings Settings { get; }

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatIDE");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");

        Settings = Load();

        _timer = new Timer(_ => WriteToDisk(), null, Timeout.Infinite, Timeout.Infinite);
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            // Corrupted settings -> start fresh, don't crash the app.
            Debug.WriteLine($"[SettingsService] Load failed: {ex}");
        }
        return new AppSettings();
    }

    /// <summary>Schedule a debounced write. Safe to call on every UI action.</summary>
    public void Save()
    {
        if (_disposed) return;
        try { _timer.Change(500, Timeout.Infinite); }
        catch (ObjectDisposedException) { /* shutdown race */ }
    }

    /// <summary>Immediately write current settings to disk (used on shutdown).</summary>
    public void Flush()
    {
        if (_disposed) return;
        try { _timer.Change(Timeout.Infinite, Timeout.Infinite); }
        catch (ObjectDisposedException) { return; }
        WriteToDisk();
    }

    private void WriteToDisk()
    {
        if (_disposed) return;
        lock (_lock)
        {
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(Settings, JsonOptions));
            }
            catch (Exception ex)
            {
                // Best effort — ignore disk errors but leave a trace in debug output.
                Debug.WriteLine($"[SettingsService] Save failed: {ex}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _timer.Change(Timeout.Infinite, Timeout.Infinite); }
        catch { /* ignore */ }
        _timer.Dispose();

        // Final best-effort write (WriteToDisk is gated by _disposed, so do it directly).
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Final save failed: {ex}");
        }
    }
}
