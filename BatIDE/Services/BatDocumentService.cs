using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BatIDE.Models;

namespace BatIDE.Services;

public sealed class BatDocumentService : IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<BatDocument>? DocumentParsed;

    public void TriggerParse(string text)
    {
        if (_disposed) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Delay(400, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;

            var doc = BatParser.Parse(text);
            Dispatcher.UIThread.Post(() => DocumentParsed?.Invoke(doc));
        }, token, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
