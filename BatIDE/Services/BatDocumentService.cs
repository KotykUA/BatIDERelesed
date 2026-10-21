using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BatIDE.Models;

namespace BatIDE.Services;

public class BatDocumentService
{
    private CancellationTokenSource? _cts;
    public event Action<BatDocument>? DocumentParsed;

    // Викликається при кожній зміні тексту в редакторі
    public void TriggerParse(string text)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // Дебаунс 400мс — чекаємо, поки користувач перестане друкувати
        Task.Delay(400, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;

            var doc = BatParser.Parse(text);
            
            // Повертаємось в UI-потік для оновлення інтерфейсу
            Dispatcher.UIThread.Post(() => DocumentParsed?.Invoke(doc));
        }, TaskScheduler.Default);
    }
}