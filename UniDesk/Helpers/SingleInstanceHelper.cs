using System.IO;
using System.IO.Pipes;
using System.Text;

namespace UniDesk.Helpers;

public sealed class SingleInstanceHelper : IDisposable
{
    private const string DefaultInstanceName =
        "UniDesk.SingleInstance.6B9BD6F1-8E3A-4C5D-9F2B-1A7C8D3E5F9A";
    private const string ActivationCommand = "Activate";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly object _stateLock = new();
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceHelper(string instanceName = DefaultInstanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("实例名称不能为空。", nameof(instanceName));

        _mutexName = $"{instanceName}.Mutex";
        _pipeName = $"{instanceName}.Pipe";
    }

    public event Action? ActivationRequested;

    public bool IsFirstInstance => _ownsMutex;

    public bool TryAcquire()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_mutex != null) return _ownsMutex;

            _mutex = new Mutex(true, _mutexName, out var createdNew);
            _ownsMutex = createdNew;
            return createdNew;
        }
    }

    public void StartListening()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_ownsMutex) throw new InvalidOperationException("只有首实例可以监听激活请求。");
            if (_listenerTask is { IsCompleted: false }) return;

            _listenerCts?.Dispose();
            var cts = new CancellationTokenSource();
            _listenerCts = cts;
            _listenerTask = Task.Run(() => ListenAsync(cts.Token));
        }
    }

    public async Task<bool> SignalExistingInstanceAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(300, cancellationToken).ConfigureAwait(false);
                await using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };
                await writer.WriteLineAsync(ActivationCommand).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            if (attempt < 4)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(command, ActivationCommand, StringComparison.Ordinal))
                {
                    try
                    {
                        ActivationRequested?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "SingleInstanceHelper.ActivationRequested");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SingleInstanceHelper.Listen");
                try
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public void Release()
    {
        Mutex? mutex;
        var releaseOwnership = false;
        lock (_stateLock)
        {
            mutex = _mutex;
            _mutex = null;
            releaseOwnership = _ownsMutex;
            _ownsMutex = false;
        }

        if (mutex == null) return;
        if (releaseOwnership)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        mutex.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource? listenerCts;
        Task? listenerTask;
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            listenerCts = _listenerCts;
            listenerTask = _listenerTask;
            _listenerCts = null;
            _listenerTask = null;
        }

        listenerCts?.Cancel();
        Release();
        if (listenerTask == null)
        {
            listenerCts?.Dispose();
            return;
        }

        _ = listenerTask.ContinueWith(
            _ => listenerCts?.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
