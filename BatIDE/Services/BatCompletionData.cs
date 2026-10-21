using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace BatIDE.Services;

public class BatCompletionData : ICompletionData
{
    private readonly string _text;

    public BatCompletionData(string text)
    {
        _text = text;
    }

    public IImage? Image => null;
    public string Text => _text;
    public object Content => _text;
    public object Description => _text;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, _text);
    }
}
