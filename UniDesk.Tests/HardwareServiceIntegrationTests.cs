using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using LibreHardwareMonitor.Hardware;
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

    [Theory]
    [InlineData(HardwareType.GpuAmd, false)]
    [InlineData(HardwareType.GpuNvidia, true)]
    [InlineData(HardwareType.GpuIntel, true)]
    [InlineData(HardwareType.Cpu, true)]
    [InlineData(HardwareType.Motherboard, true)]
    public void LibreHardwareSnapshotCollector_ShouldIsolateOnlyAmdGpuUpdate(
        HardwareType hardwareType,
        bool expected)
    {
        Assert.Equal(
            expected,
            LibreHardwareSnapshotCollector.ShouldUpdateHardware(hardwareType));
    }

    [Fact]
    public void LibreHardwareSnapshotCollector_ShouldSkipNestedAmdGpuUpdate()
    {
        var amdGpu = new FakeHardware(HardwareType.GpuAmd, "AMD Radeon");
        var intelGpu = new FakeHardware(HardwareType.GpuIntel, "Intel Graphics");
        var motherboard = new FakeHardware(
            HardwareType.Motherboard,
            "Motherboard",
            [amdGpu, intelGpu]);

        LibreHardwareSnapshotCollector.UpdateHardwareTree(motherboard);

        Assert.Equal(1, motherboard.UpdateCount);
        Assert.Equal(0, amdGpu.UpdateCount);
        Assert.Equal(1, intelGpu.UpdateCount);
    }

    [Fact]
    public void LibreHardwareSnapshotCollector_ShouldLabelIsolatedAmdGpu()
    {
        var diagnosticName = LibreHardwareSnapshotCollector.GetHardwareDiagnosticName(
            HardwareType.GpuAmd,
            HardwareDeviceType.GpuAmd,
            "AMD Radeon RX 9060 XT");

        Assert.Equal(
            "GpuAmd:AMD Radeon RX 9060 XT [LibreHardwareMonitor AMD update isolated]",
            diagnosticName);
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
        var now = DateTimeOffset.Parse("2026-07-28T00:00:05Z");
        var client = new FakeHardwareServiceClient(new HardwareServiceSnapshotResponse(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceAvailability.Available,
            null,
            DateTimeOffset.Parse("2026-07-28T00:00:00Z"),
            new PawnIoStatus(true, "2.2.0"),
            new HardwareProviderStatus(true, true, null, DateTimeOffset.Parse("2026-07-28T00:00:00Z"), ["Cpu:AMD Ryzen"]),
            [new HardwareSensorDto("/amdcpu/0", "AMD Ryzen", HardwareDeviceType.Cpu, "Core (Tctl/Tdie)", "Temperature", 52)]));
        using var host = new HardwareServiceComputerHost(client, new FixedTimeProvider(now));

        host.Refresh();

        Assert.Single(host.CurrentSensors);
        Assert.Equal(52, host.CurrentSensors[0].Value);
        Assert.True(host.DiagnosticStatus.IsInitialized);
        Assert.True(host.DiagnosticStatus.IsElevated);
        Assert.Equal(HardwareServiceAvailability.Available, host.ServiceStatus.Availability);
        Assert.Equal(HardwareIpcProtocol.CurrentVersion, host.ServiceStatus.ProtocolVersion);
        Assert.Equal(DateTimeOffset.Parse("2026-07-28T00:00:00Z"), host.ServiceStatus.LastSuccessUtc);
    }

    [Fact]
    public void ServiceHost_ShouldRejectStaleSnapshotWithoutRewritingSuccessTime()
    {
        var captured = DateTimeOffset.Parse("2026-07-28T00:00:00Z");
        var client = new FakeHardwareServiceClient(CreateAvailableResponse());
        using var host = new HardwareServiceComputerHost(
            client,
            new FixedTimeProvider(captured.Add(HardwareServiceComputerHost.MaximumSnapshotAge).AddSeconds(1)));

        host.Refresh();

        Assert.Empty(host.CurrentSensors);
        Assert.Equal(HardwareServiceAvailability.TimedOut, host.ServiceStatus.Availability);
        Assert.Null(host.ServiceStatus.LastSuccessUtc);
        Assert.Contains("stale", host.ServiceStatus.LastError, StringComparison.OrdinalIgnoreCase);
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
    public void RequestHandler_WhenSnapshotFails_ShouldNotExposeExceptionPayload()
    {
        const string secretPath = @"C:\Users\Alice\private-sensor.txt";
        var handler = new HardwareServiceRequestHandler(
            new ThrowingHardwareSnapshotSource(secretPath));
        var request = HardwareIpcProtocol.SerializeRequest(new HardwareServiceRequest(
            HardwareIpcProtocol.CurrentVersion,
            HardwareServiceCommand.GetSnapshot));

        var response = HardwareIpcProtocol.DeserializeResponse(handler.Handle(request));

        Assert.Equal(HardwareServiceAvailability.Error, response.Availability);
        Assert.Contains(nameof(InvalidOperationException), response.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(secretPath, response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HardwareServiceSources_ShouldNotPersistExceptionPayloads()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        foreach (var sourceName in new[]
                 {
                     "HardwareServiceRequestHandler.cs",
                     "HardwareServiceWorkers.cs",
                     "LibreHardwareSnapshotCollector.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(
                projectRoot,
                "UniDesk.HardwareService",
                sourceName));
            Assert.DoesNotContain("ex.Message", source, StringComparison.Ordinal);
            Assert.DoesNotContain("LogError(ex", source, StringComparison.Ordinal);
            Assert.DoesNotContain("LogCritical(ex", source, StringComparison.Ordinal);
        }
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
    public void MaintenanceService_ShouldResolveRepairHelperOnlyFromProtectedCommonProgramFiles()
    {
        var path = HardwareMonitoringMaintenanceService.GetDefaultRepairHelperPath(
            @"C:\Program Files\Common Files");

        Assert.Equal(
            @"C:\Program Files\Common Files\UniDesk\HardwareRepair\UniDesk.HardwareRepair.exe",
            path);
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
    public async Task PipeServer_ShouldContinueAcceptingAfterInvalidRequestFrames()
    {
        var source = new FakeHardwareSnapshotSource(
            CreateAvailableResponse() with { ServiceVersion = "pipe-regression-test" });
        var pipeName = $"UniDesk.HardwareMetrics.Tests.{Guid.NewGuid():N}";
        var server = new HardwarePipeServer(new HardwareServiceRequestHandler(source), pipeName);
        using var shutdown = new CancellationTokenSource();
        var runTask = server.RunAsync(shutdown.Token);

        try
        {
            for (var attempt = 0; attempt < HardwarePipeServer.AcceptLoopCount; attempt++)
            {
                if (attempt % 2 == 0)
                {
                    await SendOversizedRequestFrameAsync(pipeName);
                }
                else
                {
                    await SendInvalidUtf8RequestFrameAsync(pipeName);
                }
            }

            await Task.Delay(100);
            Assert.False(runTask.IsFaulted, "An invalid request must not fault the accept loop.");

            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(3000);

            var request = HardwareIpcProtocol.SerializeRequest(new HardwareServiceRequest(
                HardwareIpcProtocol.CurrentVersion,
                HardwareServiceCommand.GetSnapshot));
            var requestBytes = Encoding.UTF8.GetBytes(request + "\n");
            await client.WriteAsync(requestBytes);
            await client.FlushAsync();

            using var responseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var responseJson = await HardwareIpcProtocol.ReadUtf8LineAsync(
                client,
                HardwareIpcProtocol.MaxResponseBytes,
                responseTimeout.Token);
            Assert.NotNull(responseJson);
            var response = HardwareIpcProtocol.DeserializeResponse(responseJson!);

            Assert.Equal(HardwareServiceAvailability.Available, response.Availability);
            Assert.Equal("pipe-regression-test", response.ServiceVersion);
            Assert.True(source.ReadCount >= 1);
        }
        finally
        {
            shutdown.Cancel();
            await runTask;
            Assert.True(runTask.IsCompletedSuccessfully);
        }
    }

    [Fact]
    public async Task MaintenanceService_WhenCancelledAfterLaunch_ShouldWaitForTerminalResult()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniDeskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var helperPath = Path.Combine(directory, "UniDesk.HardwareRepair.cmd");
        var completionMarker = Path.Combine(directory, "completed.txt");
        await File.WriteAllTextAsync(
            helperPath,
            $"@echo off\r\n%SystemRoot%\\System32\\timeout.exe /t 1 /nobreak >nul\r\necho completed>\"{completionMarker}\"\r\n");

        try
        {
            var service = new HardwareMonitoringMaintenanceService(
                new FakeDiagnosticsSource(CreateUnavailableDiagnosticsSnapshot()),
                helperPath);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var stopwatch = Stopwatch.StartNew();

            var result = await service.RepairAsync(cancellation.Token);

            Assert.Equal(HardwareRepairLaunchStatus.Succeeded, result.Status);
            Assert.Equal(0, result.ExitCode);
            Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(700));
            Assert.True(File.Exists(completionMarker));
        }
        finally
        {
            await Task.Delay(1200);
            Directory.Delete(directory, recursive: true);
        }
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

    private static HardwareMetricsDiagnosticsSnapshot CreateUnavailableDiagnosticsSnapshot() => new(
        DateTimeOffset.Parse("2026-07-28T00:00:00Z"),
        new LibreHardwareHostDiagnosticStatus(false, false, "service unavailable", null, []),
        null,
        [],
        [],
        new HardwareServiceDiagnosticStatus(
            HardwareServiceAvailability.ServiceUnavailable,
            new PawnIoStatus(false, null),
            null,
            "service unavailable"));

    private static async Task SendOversizedRequestFrameAsync(string pipeName)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(3000);

        var bytes = new byte[HardwareIpcProtocol.MaxRequestBytes + 1];
        Array.Fill(bytes, (byte)'x');
        await client.WriteAsync(bytes);
        await client.FlushAsync();
    }

    private static async Task SendInvalidUtf8RequestFrameAsync(string pipeName)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(3000);

        await client.WriteAsync(new byte[] { 0xC3, 0x28, (byte)'\n' });
        await client.FlushAsync();
    }

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

    private sealed class ThrowingHardwareSnapshotSource(string message)
        : IHardwareSnapshotSource
    {
        public HardwareServiceSnapshotResponse GetSnapshot() =>
            throw new InvalidOperationException(message);
    }

    private sealed class FakeDiagnosticsSource(HardwareMetricsDiagnosticsSnapshot snapshot)
        : IHardwareMetricsDiagnosticsSource
    {
        public HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics() => snapshot;
    }

    private sealed class FakeHardware : IHardware
    {
        public FakeHardware(
            HardwareType hardwareType,
            string name,
            IHardware[]? subHardware = null)
        {
            HardwareType = hardwareType;
            Name = name;
            Identifier = new Identifier("test", name.Replace(' ', '-'));
            SubHardware = subHardware ?? [];
        }

        public int UpdateCount { get; private set; }
        public HardwareType HardwareType { get; }
        public Identifier Identifier { get; }
        public string Name { get; set; }
        public IHardware? Parent => null;
        public ISensor[] Sensors => [];
        public IHardware[] SubHardware { get; }
        public IDictionary<string, string> Properties { get; } =
            new Dictionary<string, string>();

        event SensorEventHandler IHardware.SensorAdded
        {
            add { }
            remove { }
        }

        event SensorEventHandler IHardware.SensorRemoved
        {
            add { }
            remove { }
        }

        public string GetReport() => string.Empty;

        public void Update() => UpdateCount++;

        public void Accept(IVisitor visitor) => visitor.VisitHardware(this);

        public void Traverse(IVisitor visitor)
        {
            foreach (var child in SubHardware)
            {
                child.Accept(visitor);
                child.Traverse(visitor);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
