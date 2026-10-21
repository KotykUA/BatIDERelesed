using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace BatIDE.Services;

public class BatCompletionData(string text) : ICompletionData
{
    public IImage? Image => null;
    public string Text { get; } = text;
    public object Content => text;
    public object Description => text;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, text);
    }
}
