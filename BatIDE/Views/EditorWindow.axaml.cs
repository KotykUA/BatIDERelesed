using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using BatIDE.Models;
using BatIDE.Services;

namespace BatIDE.Views;

public partial class EditorWindow : Window
{
    private static readonly FilePickerFileType BatFilesType = new("BAT files") { Patterns = ["*.bat"] };
    private static readonly FilePickerFileType ShFilesType = new("Shell scripts") { Patterns = ["*.sh"] };
    private static readonly FilePickerFileType ScriptsType = new("Scripts") { Patterns = ["*.bat", "*.cmd", "*.sh"] };
    private static readonly FilePickerFileType ImagesType = new("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] };

    private readonly SettingsService _settings;
    private readonly List<string> _projectPaths = [];
    private readonly SearchPanel? _searchPanel;
    private readonly BatDocumentService _documentService;
    private readonly DiagnosticRenderer _diagnosticRenderer;

    private string _currentPath;
    private CompletionWindow? _completionWindow;
    private bool _allowClose;
    private bool _closeInProgress;
    private BatDocument? _currentDocument;

    private enum SaveChoice { Save, Discard, Cancel }

    public EditorWindow() : this(null, new SettingsService()) { }

    public EditorWindow(string? initialPath, SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        _currentPath = initialPath ?? string.Empty;

        // --- Частина 1: Парсер ---
        _documentService = new BatDocumentService();
        _documentService.DocumentParsed += OnDocumentParsed;
        Editor.TextChanged += (_, _) => _documentService.TriggerParse(Editor.Text ?? string.Empty);

        // --- Частина 3: Diagnostics & Squiggles ---
        _diagnosticRenderer = new DiagnosticRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
        ProblemsList.DoubleTapped += OnProblemDoubleTapped;

        // Wire up editor
        Editor.TextArea.TextEntered += OnEditorTextEntered;
        Editor.TextArea.KeyDown += OnEditorKeyDown;
        Editor.SyntaxHighlighting = BatSyntaxHighlighting.Load();
        _searchPanel = SearchPanel.Install(Editor);

        ProjectsList.SelectionChanged += OnProjectSelected;

        foreach (var p in _settings.Settings.RecentProjects)
            AddProject(p);

        if (!string.IsNullOrEmpty(_currentPath))
        {
            AddProject(_currentPath);
            var idx = _projectPaths.IndexOf(_currentPath);
            if (idx >= 0) ProjectsList.SelectedIndex = idx;
            LoadProject(_currentPath);
        }

        Closed += (_, _) => _settings.Flush();
    }

    // ---------- Unsaved-changes state ----------

    private bool HasUnsavedChanges => Editor.Document?.UndoStack.IsOriginalFile == false;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowClose || !HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        if (_closeInProgress) return;
        _closeInProgress = true;
        _ = HandleCloseAsync();
    }

    private async Task HandleCloseAsync()
    {
        try
        {
            if (await ConfirmDiscardChangesAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        catch (Exception ex) { ShowStatus($"Close failed: {ex.Message}"); }
        finally { _closeInProgress = false; }
    }

    // ---------- Public API used by MainWindow ----------

    public async Task LoadProjectFromOutsideAsync(string path)
    {
        if (path == _currentPath) return;
        if (!await ConfirmDiscardChangesAsync()) return;
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
        var limit = Math.Max(1, _settings.Settings.MaxRecentProjects);
        while (_projectPaths.Count > limit) _projectPaths.RemoveAt(0);
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
            if (!await ConfirmDiscardChangesAsync())
            {
                var revertIndex = _projectPaths.IndexOf(_currentPath);
                if (revertIndex >= 0)
                {
                    ProjectsList.SelectionChanged -= OnProjectSelected;
                    ProjectsList.SelectedIndex = revertIndex;
                    ProjectsList.SelectionChanged += OnProjectSelected;
                }
                return;
            }
            _currentPath = path;
            LoadProject(path);
        }
        catch (Exception ex) { ShowStatus($"Switch project failed: {ex.Message}"); }
    }

    private void LoadProject(string path)
    {
        try
        {
            Editor.Text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            Editor.Document?.UndoStack.ClearAll();
            _documentService.TriggerParse(Editor.Text ?? string.Empty);
        }
        catch (Exception ex) { ShowStatus($"Failed to load: {ex.Message}"); }
        ProjectInfoText.Text = $"{Path.GetFileName(path)}  —  {path}";
        Title = $"BatIDE — {Path.GetFileName(path)}";
    }

    // ---------- Save & Save As ----------

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveCurrentFile();

    private void SaveCurrentFile()
    {
        if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
        {
            ShowStatus("No file selected. Use Ctrl+Shift+S to save as...");
            return;
        }
        try
        {
            var normalized = NormalizeLineEnding(Editor.Text ?? string.Empty, _currentPath);
            File.WriteAllText(_currentPath, normalized);
            Editor.Document?.UndoStack.ClearAll();
            ShowStatus("Saved.");
        }
        catch (Exception ex) { ShowStatus($"Save failed: {ex.Message}"); }
    }

    private async Task SaveAsCurrentFileAsync()
    {
        var suggestedName = string.IsNullOrEmpty(_currentPath) ? "script.bat" : Path.GetFileName(_currentPath);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As...",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [BatFilesType, ShFilesType]
        });
        if (file is null) return;
        _currentPath = file.Path.LocalPath;
        AddProject(_currentPath);
        var idx = _projectPaths.IndexOf(_currentPath);
        if (idx >= 0) ProjectsList.SelectedIndex = idx;
        SaveCurrentFile();
        LoadProject(_currentPath);
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
        catch (Exception ex) { await ShowErrorAsync("Run failed", ex.Message); }
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
        if (sender is Control c) menu.Open(c);
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
        catch (Exception ex) { await ShowErrorAsync("Background", ex.Message); }
    }

    private void SetEditorBackground(IImageBrushSource? image)
    {
        if (image is null) { Editor.Background = Brushes.Transparent; return; }
        Editor.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill, Opacity = 0.25 };
    }

    // ---------- Documentation ----------

    private async void OnDocsClick(object? sender, RoutedEventArgs e)
    {
        try { ScriptRunner.OpenUrl("https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands"); }
        catch (Exception ex) { await ShowErrorAsync("Docs", ex.Message); }
    }

    // ---------- Find ----------

    private void OnFindClick(object? sender, RoutedEventArgs e) => _searchPanel?.Open();

    // ---------- Comment ----------

    private void OnCommentClick(object? sender, RoutedEventArgs e)
    {
        var doc = Editor.Document;
        if (doc is null) return;
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
        var trimmedFirst = lines[0].TrimStart();
        var isCommented = trimmedFirst.StartsWith("::", StringComparison.Ordinal) || trimmedFirst.StartsWith("//", StringComparison.Ordinal) || trimmedFirst.StartsWith('#');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = isCommented ? lines[i].TrimStart(':', '/', '#', ' ') : "::" + lines[i];
        }
        Editor.SelectedText = string.Join<string?>(ending, lines);
    }

    // ---------- Undo / Redo ----------

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedoClick(object? sender, RoutedEventArgs e) => Editor.Redo();

    // ---------- Autocomplete ----------

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-' or '@' or '!' or '%' or '$' or '.';

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
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1))) wordStart--;
        var length = offset - wordStart;
        if (length < 1) return;
        var prefix = doc.GetText(wordStart, length);
        if (prefix.Length < 1) return;
        var matches = BatCommands.All.Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) return;
        ShowCompletionWindow(wordStart, matches);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            e.Handled = true;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) FindReferences();
            else GoToDefinition();
            return;
        }
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        switch (e.Key)
        {
            case Key.S:
                e.Handled = true;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _ = SaveAsCurrentFileAsync();
                else SaveCurrentFile();
                break;
            case Key.Space:
                e.Handled = true;
                ShowCompletionAtCaret();
                break;
        }
    }

    private void ShowCompletionAtCaret()
    {
        _completionWindow?.Close();
        _completionWindow = null;
        var doc = Editor.Document;
        if (doc is null) return;
        var offset = Editor.CaretOffset;
        var wordStart = offset;
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1))) wordStart--;
        ShowCompletionWindow(wordStart, BatCommands.All);
    }

    private void ShowCompletionWindow(int startOffset, IEnumerable<string> items)
    {
        var window = new CompletionWindow(Editor.TextArea) { StartOffset = startOffset };
        foreach (var item in items) window.CompletionList.CompletionData.Add(new BatCompletionData(item));
        window.Closed += (_, _) => { if (ReferenceEquals(_completionWindow, window)) _completionWindow = null; };
        _completionWindow = window;
        window.Show();
    }

    // ---------- Document Parsing & Outline & Diagnostics (Частина 1 + 2 + 3) ----------

    private void OnDocumentParsed(BatDocument doc)
    {
        _currentDocument = doc;
        UpdateDocumentOutline(doc);

        // --- Частина 3: Diagnostics ---
        var diagnostics = BatDiagnosticsService.Analyze(doc);
        ProblemsList.ItemsSource = diagnostics;
        _diagnosticRenderer.SetDiagnostics(diagnostics);
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

        var labelsCount = doc.Labels.Count;
        var varsCount = doc.Variables.Count(v => v.IsDeclaration);
        var cmdsCount = doc.Commands.Count;
        var problemsCount = diagnostics.Count;

        ShowStatus($"Parsed: {labelsCount} labels | {varsCount} set vars | {cmdsCount} commands | {problemsCount} problems");
    }

    private void UpdateDocumentOutline(BatDocument doc)
    {
        var root = new TreeViewItem { Header = "📄 Document Outline", IsExpanded = true };
        var labelsNode = new TreeViewItem { Header = "🏷️ Labels", IsExpanded = true };
        foreach (var label in doc.Labels)
        {
            var item = new TreeViewItem { Header = label.Name, Tag = label.Offset };
            item.DoubleTapped += OnOutlineItemDoubleTapped;
            labelsNode.Items.Add(item);
        }
        var varsNode = new TreeViewItem { Header = "📦 Variables", IsExpanded = true };
        var uniqueVars = doc.Variables.Where(v => v.IsDeclaration).GroupBy(v => v.Name).Select(g => g.First());
        foreach (var v in uniqueVars)
        {
            var item = new TreeViewItem { Header = v.Name, Tag = Editor.Document?.GetLineByNumber(v.Line + 1)?.Offset ?? 0 };
            item.DoubleTapped += OnOutlineItemDoubleTapped;
            varsNode.Items.Add(item);
        }
        root.Items.Add(labelsNode);
        root.Items.Add(varsNode);
        DocumentOutline.ItemsSource = new[] { root };
    }

    private void OnOutlineItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TreeViewItem { Tag: int offset })
        {
            Editor.CaretOffset = offset;
            Editor.Focus();
        }
    }

    private void OnProblemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ProblemsList.SelectedItem is Diagnostic diag)
        {
            var line = Editor.Document?.GetLineByNumber(diag.Line + 1);
            if (line != null)
            {
                Editor.CaretOffset = line.Offset;
                Editor.Focus();
            }
        }
    }

    // ---------- Go to Definition & Find References (Частина 2) ----------

    private (string Name, bool IsLabel, int StartOffset)? GetSymbolUnderCaret()
    {
        var doc = Editor.Document;
        if (doc == null) return null;
        var offset = Editor.CaretOffset;
        var line = doc.GetLineByOffset(offset);
        var text = doc.GetText(line);
        var labelMatch = Regex.Match(text, @"(?:goto|call)\s+:?([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
        if (labelMatch.Success)
        {
            var labelName = labelMatch.Groups[1].Value;
            var labelStart = line.Offset + labelMatch.Groups[1].Index;
            var labelEnd = labelStart + labelName.Length;
            if (offset >= labelStart - 1 && offset <= labelEnd + 1) return (labelName, true, labelStart);
        }
        var varMatch = Regex.Match(text, @"[%!]([^%!\s]+)[%!]");
        if (varMatch.Success)
        {
            var varName = varMatch.Groups[1].Value;
            var varStart = line.Offset + varMatch.Groups[1].Index;
            var varEnd = varStart + varName.Length;
            if (offset >= varStart - 1 && offset <= varEnd + 1) return (varName, false, varStart);
        }
        return null;
    }

    private void GoToDefinition()
    {
        if (_currentDocument == null) return;
        var symbol = GetSymbolUnderCaret();
        if (symbol == null) { ShowStatus("No symbol under cursor."); return; }
        var (name, isLabel, _) = symbol.Value;
        if (isLabel)
        {
            var target = _currentDocument.Labels.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (target != null) { Editor.CaretOffset = target.Offset; ShowStatus($"Jumped to label :{name}"); }
            else ShowStatus($"Label :{name} not found in document.");
        }
        else
        {
            var target = _currentDocument.Variables.FirstOrDefault(v => v.IsDeclaration && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (target != null) { var lineOffset = Editor.Document?.GetLineByNumber(target.Line + 1)?.Offset ?? 0; Editor.CaretOffset = lineOffset; ShowStatus($"Jumped to variable {name}"); }
            else ShowStatus($"Variable {name} is not declared in this document.");
        }
    }

    private void FindReferences()
    {
        if (_currentDocument == null) return;
        var symbol = GetSymbolUnderCaret();
        if (symbol == null) return;
        var (name, isLabel, _) = symbol.Value;
        if (isLabel)
        {
            var usages = _currentDocument.Commands.Where(c => (c.Name == "GOTO" || c.Name == "CALL") && c.Arguments.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
            ShowStatus($"Found {usages.Count} references to label :{name}.");
        }
        else
        {
            var usages = _currentDocument.Variables.Where(v => !v.IsDeclaration && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            ShowStatus($"Found {usages.Count} references to variable {name}.");
        }
    }

    // ---------- Safe wrapper for async event handlers ----------

    private async Task SafeRunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { await ShowErrorAsync("Error", ex.Message); }
    }

    // ---------- Dialogs ----------

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!HasUnsavedChanges) return true;
        var choice = await AskSaveChangesAsync();
        switch (choice)
        {
            case SaveChoice.Cancel: return false;
            case SaveChoice.Save: SaveCurrentFile(); break;
        }
        return true;
    }

    private async Task<SaveChoice> AskSaveChangesAsync()
    {
        var result = SaveChoice.Cancel;
        var dialog = new Window { Title = "Unsaved changes", Width = 420, Height = 170, Background = new SolidColorBrush(Color.Parse("#0d0d0d")), WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = "You have unsaved changes. Save before continuing?", Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8,
                    Children = { MakeButton("Save", SaveChoice.Save), MakeButton("Discard", SaveChoice.Discard), MakeButton("Cancel", SaveChoice.Cancel) }
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
            Title = title, Width = 400, Height = 180, Background = new SolidColorBrush(Color.Parse("#0d0d0d")), WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20), Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Width = 80 }
                }
            }
        };
        if (dialog.Content is StackPanel sp && sp.Children[1] is Button ok) ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}

// ---------- Squiggly Renderer (Частина 3) ----------

public class DiagnosticRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private List<Diagnostic> _diagnostics = new();

    public DiagnosticRenderer(TextView textView) => _textView = textView;

    public KnownLayer Layer => KnownLayer.Background;

    public void SetDiagnostics(List<Diagnostic> diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_diagnostics.Count == 0 || !textView.VisualLinesValid) return;

        var pen = new Pen(new SolidColorBrush(Color.Parse("#d16969")), 1.5);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            foreach (var diag in _diagnostics)
            {
                var line = textView.Document.GetLineByNumber(diag.Line + 1);
                if (line == null) continue;

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new SimpleSegment(line.Offset, line.Length)))
                {
                    var start = rect.BottomLeft;
                    var end = rect.BottomRight;

                    // Малюємо хвилясту лінію внизу рядка
                    context.BeginFigure(start, false);
                    for (double x = start.X; x < end.X; x += 4)
                    {
                        var y = start.Y + (x % 8 == 0 ? 0 : 2);
                        context.LineTo(new Point(x, y));
                    }
                }
            }
        }
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
