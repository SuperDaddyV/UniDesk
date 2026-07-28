using UniDesk.Hardware.Contracts;
using UniDesk.HardwareService;
using UniDesk.Services;

namespace UniDesk.Tests;

public class HardwareServiceIntegrationTests
{
    [Fact]
    public void Protocol_ShouldRoundTripKnownSnapshotRequest()
    {
        var request = new HardwareServiceRequest(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceCommand.GetSnapshot);

        var json = HardwareIpcProtocol.SerializeRequest(request);
        var parsed = HardwareIpcProtocol.DeserializeRequest(json);

        Assert.Equal(request, parsed);
    }

    [Fact]
    public void HealthCheck_ShouldRequireDriverAndInitializedProvider()
    {
        Assert.Equal(0, HardwareServiceHealthCheck.Evaluate(CreateAvailableResponse()));
        Assert.NotEqual(0, HardwareServiceHealthCheck.Evaluate(
            CreateAvailableResponse() with { PawnIo = new PawnIoStatus(false, null) }));
        Assert.NotEqual(0, HardwareServiceHealthCheck.Evaluate(
            CreateAvailableResponse() with
            {
                Provider = new HardwareProviderStatus(false, true, "failed", null, [])
            }));
    }

    [Fact]
    public void PipeServer_ShouldUseBoundedParallelAcceptLoops()
    {
        Assert.Equal(4, HardwarePipeServer.AcceptLoopCount);
    }

    [Fact]
    public void InitializationRetryPolicy_ShouldBackOffAndThenAllowRetry()
    {
        var policy = new InitializationRetryPolicy(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(20));
        var now = DateTimeOffset.Parse("2026-07-28T00:00:00Z");

        Assert.True(policy.CanAttempt(now));
        policy.RecordFailure(now);
        Assert.False(policy.CanAttempt(now.AddSeconds(4)));
        Assert.True(policy.CanAttempt(now.AddSeconds(5)));
        policy.RecordFailure(now.AddSeconds(5));
        Assert.False(policy.CanAttempt(now.AddSeconds(14)));
        Assert.True(policy.CanAttempt(now.AddSeconds(15)));
        policy.RecordSuccess();
        Assert.True(policy.CanAttempt(now.AddSeconds(15)));
    }

    [Fact]
    public void Protocol_ShouldRejectOversizedRequest()
    {
        var oversized = new string('x', HardwareIpcProtocol.MaxRequestBytes + 1);

        Assert.Throws<InvalidDataException>(() =>
            HardwareIpcProtocol.DeserializeRequest(oversized));
    }

    [Fact]
    public void Protocol_ShouldRejectUnknownCommand()
    {
        const string json = "{\"protocolVersion\":1,\"command\":999}";

        Assert.Throws<InvalidDataException>(() =>
            HardwareIpcProtocol.DeserializeRequest(json));
    }

    [Fact]
    public void ServiceHost_ShouldMapSuccessfulDetachedSnapshot()
    {
        var client = new FakeHardwareServiceClient(new HardwareServiceSnapshotResponse(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceAvailability.Available,
            null,
            DateTimeOffset.Parse("2026-07-28T00:00:00Z"),
            new PawnIoStatus(true, "2.2.0"),
            new HardwareProviderStatus(true, true, null, DateTimeOffset.Parse("2026-07-28T00:00:00Z"), ["Cpu:AMD Ryzen"]),
            [new HardwareSensorDto("/amdcpu/0", "AMD Ryzen", HardwareDeviceType.Cpu, "Core (Tctl/Tdie)", "Temperature", 52)]));
        using var host = new HardwareServiceComputerHost(client);

        host.Refresh();

        Assert.Single(host.CurrentSensors);
        Assert.Equal(52, host.CurrentSensors[0].Value);
        Assert.True(host.DiagnosticStatus.IsInitialized);
        Assert.True(host.DiagnosticStatus.IsElevated);
        Assert.Equal(HardwareServiceAvailability.Available, host.ServiceStatus.Availability);
        Assert.Equal(HardwareIpcProtocol.CurrentVersion, host.ServiceStatus.ProtocolVersion);
    }

    [Fact]
    public void ServiceHost_ShouldDegradeWithoutThrowingWhenServiceIsUnavailable()
    {
        var client = new FakeHardwareServiceClient(
            HardwareServiceClientResult.Failure(
                HardwareServiceAvailability.ServiceUnavailable,
                "service unavailable"));
        using var host = new HardwareServiceComputerHost(client);

        host.Refresh();

        Assert.Empty(host.CurrentSensors);
        Assert.False(host.DiagnosticStatus.IsInitialized);
        Assert.Equal("service unavailable", host.DiagnosticStatus.LastError);
        Assert.Equal(HardwareServiceAvailability.ServiceUnavailable, host.ServiceStatus.Availability);
    }

    [Fact]
    public void RequestHandler_ShouldRejectProtocolMismatchWithoutCallingSnapshotSource()
    {
        var source = new FakeHardwareSnapshotSource(CreateAvailableResponse());
        var handler = new HardwareServiceRequestHandler(source);
        var request = HardwareIpcProtocol.SerializeRequest(new HardwareServiceRequest(
            HardwareIpcProtocol.CurrentVersion + 1,
            HardwareServiceCommand.GetSnapshot));

        var response = HardwareIpcProtocol.DeserializeResponse(handler.Handle(request));

        Assert.Equal(HardwareServiceAvailability.ProtocolMismatch, response.Availability);
        Assert.Equal(0, source.ReadCount);
        Assert.Empty(response.Sensors);
    }

    [Fact]
    public void RequestHandler_GetStatusShouldNotExposeSensorPayload()
    {
        var source = new FakeHardwareSnapshotSource(CreateAvailableResponse());
        var handler = new HardwareServiceRequestHandler(source);
        var request = HardwareIpcProtocol.SerializeRequest(new HardwareServiceRequest(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceCommand.GetStatus));

        var response = HardwareIpcProtocol.DeserializeResponse(handler.Handle(request));

        Assert.Equal(HardwareServiceAvailability.Available, response.Availability);
        Assert.Empty(response.Sensors);
        Assert.Equal(1, source.ReadCount);
    }

    [Fact]
    public async Task MaintenanceService_ShouldReportMissingRepairHelperWithoutLaunching()
    {
        var diagnostics = new FakeDiagnosticsSource(new HardwareMetricsDiagnosticsSnapshot(
            DateTimeOffset.Parse("2026-07-28T00:00:00Z"),
            new LibreHardwareHostDiagnosticStatus(false, false, "service unavailable", null, []),
            null,
            [],
            [],
            new HardwareServiceDiagnosticStatus(
                HardwareServiceAvailability.ServiceUnavailable,
                new PawnIoStatus(false, null),
                null,
                "service unavailable")));
        var missingHelper = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "UniDesk.HardwareRepair.exe");
        var service = new HardwareMonitoringMaintenanceService(diagnostics, missingHelper);

        var status = await service.GetStatusAsync();
        var repair = await service.RepairAsync();

        Assert.Equal(HardwareServiceAvailability.ServiceUnavailable, status.Availability);
        Assert.Equal(HardwareRepairLaunchStatus.HelperMissing, repair.Status);
    }

    [Fact]
    public async Task PipeRequestReader_ShouldRejectFrameBeyondProtocolLimit()
    {
        var bytes = new byte[HardwareIpcProtocol.MaxRequestBytes + 2];
        Array.Fill(bytes, (byte)'x');
        bytes[^1] = (byte)'\n';
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HardwarePipeServer.ReadRequestAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task ProtocolReader_ShouldRejectResponseBeyondProtocolLimit()
    {
        var bytes = new byte[HardwareIpcProtocol.MaxResponseBytes + 2];
        Array.Fill(bytes, (byte)'x');
        bytes[^1] = (byte)'\n';
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HardwareIpcProtocol.ReadUtf8LineAsync(
                stream,
                HardwareIpcProtocol.MaxResponseBytes,
            CancellationToken.None));
    }

    [Fact]
    public void Protocol_ShouldRejectOversizedSerializedResponse()
    {
        var oversizedError = new string('x', HardwareIpcProtocol.MaxResponseBytes);
        var response = CreateAvailableResponse() with { Error = oversizedError };

        Assert.Throws<InvalidDataException>(() =>
            HardwareIpcProtocol.SerializeResponse(response));
    }

    private static HardwareServiceSnapshotResponse CreateAvailableResponse() => new(
        HardwareIpcProtocol.CurrentVersion,
        HardwareServiceAvailability.Available,
        null,
        DateTimeOffset.Parse("2026-07-28T00:00:00Z"),
        new PawnIoStatus(true, "2.2.0"),
        new HardwareProviderStatus(true, true, null, DateTimeOffset.Parse("2026-07-28T00:00:00Z"), ["Cpu:AMD Ryzen"]),
        [new HardwareSensorDto("/amdcpu/0", "AMD Ryzen", HardwareDeviceType.Cpu, "Core", "Temperature", 52)]);

    private sealed class FakeHardwareServiceClient : IHardwareServiceClient
    {
        private readonly HardwareServiceClientResult _result;

        public FakeHardwareServiceClient(HardwareServiceSnapshotResponse response)
            : this(HardwareServiceClientResult.Success(response))
        {
        }

        public FakeHardwareServiceClient(HardwareServiceClientResult result)
        {
            _result = result;
        }

        public HardwareServiceClientResult GetSnapshot() => _result;

        public void Dispose()
        {
        }
    }

    private sealed class FakeHardwareSnapshotSource(HardwareServiceSnapshotResponse response)
        : IHardwareSnapshotSource
    {
        public int ReadCount { get; private set; }

        public HardwareServiceSnapshotResponse GetSnapshot()
        {
            ReadCount++;
            return response;
        }
    }

    private sealed class FakeDiagnosticsSource(HardwareMetricsDiagnosticsSnapshot snapshot)
        : IHardwareMetricsDiagnosticsSource
    {
        public HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics() => snapshot;
    }
}
