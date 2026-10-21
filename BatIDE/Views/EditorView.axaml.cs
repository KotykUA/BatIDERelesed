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
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using BatIDE.Models;
using BatIDE.Services;

namespace BatIDE.Views;

public partial class EditorView : UserControl
{
    private static readonly FilePickerFileType BatFilesType = new("BAT files") { Patterns = ["*.bat"] };
    private static readonly FilePickerFileType ShFilesType = new("Shell scripts") { Patterns = ["*.sh"] };
    private static readonly FilePickerFileType ScriptsType = new("Scripts") { Patterns = ["*.bat", "*.cmd", "*.sh"] };

    private readonly SettingsService _settings;
    private readonly List<string> _projectPaths = [];
    private readonly SearchPanel? _searchPanel;
    private readonly BatDocumentService _documentService;
    private readonly DiagnosticRenderer _diagnosticRenderer;
    private readonly FoldingManager _foldingManager;
    private readonly BatFoldingStrategy _foldingStrategy;

    private string _currentPath = string.Empty;
    private CompletionWindow? _completionWindow;
    private BatDocument? _currentDocument;

    private enum SaveChoice { Save, Discard, Cancel }

    public string FilePath => _currentPath;
    public bool HasUnsavedChanges => Editor.Document?.UndoStack.IsOriginalFile == false;
    public string DisplayName => string.IsNullOrEmpty(_currentPath) ? "Untitled" : Path.GetFileName(_currentPath);

    public event Action? TitleChanged;
    public event Action<string>? StatusChanged;

    public EditorView() : this(new SettingsService()) { }

    public EditorView(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        _documentService = new BatDocumentService();
        _documentService.DocumentParsed += OnDocumentParsed;
        Editor.TextChanged += (_, _) => _documentService.TriggerParse(Editor.Text ?? string.Empty);

        // --- Частина 3: Diagnostics ---
        _diagnosticRenderer = new DiagnosticRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
        ProblemsList.DoubleTapped += OnProblemDoubleTapped;

        // --- Частина 5: Folding ---
        _foldingManager = FoldingManager.Install(Editor.TextArea);
        _foldingStrategy = new BatFoldingStrategy();
        Editor.TextChanged += (_, _) => UpdateFoldings();

        Editor.TextArea.TextEntered += OnEditorTextEntered;
        Editor.TextArea.KeyDown += OnEditorKeyDown;
        Editor.SyntaxHighlighting = BatSyntaxHighlighting.Load();
        _searchPanel = SearchPanel.Install(Editor);

        ProjectsList.SelectionChanged += OnProjectSelected;
        foreach (var p in _settings.Settings.RecentProjects) AddProject(p);
    }

    public void UpdateFoldings()
    {
        if (Editor.Document == null) return;
        _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
    }

    public async Task LoadFileAsync(string path)
    {
        try
        {
            _currentPath = path;
            Editor.Text = File.Exists(path) ? await File.ReadAllTextAsync(path) : string.Empty;
            Editor.Document?.UndoStack.ClearAll();
            _documentService.TriggerParse(Editor.Text ?? string.Empty);
            UpdateFoldings();
        }
        catch (Exception ex) { StatusChanged?.Invoke($"Failed to load: {ex.Message}"); }

        AddProject(path);
        TitleChanged?.Invoke();
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
        {
            StatusChanged?.Invoke("No file selected.");
            return;
        }
        try
        {
            var normalized = NormalizeLineEnding(Editor.Text ?? string.Empty, _currentPath);
            File.WriteAllText(_currentPath, normalized);
            Editor.Document?.UndoStack.ClearAll();
            StatusChanged?.Invoke("Saved.");
        }
        catch (Exception ex) { StatusChanged?.Invoke($"Save failed: {ex.Message}"); }
    }

    public async Task<bool> ConfirmCloseAsync()
    {
        if (!HasUnsavedChanges) return true;
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return true;

        var result = SaveChoice.Cancel;
        var dialog = new Window { Title = "Unsaved changes", Width = 420, Height = 170, Background = new SolidColorBrush(Color.Parse("#0d0d0d")), WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"Save changes to {DisplayName}?", Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8,
                    Children = { MakeButton("Save", SaveChoice.Save), MakeButton("Discard", SaveChoice.Discard), MakeButton("Cancel", SaveChoice.Cancel) }
                }
            }
        };
        await dialog.ShowDialog(window);

        switch (result)
        {
            case SaveChoice.Cancel: return false;
            case SaveChoice.Save: Save(); break;
        }
        return true;

        Button MakeButton(string text, SaveChoice value)
        {
            var btn = new Button { Content = text, MinWidth = 90 };
            btn.Click += (_, _) => { result = value; dialog.Close(); };
            return btn;
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
        var index = ProjectsList.SelectedIndex;
        if (index < 0 || index >= _projectPaths.Count) return;
        var path = _projectPaths[index];
        if (path == _currentPath) return;
        if (!await ConfirmCloseAsync())
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
        await LoadFileAsync(path);
    }

    // ---------- Autocomplete & Snippets ----------

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-' or '@' or '!' or '%' or '$' or '.';

    private void OnEditorTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null || e.Text.Length == 0 || _completionWindow != null) return;
        if (!IsWordChar(e.Text[0])) return;
        var offset = Editor.CaretOffset;
        if (offset <= 0) return;
        var doc = Editor.Document;
        if (doc is null) return;
        var wordStart = offset - 1;
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1))) wordStart--;
        var prefix = doc.GetText(wordStart, offset - wordStart);
        if (prefix.Length < 1) return;
        var matches = BatCommands.All.Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) return;
        ShowCompletionWindow(wordStart, matches);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // --- Частина 5: Snippets (Tab) ---
        if (e.Key == Key.Tab)
        {
            if (TryExpandSnippet())
            {
                e.Handled = true;
                return;
            }
        }

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
                Save();
                break;
            case Key.Space:
                e.Handled = true;
                ShowCompletionAtCaret();
                break;
        }
    }

    private bool TryExpandSnippet()
    {
        var doc = Editor.Document;
        if (doc == null || Editor.SelectionLength > 0) return false;

        var offset = Editor.CaretOffset;
        var wordStart = offset;
        while (wordStart > 0 && IsWordChar(doc.GetCharAt(wordStart - 1))) wordStart--;

        var prefix = doc.GetText(wordStart, offset - wordStart);
        if (!SnippetManager.Snippets.TryGetValue(prefix, out var expansion))
            return false;

        doc.Replace(wordStart, offset - wordStart, expansion);

        // Ставимо курсор всередині дужок (перший \n)
        var firstNewLine = expansion.IndexOf('\n');
        if (firstNewLine >= 0)
            Editor.CaretOffset = wordStart + firstNewLine + 1;
        else
            Editor.CaretOffset = wordStart + expansion.Length;

        return true;
    }

    private void ShowCompletionAtCaret()
    {
        _completionWindow?.Close();
        _completionWindow = null;
        var doc = Editor.Document;
        if (doc is null) return;
        var wordStart = Editor.CaretOffset;
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

    // ---------- Document Parsing ----------

    private void OnDocumentParsed(BatDocument doc)
    {
        _currentDocument = doc;
        UpdateDocumentOutline(doc);
        var diagnostics = BatDiagnosticsService.Analyze(doc);
        ProblemsList.ItemsSource = diagnostics;
        _diagnosticRenderer.SetDiagnostics(diagnostics);
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        StatusChanged?.Invoke($"Parsed: {doc.Labels.Count} labels | {diagnostics.Count} problems");
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
            if (line != null) { Editor.CaretOffset = line.Offset; Editor.Focus(); }
        }
    }

    // ---------- Go to Definition & References ----------

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
            var name = labelMatch.Groups[1].Value;
            var start = line.Offset + labelMatch.Groups[1].Index;
            if (offset >= start - 1 && offset <= start + name.Length + 1) return (name, true, start);
        }
        var varMatch = Regex.Match(text, @"[%!]([^%!\s]+)[%!]");
        if (varMatch.Success)
        {
            var name = varMatch.Groups[1].Value;
            var start = line.Offset + varMatch.Groups[1].Index;
            if (offset >= start - 1 && offset <= start + name.Length + 1) return (name, false, start);
        }
        return null;
    }

    private void GoToDefinition()
    {
        if (_currentDocument == null) return;
        var symbol = GetSymbolUnderCaret();
        if (symbol == null) { StatusChanged?.Invoke("No symbol under cursor."); return; }
        var (name, isLabel, _) = symbol.Value;
        if (isLabel)
        {
            var target = _currentDocument.Labels.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (target != null) { Editor.CaretOffset = target.Offset; StatusChanged?.Invoke($"Jumped to :{name}"); }
            else StatusChanged?.Invoke($"Label :{name} not found.");
        }
        else
        {
            var target = _currentDocument.Variables.FirstOrDefault(v => v.IsDeclaration && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (target != null) { Editor.CaretOffset = Editor.Document?.GetLineByNumber(target.Line + 1)?.Offset ?? 0; StatusChanged?.Invoke($"Jumped to {name}"); }
            else StatusChanged?.Invoke($"Variable {name} not declared.");
        }
    }

    private void FindReferences()
    {
        if (_currentDocument == null) return;
        var symbol = GetSymbolUnderCaret();
        if (symbol == null) return;
        var (name, isLabel, _) = symbol.Value;
        var count = isLabel
            ? _currentDocument.Commands.Count(c => (c.Name == "GOTO" || c.Name == "CALL") && c.Arguments.Contains(name, StringComparison.OrdinalIgnoreCase))
            : _currentDocument.Variables.Count(v => !v.IsDeclaration && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        StatusChanged?.Invoke($"Found {count} references to {(isLabel ? "label" : "variable")} {name}.");
    }
}

// ---------- Squiggly Renderer ----------

public class DiagnosticRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private List<Diagnostic> _diagnostics = new();

    public DiagnosticRenderer(TextView textView) => _textView = textView;
    public KnownLayer Layer => KnownLayer.Background;
    public void SetDiagnostics(List<Diagnostic> diagnostics) => _diagnostics = diagnostics;

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
                    context.BeginFigure(rect.BottomLeft, false);
                    for (double x = rect.BottomLeft.X; x < rect.BottomRight.X; x += 4)
                        context.LineTo(new Point(x, rect.BottomLeft.Y + (x % 8 == 0 ? 0 : 2)));
                }
            }
        }
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
