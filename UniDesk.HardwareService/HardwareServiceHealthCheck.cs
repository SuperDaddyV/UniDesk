using System.IO.Pipes;
using System.Text;
using UniDesk.Hardware.Contracts;

namespace UniDesk.HardwareService;

public static class HardwareServiceHealthCheck
{
    public const int Success = 0;
    public const int ServiceUnavailable = 20;
    public const int ProtocolMismatch = 21;
    public const int DriverUnavailable = 22;
    public const int ProviderUnavailable = 23;

    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
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
                HardwareServiceCommand.GetStatus));
            await writer.WriteLineAsync(request.AsMemory(), cancellationToken).ConfigureAwait(false);
            var responseText = await HardwareIpcProtocol.ReadUtf8LineAsync(
                pipe,
                HardwareIpcProtocol.MaxResponseBytes,
                cancellationToken).ConfigureAwait(false);
            if (responseText == null)
            {
                return ServiceUnavailable;
            }

            return Evaluate(HardwareIpcProtocol.DeserializeResponse(responseText));
        }
        catch (OperationCanceledException)
        {
            return ServiceUnavailable;
        }
        catch
        {
            return ServiceUnavailable;
        }
    }

    public static int Evaluate(HardwareServiceSnapshotResponse response)
    {
        if (response.ProtocolVersion != HardwareIpcProtocol.CurrentVersion ||
            response.Availability == HardwareServiceAvailability.ProtocolMismatch)
        {
            return ProtocolMismatch;
        }

        if (!response.PawnIo.IsInstalled ||
            response.Availability == HardwareServiceAvailability.DriverUnavailable)
        {
            return DriverUnavailable;
        }

        if (response.Availability != HardwareServiceAvailability.Available ||
            !response.Provider.IsInitialized)
        {
            return ProviderUnavailable;
        }

        return Success;
    }
}
