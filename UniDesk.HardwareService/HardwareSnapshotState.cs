using UniDesk.Hardware.Contracts;

namespace UniDesk.HardwareService;

public sealed class HardwareSnapshotState : IHardwareSnapshotSource
{
    private readonly object _sync = new();
    private HardwareServiceSnapshotResponse _snapshot = new(
        HardwareIpcProtocol.CurrentVersion,
        HardwareServiceAvailability.ServiceUnavailable,
        "Hardware sensor sampling has not started.",
        DateTimeOffset.UtcNow,
        new PawnIoStatus(false, null),
        new HardwareProviderStatus(false, true, "Hardware sensor sampling has not started.", null, []),
        [],
        typeof(HardwareSnapshotState).Assembly.GetName().Version?.ToString(3));

    public HardwareServiceSnapshotResponse GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public void Update(HardwareServiceSnapshotResponse snapshot)
    {
        lock (_sync)
        {
            _snapshot = snapshot;
        }
    }
}
