using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Search;
using BatIDE.Services;

namespace BatIDE.Views;

public partial class EditorWindow : Window
{
    // Reusable file-type descriptors (avoids re-allocating arrays on every picker call).
    private static readonly FilePickerFileType BatFilesType =
        new("BAT files") { Patterns = ["*.bat"] };
    private static readonly FilePickerFileType ShFilesType =
        new("Shell scripts") { Patterns = ["*.sh"] };
    private static readonly FilePickerFileType ScriptsType =
        new("Scripts") { Patterns = ["*.bat", "*.cmd", "*.sh"] };
    private static readonly FilePickerFileType ImagesType =
        new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] };

    private readonly SettingsService _settings;
    private readonly List<string> _projectPaths = [];
    private readonly SearchPanel? _searchPanel;

    private string _currentPath;
    private CompletionWindow? _completionWindow;
    private bool _allowClose;
    private bool _closeInProgress;

    private enum SaveChoice { Save, Discard, Cancel }

    public EditorWindow() : this(null, new SettingsService()) { }

    public EditorWindow(string? initialPath, SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        _currentPath = initialPath ?? string.Empty;

        // Wire up editor
        Editor.TextArea.TextEntered += OnEditorTextEntered;
        Editor.TextArea.KeyDown += OnEditorKeyDown;
        Editor.SyntaxHighlighting = BatSyntaxHighlighting.Load();

        _searchPanel = SearchPanel.Install(Editor);

        ProjectsList.SelectionChanged += OnProjectSelected;

        // Seed from recent projects
        foreach (var p in _settings.Settings.RecentProjects)
            AddProject(p);

        if (!string.IsNullOrEmpty(_currentPath))
        {
            AddProject(_currentPath);
            var idx = _projectPaths.IndexOf(_currentPath);
            if (idx >= 0) ProjectsList.SelectedIndex = idx;
            LoadProject(_currentPath);
        }

        // Make sure the debounced settings write actually hits disk before we exit.
        Closed += (_, _) => _settings.Flush();
    }

    // ---------- Unsaved-changes state ----------

    // AvaloniaEdit exposes "is the file dirty?" via UndoStack.IsOriginalFile:
    // the stack is "original" if nothing has been recorded since the last ClearAll().
    private bool HasUnsavedChanges =>
        Editor.Document?.UndoStack.IsOriginalFile == false;

    // ---------- Close guard ----------

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        if (!HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        // Do not let the window close until the user decides.
        e.Cancel = true;

        // Guard against double-click on X / multiple OnClosing calls while a
        // dialog is already in flight.
        if (_closeInProgress) return;

        _closeInProgress = true;
        _ = HandleCloseAsync();
    }

    private async Task HandleCloseAsync()
    {
        try
        {
            var choice = await AskSaveChangesAsync();
            switch (choice)
            {
                case SaveChoice.Cancel:
                    return;
                case SaveChoice.Save:
                    SaveCurrentFile();
                    break;
            }

            _allowClose = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowStatus($"Close failed: {ex.Message}");
        }
        finally
        {
            // Allow a retry if the dialog was cancelled or failed.
            _closeInProgress = false;
        }
    }

    // ---------- Public API used by MainWindow ----------

    /// <summary>Load a new path into this window from an external caller.</summary>
    public async Task LoadProjectFromOutsideAsync(string path)
    {
        if (path == _currentPath) return;

        if (HasUnsavedChanges)
        {
            var choice = await AskSaveChangesAsync();
            switch (choice)
            {
                case SaveChoice.Cancel:
                    return;
                case SaveChoice.Save:
                    SaveCurrentFile();
                    break;
            }
        }

        AddProject(path);
        _currentPath = path;
        var idx = _projectPaths.IndexOf(path);
        if (idx >= 0) ProjectsList.SelectedIndex = idx;
        LoadProject(path);
    }

    // ---------- Project list ----------

    private void AddProject(string path)
    {
        if (_projectPaths.Contains(path)) return;
        _projectPaths.Add(path);

        // Keep the list bounded — old entries roll off the front.
        var limit = Math.Max(1, _settings.Settings.MaxRecentProjects);
        while (_projectPaths.Count > limit)
            _projectPaths.RemoveAt(0);

        // Use display names in the list but keep full paths in _projectPaths.
        ProjectsList.ItemsSource = null;
        ProjectsList.ItemsSource = _projectPaths.Select(Path.GetFileName).ToList();
    }

    private async void OnProjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            var index = ProjectsList.SelectedIndex;
            if (index < 0 || index >= _projectPaths.Count) return;

            var path = _projectPaths[index];
            if (path == _currentPath) return;

            if (HasUnsavedChanges)
            {
                var choice = await AskSaveChangesAsync();
                switch (choice)
                {
                    case SaveChoice.Cancel:
                        // Revert the ComboBox selection without re-entering this handler.
                        // Guard against -1 in case _currentPath is no longer in the list
                        // (e.g. it was trimmed by the limit above).
                        var revertIndex = _projectPaths.IndexOf(_currentPath);
                        if (revertIndex >= 0)
                        {
                            ProjectsList.SelectionChanged -= OnProjectSelected;
                            ProjectsList.SelectedIndex = revertIndex;
                            ProjectsList.SelectionChanged += OnProjectSelected;
                        }
                        return;
                    case SaveChoice.Save:
                        SaveCurrentFile();
                        break;
                }
            }

            _currentPath = path;
            LoadProject(path);
        }
        catch (Exception ex)
        {
            ShowStatus($"Switch project failed: {ex.Message}");
        }
    }

    private void LoadProject(string path)
    {
        try
        {
            Editor.Text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            Editor.Document?.UndoStack.ClearAll(); // resets IsOriginalFile to true
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load: {ex.Message}");
        }

        ProjectInfoText.Text = $"{Path.GetFileName(path)}  —  {path}";
        Title = $"BatIDE — {Path.GetFileName(path)}";
    }

    // ---------- Save ----------

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveCurrentFile();

    private void SaveCurrentFile()
    {
        if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
        {
            ShowStatus("No file selected.");
            return;
        }

        try
        {
            var normalized = NormalizeLineEnding(Editor.Text ?? string.Empty, _currentPath);
            File.WriteAllText(_currentPath, normalized);
            Editor.Document?.UndoStack.ClearAll(); // mark saved
            ShowStatus("Saved.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Save failed: {ex.Message}");
        }
    }

    private static string NormalizeLineEnding(string text, string path)
    {
        var ending = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bat" or ".cmd" => "\r\n",
            ".sh" => "\n",
            _ => Environment.NewLine
        };
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", ending);
    }

    // ---------- Run ----------

    private async void OnRunClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath)) return;

            SaveCurrentFile();
            ScriptRunner.Run(_currentPath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Run failed", ex.Message);
        }
    }

    // ---------- File menu ----------

    private void OnFileMenuClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var createItem = new MenuItem { Header = "Create new…" };
        var openItem = new MenuItem { Header = "Open…" };
        createItem.Click += async (_, _) => await SafeRunAsync(CreateFileAsync);
        openItem.Click += async (_, _) => await SafeRunAsync(OpenFileAsync);
        menu.Items.Add(createItem);
        menu.Items.Add(openItem);

        if (sender is Control c)
            menu.Open(c);
    }

    private async Task CreateFileAsync()
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
        AddProject(path);
        _currentPath = path;
        var idx = _projectPaths.IndexOf(path);
        if (idx >= 0) ProjectsList.SelectedIndex = idx;
        LoadProject(path);
    }

    private async Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open script",
            AllowMultiple = false,
            FileTypeFilter = [ScriptsType]
        });

        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        AddProject(path);
        _currentPath = path;
        var idx = _projectPaths.IndexOf(path);
        if (idx >= 0) ProjectsList.SelectedIndex = idx;
        LoadProject(path);
    }

    // ---------- Settings ----------

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var bgItem = new MenuItem { Header = "Set background image…" };
        var clearItem = new MenuItem { Header = "Clear background image" };
        bgItem.Click += async (_, _) => await SafeRunAsync(SetBackgroundAsync);
        clearItem.Click += (_, _) => SetEditorBackground(null);
        menu.Items.Add(bgItem);
        menu.Items.Add(clearItem);

        if (sender is Control c)
            menu.Open(c);
    }

    private async Task SetBackgroundAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose background image",
            AllowMultiple = false,
            FileTypeFilter = [ImagesType]
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var bitmap = new Bitmap(stream);
            SetEditorBackground(bitmap);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Background", ex.Message);
        }
    }

    // IImageBrushSource — accept a Bitmap without a CS1503 conversion error.
    private void SetEditorBackground(IImageBrushSource? image)
    {
        if (image is null)
        {
            Editor.Background = Brushes.Transparent;
            return;
        }

        Editor.Background = new ImageBrush(image)
        {
            Stretch = Stretch.UniformToFill,
            Opacity = 0.25
        };
    }

    // ---------- Documentation ----------

    private async void OnDocsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            ScriptRunner.OpenUrl("https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Docs", ex.Message);
        }
    }

    // ---------- Find ----------

    private void OnFindClick(object? sender, RoutedEventArgs e) => _searchPanel?.Open();

    // ---------- Comment ----------

    private void OnCommentClick(object? sender, RoutedEventArgs e)
    {
        var doc = Editor.Document;
        if (doc is null) return;

        // Expand the selection to full lines so we don't inject "::"
        // in the middle of a line (e.g. "echo foo" + selection "foo").
        var selStart = Editor.SelectionStart;
        var selEnd = selStart + Editor.SelectionLength;
        if (selEnd > doc.TextLength) selEnd = doc.TextLength;

        var startLine = doc.GetLineByOffset(selStart);
        var endLine = doc.GetLineByOffset(selEnd);

        Editor.Select(startLine.Offset, endLine.EndOffset - startLine.Offset);

        var selected = Editor.SelectedText;
        if (string.IsNullOrEmpty(selected)) return;

        var ending = selected.Contains("\r\n") ? "\r\n" : "\n";
        var lines = selected.Replace("\r\n", "\n").Split('\n');

        // Culture-invariant ordinal checks (Turkish-i trap, etc.).
        var trimmedFirst = lines[0].TrimStart();
        var isCommented =
            trimmedFirst.StartsWith("::", StringComparison.Ordinal) ||
            trimmedFirst.StartsWith("//", StringComparison.Ordinal) ||
            trimmedFirst.StartsWith('#');

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = isCommented
                ? lines[i].TrimStart(':', '/', '#', ' ')
                : "::" + lines[i];
        }

        // Explicit <string?> so the compiler picks a single overload (avoids CS0121).
        Editor.SelectedText = string.Join<string?>(ending, lines);
    }

    // ---------- Undo / Redo ----------

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedoClick(object? sender, RoutedEventArgs e) => Editor.Redo();

    // ---------- Autocomplete ----------

    // Characters that can appear inside a "word" for BAT purposes.
    // Includes the sigils used by BAT/SH so `@echo`, `%var%`, `!var!`, `$var` work.
    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) ||
        ch is '_' or '-' or '@' or '!' or '%' or '$' or '.';

    private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null || e.Text.Length == 0) return;
        if (_completionWindow != null) return;

        var c = e.Text[0];
        if (!IsWordChar(c)) return;

        var offset = Editor.CaretOffset;
        if (offset <= 0) return;

        var doc = Editor.Document;
        if (doc is null) return;

        var wordStart = offset - 1;
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1)))
            wordStart--;

        var length = offset - wordStart;
        if (length < 1) return;

        var prefix = doc.GetText(wordStart, length);
        if (prefix.Length < 1) return;

        var matches = BatCommands.All
            .Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0) return;

        ShowCompletionWindow(wordStart, matches);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        switch (e.Key)
        {
            case Key.S:
                e.Handled = true;
                SaveCurrentFile();
                break;
            case Key.Space:
                e.Handled = true;
                ShowCompletionAtCaret();
                break;
        }
    }

    private void ShowCompletionAtCaret()
    {
        // Close whatever popup is currently open. The Closed handler below
        // checks ReferenceEquals, so this stale close will NOT clobber the
        // new window we are about to assign to _completionWindow.
        _completionWindow?.Close();
        _completionWindow = null;

        var doc = Editor.Document;
        if (doc is null) return;

        var offset = Editor.CaretOffset;
        var wordStart = offset;
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1)))
            wordStart--;

        ShowCompletionWindow(wordStart, BatCommands.All);
    }

    /// <summary>
    /// Create, populate, wire, and show a <see cref="CompletionWindow"/>.
    /// The Closed handler is keyed on the specific instance so that a
    /// late-arriving close of a previous popup cannot null out our field.
    /// </summary>
    private void ShowCompletionWindow(int startOffset, IEnumerable<string> items)
    {
        var window = new CompletionWindow(Editor.TextArea) { StartOffset = startOffset };

        foreach (var item in items)
            window.CompletionList.CompletionData.Add(new BatCompletionData(item));

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_completionWindow, window))
                _completionWindow = null;
        };

        _completionWindow = window;
        window.Show();
    }

    // ---------- Safe wrapper for async event handlers ----------

    private async Task SafeRunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { await ShowErrorAsync("Error", ex.Message); }
    }

    // ---------- Dialogs ----------

    private async Task<SaveChoice> AskSaveChangesAsync()
    {
        var result = SaveChoice.Cancel;

        var dialog = new Window
        {
            Title = "Unsaved changes",
            Width = 420,
            Height = 170,
            Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "You have unsaved changes. Save before continuing?",
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        MakeButton("Save",    SaveChoice.Save),
                        MakeButton("Discard", SaveChoice.Discard),
                        MakeButton("Cancel",  SaveChoice.Cancel)
                    }
                }
            }
        };

        await dialog.ShowDialog(this);
        return result;

        Button MakeButton(string text, SaveChoice value)
        {
            var btn = new Button { Content = text, MinWidth = 90 };
            btn.Click += (_, _) => { result = value; dialog.Close(); };
            return btn;
        }
    }

    // ---------- Helpers ----------

    private void ShowStatus(string message) => StatusText.Text = message;

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Width = 80
                    }
                }
            }
        };

        if (dialog.Content is StackPanel sp && sp.Children[1] is Button ok)
            ok.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }
}
