using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BatIDE.Services;

namespace BatIDE.Views;

public partial class EditorWindow : Window
{
    private static readonly FilePickerFileType BatFilesType = new("BAT files") { Patterns = ["*.bat"] };
    private static readonly FilePickerFileType ShFilesType = new("Shell scripts") { Patterns = ["*.sh"] };
    private static readonly FilePickerFileType ScriptsType = new("Scripts") { Patterns = ["*.bat", "*.cmd", "*.sh"] };

    private readonly SettingsService _settings;
    private readonly TerminalService _terminalService;
    private readonly List<EditorView> _views = [];

    public EditorWindow() : this(new SettingsService()) { }

    public EditorWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        _terminalService = new TerminalService();
        _terminalService.OnOutput += text => OutputText.Text += text;
        _terminalService.OnStarted += () =>
        {
            RunButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        };
        _terminalService.OnExit += _ =>
        {
            RunButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        };

        Tabs.SelectionChanged += (_, _) => UpdateProjectInfo();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Flush pending debounced writes and dispose each editor view
        // (which owns a BatDocumentService with a CancellationTokenSource).
        foreach (var view in _views)
        {
            view.Dispose();
        }
        _views.Clear();

        _settings.Flush();
        base.OnClosed(e);
    }

    public async Task OpenFileInTabAsync(string path)
    {
        var existing = _views.FirstOrDefault(v => v.FilePath == path);
        if (existing != null)
        {
            Tabs.SelectedItem = existing.Parent as TabItem;
            return;
        }

        var view = new EditorView(_settings);
        view.TitleChanged += () => UpdateTabHeader(view);
        view.StatusChanged += msg => StatusText.Text = msg;

        var tab = new TabItem { Content = view, Header = Path.GetFileName(path) };
        _views.Add(view);
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;

        await view.LoadFileAsync(path);
        UpdateProjectInfo();
    }

    private void UpdateTabHeader(EditorView view)
    {
        var tab = Tabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => ReferenceEquals(t.Content, view));

        if (tab != null)
            tab.Header = (view.HasUnsavedChanges ? "* " : "") + view.DisplayName;

        UpdateProjectInfo();
    }

    private void UpdateProjectInfo()
    {
        ProjectInfoText.Text = Tabs.SelectedItem is TabItem { Content: EditorView view }
            ? view.FilePath
            : "No file open";
    }

    private EditorView? GetActiveView() => (Tabs.SelectedItem as TabItem)?.Content as EditorView;

    private void OnSaveClick(object? sender, RoutedEventArgs e) => GetActiveView()?.Save();

    private void OnFindClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Find in Tabs",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#0d0d0d"))
        };

        var textBox = new TextBox { Watermark = "Search term..." };
        var btn = new Button
        {
            Content = "Find",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Width = 80
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10,
            Children = { textBox, btn }
        };

        btn.Click += (_, _) =>
        {
            var term = textBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            var matches = _views
                .Select(v => v.GetEditor().Document?.Text)
                .Where(text => !string.IsNullOrEmpty(text))
                .Sum(text => CountOccurrences(text!, term));

            StatusText.Text = $"Found {matches} matches for '{term}' in {_views.Count} tabs.";
            dialog.Close();
        };

        _ = dialog.ShowDialog(this);
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private void OnCommentClick(object? sender, RoutedEventArgs e) { /* TODO */ }
    private void OnUndoClick(object? sender, RoutedEventArgs e) => GetActiveView()?.GetEditor().Undo();
    private void OnRedoClick(object? sender, RoutedEventArgs e) => GetActiveView()?.GetEditor().Redo();

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        var view = GetActiveView();
        if (view == null || string.IsNullOrEmpty(view.FilePath)) return;

        view.Save();
        _ = _terminalService.RunAsync(view.FilePath);
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => _terminalService.Stop();

    private void OnClearOutputClick(object? sender, RoutedEventArgs e) => OutputText.Text = string.Empty;

    private void OnFileMenuClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var createItem = new MenuItem { Header = "Create new…" };
        var openItem = new MenuItem { Header = "Open…" };

        createItem.Click += async (_, _) => await SafeRunAsync(CreateFileAsync);
        openItem.Click += async (_, _) => await SafeRunAsync(OpenFileAsync);

        menu.Items.Add(createItem);
        menu.Items.Add(openItem);

        if (sender is Control c) menu.Open(c);
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
        await OpenFileInTabAsync(path);
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

        await OpenFileInTabAsync(files[0].Path.LocalPath);
    }

    private async Task SafeRunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
    }
}

public static class EditorViewExtensions
{
    public static AvaloniaEdit.TextEditor GetEditor(this EditorView view)
    {
        return view.FindControl<AvaloniaEdit.TextEditor>("Editor")
            ?? throw new InvalidOperationException("Editor control not found in EditorView.");
    }
}
