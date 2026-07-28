using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Hardware.Contracts;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class SensorDiagnosticReporterTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public async Task ExportDiagnosticsAsync_ShouldNotBlockCallerWhileCapturingSnapshot()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"unidesk-diagnostics-{Guid.NewGuid():N}");
        using var releaseCapture = new ManualResetEventSlim();
        var source = new BlockingDiagnosticsSource(CreateEmptySnapshot(), releaseCapture);
        var reporter = new SensorDiagnosticReporter(source, outputDirectory);
        var invocationReturned = new TaskCompletionSource<Task<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callerThread = new Thread(() =>
        {
            try
            {
                invocationReturned.SetResult(reporter.ExportDiagnosticsAsync());
            }
            catch (Exception ex)
            {
                invocationReturned.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        callerThread.Start();

        try
        {
            await source.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var exportTask = await invocationReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(exportTask.IsCompleted);

            releaseCapture.Set();
            var outputPath = await exportTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            releaseCapture.Set();
            callerThread.Join(TimeSpan.FromSeconds(5));
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildReport_ShouldIncludeDiagnosticSectionsAndRemoveSensitiveText()
    {
        var userName = Environment.UserName;
        var source = new FakeDiagnosticsSource(new HardwareMetricsDiagnosticsSnapshot(
            DateTimeOffset.Parse("2026-07-12T00:00:00Z"),
            new LibreHardwareHostDiagnosticStatus(
                false,
                false,
                $"UnauthorizedAccessException: C:\\Users\\{userName}\\secret",
                null,
                ["Cpu:Processor"]),
            new GpuEngineReaderDiagnosticStatus(false, 3, null, "host 192.168.1.8 failed", 0, null),
            [new HardwareSensorSnapshot("cpu:0", "Processor", HardwareSensorDeviceType.Cpu, "CPU Package", "Temperature", 61)],
            [new SystemMetricsSnapshot
            {
                CpuUsage = 35,
                CpuTemperature = 61,
                CpuUsageSource = "Windows Performance Counter",
                CpuTemperatureSource = "LibreHardwareMonitor CPU"
            }],
            new HardwareServiceDiagnosticStatus(
                HardwareServiceAvailability.DriverUnavailable,
                new PawnIoStatus(false, null),
                null,
                $"PawnIO missing at C:\\Users\\{userName}\\driver")));

        var report = SensorDiagnosticReporter.BuildReport(
            source.CaptureDiagnostics(),
            "2.0.0",
            "Windows Test");

        Assert.Contains("[Providers]", report, StringComparison.Ordinal);
        Assert.Contains("Schema: 2", report, StringComparison.Ordinal);
        Assert.Contains("UniDesk Hardware Service | availability=DriverUnavailable", report, StringComparison.Ordinal);
        Assert.Contains("pawnIoInstalled=False", report, StringComparison.Ordinal);
        Assert.Contains("[Sensors]", report, StringComparison.Ordinal);
        Assert.Contains("[Recent snapshots]", report, StringComparison.Ordinal);
        Assert.Contains("CPU Package", report, StringComparison.Ordinal);
        Assert.DoesNotContain(userName, report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168.1.8", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("intel-cpu.json")]
    [InlineData("amd-ryzen.json")]
    [InlineData("nvidia-gpu.json")]
    [InlineData("intel-igpu.json")]
    [InlineData("dual-gpu.json")]
    [InlineData("missing-sensors.json")]
    [InlineData("invalid-values.json")]
    [InlineData("thermal-zone.json")]
    public void SensorFixture_ShouldSelectExpectedMetrics(string fixtureName)
    {
        var fixturePath = Path.Combine(
            ProjectRoot,
            "UniDesk.Tests",
            "Fixtures",
            "HardwareSensors",
            fixtureName);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var fixture = JsonSerializer.Deserialize<SensorFixture>(File.ReadAllText(fixturePath), options)!;
        using var host = new FakeLibreHost(fixture.Sensors);
        using var cpuReader = new LibreHardwareCpuReader(
            host,
            () => fixture.ThermalZones
                .Select(item => new CpuTemperatureSensorCandidate(item.Name, item.Value))
                .ToArray());
        using var gpuReader = new LibreHardwareGpuReader(host);

        var cpu = cpuReader.Read();
        var gpu = gpuReader.Read();

        Assert.Equal(fixture.Expected.CpuUsage, cpu.CpuUsage);
        Assert.Equal(fixture.Expected.CpuTemperature, cpu.CpuTemperature);
        Assert.Equal(fixture.Expected.GpuUsage, gpu.GpuUsage);
        Assert.Equal(fixture.Expected.GpuTemperature, gpu.GpuTemperature);
    }

    private sealed record SensorFixture(
        IReadOnlyList<HardwareSensorSnapshot> Sensors,
        IReadOnlyList<ThermalZoneFixture> ThermalZones,
        ExpectedMetrics Expected);

    private sealed record ThermalZoneFixture(string Name, double? Value);

    private sealed record ExpectedMetrics(
        double? CpuUsage,
        double? CpuTemperature,
        double? GpuUsage,
        double? GpuTemperature);

    private sealed class FakeDiagnosticsSource(HardwareMetricsDiagnosticsSnapshot snapshot)
        : IHardwareMetricsDiagnosticsSource
    {
        public HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics() => snapshot;
    }

    private sealed class BlockingDiagnosticsSource(
        HardwareMetricsDiagnosticsSnapshot snapshot,
        ManualResetEventSlim releaseCapture)
        : IHardwareMetricsDiagnosticsSource
    {
        public TaskCompletionSource CaptureStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public HardwareMetricsDiagnosticsSnapshot CaptureDiagnostics()
        {
            CaptureStarted.TrySetResult();
            if (!releaseCapture.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The diagnostics capture gate was not released.");
            }

            return snapshot;
        }
    }

    private static HardwareMetricsDiagnosticsSnapshot CreateEmptySnapshot() => new(
        DateTimeOffset.UtcNow,
        new LibreHardwareHostDiagnosticStatus(false, false, null, null, []),
        null,
        [],
        []);

    private sealed class FakeLibreHost(IReadOnlyList<HardwareSensorSnapshot> sensors)
        : ILibreHardwareComputerHost
    {
        public IReadOnlyList<HardwareSensorSnapshot> CurrentSensors { get; } = sensors;
        public LibreHardwareHostDiagnosticStatus DiagnosticStatus { get; } =
            new(true, false, null, null, []);
        public void Refresh() { }
        public void Dispose() { }
    }
}
