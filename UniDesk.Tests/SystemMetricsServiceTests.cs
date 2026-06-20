using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class SystemMetricsServiceTests
{
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuMotherboardTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuMotherboardTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuMotherboardTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectWindowsThermalZoneTemperatureSensor(
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
        var selection = SystemMetricsService.SelectCpuTemperatureProvider(
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
        var selection = SystemMetricsService.SelectCpuTemperatureProvider(
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
        var selection = SystemMetricsService.SelectCpuUsageSensor(
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
        var selection = SystemMetricsService.SelectCpuUsageSensor(
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
        var selection = SystemMetricsService.SelectGpuUsageSensor(
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
        var selection = SystemMetricsService.SelectGpuUsageSensor(
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
        var selection = SystemMetricsService.SelectGpuTemperatureSensor(
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
        var selection = SystemMetricsService.SelectGpuTemperatureSensor(
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
        var usage = SystemMetricsService.SelectGpuUsageSensor(
            [
                new("GPU Core", -1),
                new("GPU 3D", 101),
                new("Graphics", double.PositiveInfinity)
            ]);
        var temperature = SystemMetricsService.SelectGpuTemperatureSensor(
            [
                new("GPU Core", -1),
                new("GPU Temperature", 999),
                new("Core", double.NaN)
            ]);

        Assert.Null(usage);
        Assert.Null(temperature);
    }
}
