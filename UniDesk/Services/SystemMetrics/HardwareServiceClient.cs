using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Win32;
using UniDesk.Hardware.Contracts;

namespace UniDesk.Services;

public interface IHardwareServiceClient : IDisposable
{
    HardwareServiceClientResult GetSnapshot();
}

public sealed record HardwareServiceClientResult(
    HardwareServiceSnapshotResponse? Response,
    HardwareServiceAvailability Availability,
    string? Error)
{
    public static HardwareServiceClientResult Success(HardwareServiceSnapshotResponse response) =>
        new(response, response.Availability, response.Error);

    public static HardwareServiceClientResult Failure(
        HardwareServiceAvailability availability,
        string error) =>
        new(null, availability, error);
}

public sealed class NamedPipeHardwareServiceClient : IHardwareServiceClient
{
    private readonly TimeSpan _timeout;
    private bool _disposed;

    public NamedPipeHardwareServiceClient(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromMilliseconds(750);
    }

    public HardwareServiceClientResult GetSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var cancellation = new CancellationTokenSource(_timeout);
        try
        {
            return GetSnapshotAsync(cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.TimedOut,
                "Hardware service request timed out.");
        }
        catch (TimeoutException)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.TimedOut,
                "Hardware service connection timed out.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.ServiceUnavailable,
                $"Hardware service access denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            return HardwareServiceClientResult.Failure(
                GetUnavailableServiceStatus(),
                $"Hardware service unavailable: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.ProtocolMismatch,
                ex.Message);
        }
        catch (Exception ex)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.Error,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<HardwareServiceClientResult> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            HardwareIpcProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);

        await using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(false),
            bufferSize: 4096,
            leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        var request = HardwareIpcProtocol.SerializeRequest(new HardwareServiceRequest(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceCommand.GetSnapshot));
        await writer.WriteLineAsync(request.AsMemory(), cancellationToken).ConfigureAwait(false);
        var responseText = await HardwareIpcProtocol.ReadUtf8LineAsync(
            pipe,
            HardwareIpcProtocol.MaxResponseBytes,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The hardware service returned no response.");
        var response = HardwareIpcProtocol.DeserializeResponse(responseText);
        if (response.ProtocolVersion != HardwareIpcProtocol.CurrentVersion)
        {
            return HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.ProtocolMismatch,
                $"Unsupported hardware service protocol {response.ProtocolVersion}.");
        }

        return HardwareServiceClientResult.Success(response);
    }

    public void Dispose() => _disposed = true;

    private static HardwareServiceAvailability GetUnavailableServiceStatus()
    {
        try
        {
            using var serviceKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\UniDeskHardwareService",
                writable: false);
            return serviceKey == null
                ? HardwareServiceAvailability.ServiceNotInstalled
                : HardwareServiceAvailability.ServiceStopped;
        }
        catch
        {
            return HardwareServiceAvailability.ServiceUnavailable;
        }
    }
}
