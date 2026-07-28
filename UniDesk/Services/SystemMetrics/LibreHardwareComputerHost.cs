namespace UniDesk.Services;

public sealed record LibreHardwareHostDiagnosticStatus(
    bool IsInitialized,
    bool IsElevated,
    string? LastError,
    DateTimeOffset? LastRefreshUtc,
    IReadOnlyList<string> HardwareNames);

public interface ILibreHardwareComputerHost : IDisposable
{
    IReadOnlyList<HardwareSensorSnapshot> CurrentSensors { get; }
    LibreHardwareHostDiagnosticStatus DiagnosticStatus { get; }
    void Refresh();
}

public sealed class LibreHardwareComputerHost :
    ILibreHardwareComputerHost,
    IHardwareServiceDiagnosticsProvider
{
    private readonly HardwareServiceComputerHost _inner;

    public LibreHardwareComputerHost()
    {
        _inner = new HardwareServiceComputerHost(new NamedPipeHardwareServiceClient());
    }

    public IReadOnlyList<HardwareSensorSnapshot> CurrentSensors => _inner.CurrentSensors;

    public LibreHardwareHostDiagnosticStatus DiagnosticStatus => _inner.DiagnosticStatus;

    public HardwareServiceDiagnosticStatus ServiceStatus => _inner.ServiceStatus;

    public void Refresh() => _inner.Refresh();

    public void Dispose() => _inner.Dispose();
}
