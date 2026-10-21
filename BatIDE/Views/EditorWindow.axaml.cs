using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private readonly List<EditorView> _views = new();

    public EditorWindow() : this(new SettingsService()) { }

    public EditorWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        _terminalService = new TerminalService();
        _terminalService.OnOutput += text => OutputText.Text += text;
        _terminalService.OnStarted += () => { RunButton.IsEnabled = false; StopButton.IsEnabled = true; };
        _terminalService.OnExit += _ => { RunButton.IsEnabled = true; StopButton.IsEnabled = false; };

        Tabs.SelectionChanged += (_, _) => UpdateProjectInfo();
        Closed += (_, _) => _settings.Flush();
    }

    public async Task OpenFileInTabAsync(string path)
    {
        // Якщо файл вже відкритий — перемикаємось на нього
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
        var tab = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Content == view);
        if (tab != null) tab.Header = (view.HasUnsavedChanges ? "* " : "") + view.DisplayName;
        UpdateProjectInfo();
    }

    private void UpdateProjectInfo()
    {
        if (Tabs.SelectedItem is TabItem { Content: EditorView view })
            ProjectInfoText.Text = view.FilePath;
        else
            ProjectInfoText.Text = "No file open";
    }

    private EditorView? GetActiveView() => (Tabs.SelectedItem as TabItem)?.Content as EditorView;

    private void OnSaveClick(object? sender, RoutedEventArgs e) => GetActiveView()?.Save();
    private void OnFindClick(object? sender, RoutedEventArgs e) { /* TODO */ }
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

    private async void OnFileMenuClick(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var createItem = new MenuItem { Header = "Create new…" };
        var openItem = new MenuItem { Header = "Open…" };
        createItem.Click += async (_, _) => await CreateFileAsync();
        openItem.Click += async (_, _) => await OpenFileAsync();
        menu.Items.Add(createItem);
        menu.Items.Add(openItem);
        if (sender is Control c) menu.Open(c);
    }

    private async Task CreateFileAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create script", SuggestedFileName = "script.bat",
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
            Title = "Open script", AllowMultiple = false, FileTypeFilter = [ScriptsType]
        });
        if (files.Count == 0) return;
        await OpenFileInTabAsync(files[0].Path.LocalPath);
    }
}

public static class EditorViewExtensions
{
    public static AvaloniaEdit.TextEditor GetEditor(this EditorView view)
    {
        return view.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
    }
}
