using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BatIDE.Services;

namespace BatIDE.Views;

public partial class MainWindow : Window
{
    // Reusable file-type descriptors (avoids re-allocating arrays on every picker call).
    private static readonly FilePickerFileType BatFilesType =
        new("BAT files") { Patterns = ["*.bat"] };
    private static readonly FilePickerFileType ShFilesType =
        new("Shell scripts") { Patterns = ["*.sh"] };
    private static readonly FilePickerFileType ScriptsType =
        new("Scripts") { Patterns = ["*.bat", "*.cmd", "*.sh"] };
    private static readonly FilePickerFileType AllFilesType =
        new("All files") { Patterns = ["*"] };

    private readonly SettingsService _settings;
    private EditorWindow? _editor;

    // Parameterless ctor kept for Avalonia's XAML factory.
    public MainWindow() : this(new SettingsService()) { }

    public MainWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Persist any pending debounced writes and release the timer.
        _settings.Flush();
        _settings.Dispose();
        base.OnClosed(e);
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Create script",
                SuggestedFileName = "script.bat",
                FileTypeChoices = [BatFilesType, ShFilesType]
            });

            if (file is null) return;

            var path = file.Path.LocalPath;
            await File.WriteAllTextAsync(path, "");
            RememberProject(path);
            await OpenEditorAsync(path);
        }
        catch (Exception ex)
        {
            // Guard against unhandled exception crashing the app from async void.
            Debug.WriteLine($"[OnCreateClick] {ex}");
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open script",
                AllowMultiple = false,
                FileTypeFilter = [ScriptsType, AllFilesType]
            });

            if (files.Count == 0) return;

            var path = files[0].Path.LocalPath;
            RememberProject(path);
            await OpenEditorAsync(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OnOpenClick] {ex}");
        }
    }

    private void RememberProject(string path)
    {
        _settings.Settings.LastPath = path;
        _settings.Settings.LastName = Path.GetFileName(path);

        // Move to the end (recency order), then trim to the configured limit.
        var recent = _settings.Settings.RecentProjects;
        recent.Remove(path);
        recent.Add(path);

        var limit = Math.Max(1, _settings.Settings.MaxRecentProjects);
        while (recent.Count > limit)
            recent.RemoveAt(0);

        _settings.Save();
    }

    /// <summary>
    /// Opens (or focuses) the editor window, forwarding the new path to it if
    /// an editor is already visible. Uses <see cref="Task"/> so the caller's
    /// try/catch actually observes exceptions thrown during loading.
    /// </summary>
    private async Task OpenEditorAsync(string path)
    {
        if (_editor is { IsVisible: true })
        {
            // Forward the new path to the existing window instead of ignoring it.
            await _editor.LoadProjectFromOutsideAsync(path);
            _editor.Activate();
            return;
        }

        _editor = new EditorWindow(path, _settings);
        _editor.Closed += (_, _) => _editor = null;
        _editor.Show();
    }
}
