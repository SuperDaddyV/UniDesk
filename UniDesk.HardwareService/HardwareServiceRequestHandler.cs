using UniDesk.Hardware.Contracts;

namespace UniDesk.HardwareService;

public interface IHardwareSnapshotSource
{
    HardwareServiceSnapshotResponse GetSnapshot();
}

public sealed class HardwareServiceRequestHandler
{
    private readonly IHardwareSnapshotSource _source;

    public HardwareServiceRequestHandler(IHardwareSnapshotSource source)
    {
        _source = source;
    }

    public string Handle(string requestJson)
    {
        try
        {
            var request = HardwareIpcProtocol.DeserializeRequest(requestJson);
            if (request.ProtocolVersion != HardwareIpcProtocol.CurrentVersion)
            {
                return HardwareIpcProtocol.SerializeResponse(CreateError(
                    HardwareServiceAvailability.ProtocolMismatch,
                    $"Unsupported protocol {request.ProtocolVersion}."));
            }

            var snapshot = _source.GetSnapshot();
            if (request.Command == HardwareServiceCommand.GetStatus)
            {
                snapshot = snapshot with { Sensors = [] };
            }

            return HardwareIpcProtocol.SerializeResponse(snapshot);
        }
        catch (InvalidDataException)
        {
            return HardwareIpcProtocol.SerializeResponse(CreateError(
                HardwareServiceAvailability.ProtocolMismatch,
                "Invalid hardware service request payload."));
        }
        catch (Exception ex)
        {
            return HardwareIpcProtocol.SerializeResponse(CreateError(
                HardwareServiceAvailability.Error,
                $"{ex.GetType().Name} (0x{ex.HResult:X8})"));
        }
    }

    private static HardwareServiceSnapshotResponse CreateError(
        HardwareServiceAvailability availability,
        string error) =>
        new(
            HardwareIpcProtocol.CurrentVersion,
            availability,
            error,
            DateTimeOffset.UtcNow,
            new PawnIoStatus(false, null),
            new HardwareProviderStatus(false, true, error, null, []),
            [],
            typeof(HardwareServiceRequestHandler).Assembly.GetName().Version?.ToString(3));
}
