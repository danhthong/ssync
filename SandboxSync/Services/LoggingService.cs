using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using SandboxSync.Models;

namespace SandboxSync.Services;

/// <summary>
/// Thread-safe logging with async drain to UI-bound collection.
/// </summary>
public sealed class LoggingService : IDisposable
{
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;
    private Dispatcher? _dispatcher;
    private const int MaxEntries = 2000;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public event EventHandler<LogEntry>? EntryAdded;

    public LoggingService()
    {
        _drainTask = Task.Run(DrainLoopAsync);
    }

    public void AttachDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public void Debug(string message) => Enqueue(LogLevel.Debug, message);

    public void Info(string message) => Enqueue(LogLevel.Info, message);

    public void Warning(string message) => Enqueue(LogLevel.Warning, message);

    public void Error(string message) => Enqueue(LogLevel.Error, message);

    public void Error(Exception ex, string message) =>
        Enqueue(LogLevel.Error, $"{message}: {ex.Message}");

    private void Enqueue(LogLevel level, string message)
    {
        _queue.Enqueue(new LogEntry
        {
            Level = level,
            Message = message,
            Timestamp = DateTime.Now
        });
    }

    private async Task DrainLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                while (_queue.TryDequeue(out var entry))
                {
                    if (_dispatcher is null)
                    {
                        await Task.Delay(10, _cts.Token).ConfigureAwait(false);
                        _queue.Enqueue(entry);
                        break;
                    }

                    await _dispatcher.InvokeAsync(() =>
                    {
                        Entries.Add(entry);
                        while (Entries.Count > MaxEntries)
                        {
                            Entries.RemoveAt(0);
                        }

                        EntryAdded?.Invoke(this, entry);
                    }, DispatcherPriority.Background);
                }

                await Task.Delay(5, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Clear()
    {
        if (_dispatcher is null)
        {
            return;
        }

        _dispatcher.Invoke(() => Entries.Clear());
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _drainTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignore on shutdown
        }

        _cts.Dispose();
    }
}
