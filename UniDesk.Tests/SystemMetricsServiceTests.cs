using UniDesk.Services;
using System.Net.NetworkInformation;
using Xunit;

namespace UniDesk.Tests;

public class SystemMetricsServiceTests
{
    [Fact]
    public void GpuMetricsReader_ShouldPreferCompleteDiscreteGpu()
    {
        using var reader = new GpuMetricsReader(
            () => new GpuMetrics(45, 61, "Discrete", 10, true),
            () => new GpuMetrics(80, 55, "Integrated", 20, false),
            () => GpuMetrics.Empty);

        var metrics = reader.Read();

        Assert.Equal("Discrete", metrics.SourceName);
        Assert.Equal(45, metrics.GpuUsage);
        Assert.Equal(61, metrics.GpuTemperature);
    }

    [Fact]
    public void GpuMetricsReader_ShouldMergePartialCandidates()
    {
        using var reader = new GpuMetricsReader(
            () => new GpuMetrics(45, null, "Usage source", 10, true),
            () => new GpuMetrics(null, 63, "Temperature source", 20, true),
            () => GpuMetrics.Empty);

        var metrics = reader.Read();

        Assert.Equal("Usage source", metrics.SourceName);
        Assert.Equal(45, metrics.GpuUsage);
        Assert.Equal(63, metrics.GpuTemperature);
    }

    [Fact]
    public void WindowsMemoryMetricsReader_CreateMetrics_ShouldNormalizeAvailableBytes()
    {
        var normal = WindowsMemoryMetricsReader.CreateMetrics(1_000, 250);
        var overReportedAvailable = WindowsMemoryMetricsReader.CreateMetrics(1_000, 1_200);

        Assert.Equal(75, normal.UsagePercent);
        Assert.Equal((ulong)750, normal.UsedBytes);
        Assert.Equal(0, overReportedAvailable.UsagePercent);
        Assert.Equal((ulong)1_000, overReportedAvailable.AvailableBytes);
    }

    [Fact]
    public void NetworkMetricsReader_ShouldClampNegativeDeltaToZero()
    {
        var samples = new Queue<NetworkSample>(
        [
            new(DateTimeOffset.UnixEpoch, 100, 100),
            new(DateTimeOffset.UnixEpoch.AddSeconds(1), 90, 120)
        ]);
        using var reader = new NetworkMetricsReader(() => samples.Dequeue());

        _ = reader.Read();
        var metrics = reader.Read();

        Assert.Equal(0, metrics.ReceivedBytesPerSecond);
        Assert.Equal(0, metrics.SentBytesPerSecond);
    }

    [Theory]
    [InlineData("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter")]
    [InlineData("Ethernet", "VMware Virtual Ethernet Adapter")]
    [InlineData("WireGuard Tunnel", "WireGuard")]
    public void NetworkMetricsReader_VirtualAdapter_ShouldBeExcluded(string name, string description)
    {
        Assert.False(NetworkMetricsReader.IsUsableAdapter(
            OperationalStatus.Up,
            NetworkInterfaceType.Ethernet,
            name,
            description));
    }

    [Fact]
    public void CpuMetricsReader_ShouldPreferAsusTemperature()
    {
        using var reader = new CpuMetricsReader(
            () => 25,
            () => 61,
            () => new CpuMetrics(45, 72));

        var metrics = reader.Read();

        Assert.Equal(25, metrics.CpuUsage);
        Assert.Equal(61, metrics.CpuTemperature);
    }

    [Fact]
    public void CpuMetricsReader_ShouldFillMissingPerformanceUsageFromLibre()
    {
        using var reader = new CpuMetricsReader(
            () => null,
            () => 61,
            () => new CpuMetrics(45, 72));

        var metrics = reader.Read();

        Assert.Equal(45, metrics.CpuUsage);
        Assert.Equal(61, metrics.CpuTemperature);
    }

    [Fact]
    public void CpuMetricsReader_InvalidAsusTemperature_ShouldFallBackToLibre()
    {
        using var reader = new CpuMetricsReader(
            () => 25,
            () => 121,
            () => new CpuMetrics(45, 72));

        var metrics = reader.Read();

        Assert.Equal(25, metrics.CpuUsage);
        Assert.Equal(72, metrics.CpuTemperature);
    }

    [Fact]
    public void Read_ShouldReturnSnapshotWithoutThrowing()
    {
        using var service = new SystemMetricsService();

        var snapshot = service.Read();

        Assert.NotNull(snapshot);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldPreferIntelPackageTemperature()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("Core #1", 54),
                new("CPU Package", 61),
                new("Core Max", 58)
            ],
            "Intel Core i9-13900");

        Assert.NotNull(selection);
        Assert.Equal("CPU Package", selection.Value.Name);
        Assert.Equal(61, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldPreferIntelIaCoresWhenPackageIsMissing()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("CPU GT Cores", 48),
                new("CPU IA Cores", 64),
                new("PCH", 44)
            ],
            "Intel Core i9-13900");

        Assert.NotNull(selection);
        Assert.Equal("CPU IA Cores", selection.Value.Name);
        Assert.Equal(64, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldPreferAmdTctlTemperature()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("CPU Package", 58),
                new("Tdie", 57),
                new("Tctl", 63)
            ],
            "AMD Ryzen 7");

        Assert.NotNull(selection);
        Assert.Equal("Tctl", selection.Value.Name);
        Assert.Equal(63, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldPreferAmdCombinedTctlTdieTemperature()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("PCH", 45),
                new("Core (Tctl/Tdie)", 62)
            ],
            "AMD Ryzen 7 9700X");

        Assert.NotNull(selection);
        Assert.Equal("Core (Tctl/Tdie)", selection.Value.Name);
        Assert.Equal(62, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuMotherboardTemperatureSensor_ShouldUseCpuNamedSensor()
    {
        var selection = SensorSelection.SelectCpuMotherboardTemperatureSensor(
            [
                new("System", 33),
                new("CPU", 47),
                new("CPU VRM", 68)
            ],
            "AMD Ryzen 7 9700X");

        Assert.NotNull(selection);
        Assert.Equal("CPU", selection.Value.Name);
        Assert.Equal(47, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuMotherboardTemperatureSensor_ShouldUsePchFallbackForRyzen9000()
    {
        var selection = SensorSelection.SelectCpuMotherboardTemperatureSensor(
            [
                new("System", 33),
                new("PCH", 52)
            ],
            "AMD Ryzen 7 9700X");

        Assert.NotNull(selection);
        Assert.Equal("PCH", selection.Value.Name);
        Assert.Equal(52, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuMotherboardTemperatureSensor_ShouldNotUsePchFallbackForUnknownCpu()
    {
        var selection = SensorSelection.SelectCpuMotherboardTemperatureSensor(
            [
                new("System", 33),
                new("PCH", 52)
            ],
            "AMD Ryzen 7 7700X");

        Assert.Null(selection);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldUseHighestCoreTemperatureWhenPackageIsMissing()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("Core #1", 49),
                new("Core #2", 56),
                new("Core #3", 52)
            ],
            "Intel Core i9-13900");

        Assert.NotNull(selection);
        Assert.Equal("Core #2", selection.Value.Name);
        Assert.Equal(56, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldIgnoreInvalidTemperatures()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("CPU Package", null),
                new("Package", double.NaN),
                new("Core #1", 0),
                new("Core #2", 121)
            ],
            "Intel Core i9-13900");

        Assert.Null(selection);
    }

    [Fact]
    public void SelectCpuTemperatureSensor_ShouldNotUsePchAsDirectCpuTemperature()
    {
        var selection = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("PCH", 52),
                new("Chipset", 49)
            ],
            "AMD Ryzen 7 9700X");

        Assert.Null(selection);
    }

    [Fact]
    public void SelectWindowsThermalZoneTemperatureSensor_ShouldUseHighestValidThermalZone()
    {
        var selection = SensorSelection.SelectWindowsThermalZoneTemperatureSensor(
            [
                new("ACPI TZ00", 41),
                new("ACPI TZ01", 58),
                new("ACPI TZ02", 0)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("ACPI TZ01", selection.Value.Name);
        Assert.Equal(58, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureProvider_ShouldPreferCpuBeforeFallbacks()
    {
        var selection = SensorSelection.SelectCpuTemperatureProvider(
            new("CPU Package", 61),
            new("CPU", 45),
            new("ACPI TZ00", 48));

        Assert.NotNull(selection);
        Assert.Equal("LibreHardwareMonitor CPU", selection.Value.Source);
        Assert.Equal("CPU Package", selection.Value.Name);
        Assert.Equal(61, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuTemperatureProvider_ShouldUseWindowsThermalZoneOnlyAsLastFallback()
    {
        var selection = SensorSelection.SelectCpuTemperatureProvider(
            null,
            null,
            new("ACPI TZ00", 48));

        Assert.NotNull(selection);
        Assert.Equal("Windows ACPI Thermal Zone", selection.Value.Source);
        Assert.Equal("ACPI TZ00", selection.Value.Name);
        Assert.Equal(48, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuUsageSensor_ShouldPreferCpuTotal()
    {
        var selection = SensorSelection.SelectCpuUsageSensor(
            [
                new("CPU Core #1", 80),
                new("CPU Total", 34),
                new("CPU Core #2", 20)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("CPU Total", selection.Value.Name);
        Assert.Equal(34, selection.Value.Value);
    }

    [Fact]
    public void SelectCpuUsageSensor_ShouldAverageCoreLoadsWhenTotalIsMissing()
    {
        var selection = SensorSelection.SelectCpuUsageSensor(
            [
                new("CPU Core #1", 30),
                new("CPU Core #2", 50)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("CPU Core Average", selection.Value.Name);
        Assert.Equal(40, selection.Value.Value);
    }

    [Fact]
    public void SelectGpuUsageSensor_ShouldPrefer3DOrGraphicsLoad()
    {
        var selection = SensorSelection.SelectGpuUsageSensor(
            [
                new("Memory Controller", 70),
                new("GPU 3D", 42),
                new("Video Decode", 12)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("GPU 3D", selection.Value.Name);
        Assert.Equal(42, selection.Value.Value);
    }

    [Fact]
    public void SelectGpuUsageSensor_ShouldSupportIntelGraphicsNames()
    {
        var selection = SensorSelection.SelectGpuUsageSensor(
            [
                new("Render", 18),
                new("Video", 9)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("Render", selection.Value.Name);
        Assert.Equal(18, selection.Value.Value);
    }

    [Fact]
    public void SelectGpuTemperatureSensor_ShouldPreferGpuCore()
    {
        var selection = SensorSelection.SelectGpuTemperatureSensor(
            [
                new("GPU Hot Spot", 82),
                new("GPU Core", 66),
                new("GPU Memory Junction", 78)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("GPU Core", selection.Value.Name);
        Assert.Equal(66, selection.Value.Value);
    }

    [Fact]
    public void SelectGpuTemperatureSensor_ShouldSupportHotSpotWhenCoreIsMissing()
    {
        var selection = SensorSelection.SelectGpuTemperatureSensor(
            [
                new("GPU Memory Junction", 78),
                new("GPU Hot Spot", 82)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("GPU Hot Spot", selection.Value.Name);
        Assert.Equal(82, selection.Value.Value);
    }

    [Fact]
    public void SensorSelection_ShouldIgnoreInvalidPercentagesAndTemperatures()
    {
        var usage = SensorSelection.SelectGpuUsageSensor(
            [
                new("GPU Core", -1),
                new("GPU 3D", 101),
                new("Graphics", double.PositiveInfinity)
            ]);
        var temperature = SensorSelection.SelectGpuTemperatureSensor(
            [
                new("GPU Core", -1),
                new("GPU Temperature", 999),
                new("Core", double.NaN)
            ]);

        Assert.Null(usage);
        Assert.Null(temperature);
    }

    [Fact]
    public void SensorSelection_ShouldAcceptBoundaryPercentagesAndTemperature()
    {
        var zeroUsage = SensorSelection.SelectCpuUsageSensor([new("CPU Total", 0)]);
        var fullUsage = SensorSelection.SelectGpuUsageSensor([new("GPU Core", 100)]);
        var maxTemperature = SensorSelection.SelectGpuTemperatureSensor([new("GPU Core", 120)]);

        Assert.Equal(0, zeroUsage?.Value);
        Assert.Equal(100, fullUsage?.Value);
        Assert.Equal(120, maxTemperature?.Value);
    }

    [Fact]
    public void SelectGpuUsageSensor_ShouldPreferSourceKeywordPriorityOverHigherValue()
    {
        var selection = SensorSelection.SelectGpuUsageSensor(
            [
                new("Overall", 95),
                new("Graphics", 80),
                new("GPU Core", 35)
            ]);

        Assert.NotNull(selection);
        Assert.Equal("GPU Core", selection.Value.Name);
        Assert.Equal(35, selection.Value.Value);
    }

    [Fact]
    public void SensorSelection_MixedCandidates_ShouldChooseOnlyValidPreferredValues()
    {
        var usage = SensorSelection.SelectCpuUsageSensor(
            [
                new("CPU Total", double.NaN),
                new("CPU Core #1", 20),
                new("CPU Core #2", 60),
                new("CPU Core #3", 101)
            ]);
        var temperature = SensorSelection.SelectCpuTemperatureSensor(
            [
                new("CPU Package", 121),
                new("Core #1", 58),
                new("PCH", 45)
            ],
            "Intel Core");

        Assert.Equal(40, usage?.Value);
        Assert.Equal("Core #1", temperature?.Name);
        Assert.Equal(58, temperature?.Value);
    }
}
