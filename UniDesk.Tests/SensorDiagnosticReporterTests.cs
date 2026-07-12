using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class SensorDiagnosticReporterTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

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
            }]));

        var report = SensorDiagnosticReporter.BuildReport(
            source.CaptureDiagnostics(),
            "2.0.0",
            "Windows Test");

        Assert.Contains("[Providers]", report, StringComparison.Ordinal);
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
