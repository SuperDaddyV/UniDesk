namespace UniDesk.Services;

public static class SensorSelection
{
    public static CpuTemperatureSensorSelection? SelectCpuTemperatureSensor(
        IEnumerable<CpuTemperatureSensorCandidate> sensors,
        string? hardwareName = null)
    {
        var validSensors = sensors
            .Where(sensor => IsValidTemperature(sensor.Value) && !IsExcludedCpuTemperatureSensor(sensor.Name))
            .Select(sensor => new CpuTemperatureSensorSelection(sensor.Name, sensor.Value!.Value))
            .ToList();

        if (validSensors.Count == 0)
        {
            return null;
        }

        var keywordGroups = GetCpuTemperaturePriorityGroups(hardwareName);
        foreach (var group in keywordGroups)
        {
            var match = PickHighestByKeywords(validSensors, group);
            if (match.HasValue)
            {
                return match;
            }
        }

        var coreMax = validSensors
            .Where(sensor => IsCoreTemperatureSensor(sensor.Name))
            .OrderByDescending(sensor => sensor.Value)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(coreMax.Name))
        {
            return coreMax;
        }

        return validSensors
            .OrderByDescending(sensor => sensor.Value)
            .First();
    }

    public static CpuTemperatureSensorSelection? SelectWindowsThermalZoneTemperatureSensor(
        IEnumerable<CpuTemperatureSensorCandidate> sensors)
    {
        var validSensors = sensors
            .Where(sensor => IsValidTemperature(sensor.Value))
            .Select(sensor => new CpuTemperatureSensorSelection(sensor.Name, sensor.Value!.Value))
            .ToList();

        if (validSensors.Count == 0)
        {
            return null;
        }

        return validSensors
            .OrderByDescending(sensor => sensor.Value)
            .First();
    }

    public static CpuTemperatureProviderSelection? SelectCpuTemperatureProvider(
        CpuTemperatureSensorSelection? cpuSelection,
        CpuTemperatureSensorSelection? motherboardSelection,
        CpuTemperatureSensorSelection? windowsThermalZoneSelection)
    {
        var candidates = new List<CpuTemperatureProviderSelection>();
        if (cpuSelection.HasValue)
        {
            candidates.Add(new CpuTemperatureProviderSelection(
                "LibreHardwareMonitor CPU",
                cpuSelection.Value.Name,
                cpuSelection.Value.Value,
                10));
        }

        if (motherboardSelection.HasValue)
        {
            candidates.Add(new CpuTemperatureProviderSelection(
                "LibreHardwareMonitor Motherboard",
                motherboardSelection.Value.Name,
                motherboardSelection.Value.Value,
                30));
        }

        if (windowsThermalZoneSelection.HasValue)
        {
            candidates.Add(new CpuTemperatureProviderSelection(
                "Windows ACPI Thermal Zone",
                windowsThermalZoneSelection.Value.Name,
                windowsThermalZoneSelection.Value.Value,
                80));
        }

        return candidates.Count == 0
            ? null
            : candidates.OrderBy(candidate => candidate.Priority).First();
    }

    public static CpuTemperatureSensorSelection? SelectCpuMotherboardTemperatureSensor(
        IEnumerable<CpuTemperatureSensorCandidate> sensors,
        string? cpuHardwareName = null)
    {
        var cpuNamedSensors = sensors
            .Where(sensor => IsValidTemperature(sensor.Value) &&
                             IsCpuMotherboardTemperatureSensorName(sensor.Name) &&
                             !IsExcludedMotherboardCpuTemperatureSensor(sensor.Name))
            .ToList();

        var cpuNamedSelection = SelectCpuTemperatureSensor(cpuNamedSensors, cpuHardwareName);
        if (cpuNamedSelection.HasValue)
        {
            return cpuNamedSelection;
        }

        if (!IsAmdRyzen9000DesktopCpu(cpuHardwareName))
        {
            return null;
        }

        var pchFallback = sensors
            .Where(sensor => IsValidTemperature(sensor.Value) && IsPchTemperatureSensor(sensor.Name))
            .Select(sensor => new CpuTemperatureSensorSelection(sensor.Name, sensor.Value!.Value))
            .OrderByDescending(sensor => sensor.Value)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(pchFallback.Name) ? null : pchFallback;
    }

    public static CpuUsageSensorSelection? SelectCpuUsageSensor(
        IEnumerable<CpuUsageSensorCandidate> sensors)
    {
        var validSensors = sensors
            .Where(sensor => IsValidPercentage(sensor.Value))
            .Select(sensor => new CpuUsageSensorSelection(sensor.Name, sensor.Value!.Value))
            .ToList();

        if (validSensors.Count == 0)
        {
            return null;
        }

        foreach (var keywords in new[]
                 {
                     new[] { "CPU Total" },
                     new[] { "Total" },
                     new[] { "CPU Package" },
                     new[] { "Package" }
                 })
        {
            var match = PickHighestByKeywords(
                validSensors.Select(sensor => new CpuTemperatureSensorSelection(sensor.Name, sensor.Value)),
                keywords);
            if (match.HasValue)
            {
                return new CpuUsageSensorSelection(match.Value.Name, match.Value.Value);
            }
        }

        var coreLoads = validSensors
            .Where(sensor => IsCoreTemperatureSensor(sensor.Name))
            .ToList();
        if (coreLoads.Count > 0)
        {
            return new CpuUsageSensorSelection("CPU Core Average", coreLoads.Average(sensor => sensor.Value));
        }

        return validSensors
            .OrderByDescending(sensor => sensor.Value)
            .First();
    }

    public static GpuSensorSelection? SelectGpuUsageSensor(IEnumerable<GpuSensorCandidate> sensors)
    {
        var validSensors = sensors
            .Where(sensor => IsValidPercentage(sensor.Value))
            .Select(sensor => new GpuSensorSelection(sensor.Name, sensor.Value!.Value))
            .ToList();

        if (validSensors.Count == 0)
        {
            return null;
        }

        foreach (var keywords in new[]
                 {
                     new[] { "GPU Core" },
                     new[] { "GPU 3D" },
                     new[] { "D3D 3D" },
                     new[] { "Graphics" },
                     new[] { "Render" },
                     new[] { "Overall" },
                     new[] { "GPU" },
                     new[] { "Core" }
                 })
        {
            var match = PickHighestGpuByKeywords(validSensors, keywords);
            if (match.HasValue)
            {
                return match;
            }
        }

        return validSensors
            .OrderByDescending(sensor => sensor.Value)
            .First();
    }

    public static GpuSensorSelection? SelectGpuTemperatureSensor(IEnumerable<GpuSensorCandidate> sensors)
    {
        var validSensors = sensors
            .Where(sensor => IsValidTemperature(sensor.Value))
            .Select(sensor => new GpuSensorSelection(sensor.Name, sensor.Value!.Value))
            .ToList();

        if (validSensors.Count == 0)
        {
            return null;
        }

        foreach (var keywords in new[]
                 {
                     new[] { "GPU Core" },
                     new[] { "GPU Hot Spot" },
                     new[] { "Hot Spot" },
                     new[] { "GPU Memory Junction" },
                     new[] { "Memory Junction" },
                     new[] { "GPU Temperature" },
                     new[] { "Temperature" },
                     new[] { "Core" }
                 })
        {
            var match = PickHighestGpuByKeywords(validSensors, keywords);
            if (match.HasValue)
            {
                return match;
            }
        }

        return validSensors
            .OrderByDescending(sensor => sensor.Value)
            .First();
    }

    public static bool IsValidPercentage(double? value)
    {
        if (!value.HasValue)
        {
            return false;
        }

        var percentage = value.Value;
        return !double.IsNaN(percentage) &&
               !double.IsInfinity(percentage) &&
               percentage >= 0 &&
               percentage <= 100;
    }

    public static bool IsValidTemperature(double? value)
    {
        if (!value.HasValue)
        {
            return false;
        }

        var temperature = value.Value;
        return !double.IsNaN(temperature) &&
               !double.IsInfinity(temperature) &&
               temperature > 0 &&
               temperature <= 120;
    }

    internal static double? NormalizePercentage(double? value) =>
        IsValidPercentage(value) ? value!.Value : null;

    internal static double? NormalizeTemperature(double? value) =>
        IsValidTemperature(value) ? value!.Value : null;

    private static IReadOnlyList<string[]> GetCpuTemperaturePriorityGroups(string? hardwareName)
    {
        var isAmd = ContainsAny(hardwareName, "AMD", "Ryzen");
        var isIntel = ContainsAny(hardwareName, "Intel", "Core");

        if (isAmd)
        {
            return
            [
                ["Tctl"],
                ["Tdie"],
                ["Die"],
                ["CPU Package"],
                ["Package"],
                ["Core Max"],
                ["Core Average"],
                ["CPU Core"]
            ];
        }

        if (isIntel)
        {
            return
            [
                ["CPU Package"],
                ["Package"],
                ["CPU IA"],
                ["IA Cores"],
                ["Core Max"],
                ["Core Average"],
                ["CPU Core"],
                ["Tctl"],
                ["Tdie"]
            ];
        }

        return
        [
            ["CPU Package"],
            ["Package"],
            ["CPU IA"],
            ["IA Cores"],
            ["Core Max"],
            ["Core Average"],
            ["CPU Core"],
            ["Tctl"],
            ["Tdie"]
        ];
    }

    private static CpuTemperatureSensorSelection? PickHighestByKeywords(
        IEnumerable<CpuTemperatureSensorSelection> sensors,
        IReadOnlyCollection<string> keywords)
    {
        var match = sensors
            .Where(sensor => ContainsAny(sensor.Name, keywords))
            .OrderByDescending(sensor => sensor.Value)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(match.Name) ? null : match;
    }

    private static bool IsExcludedCpuTemperatureSensor(string? name) =>
        ContainsAny(
            name,
            "Distance",
            "TjMax",
            "Throttle",
            "Limit",
            "PCH",
            "VRM",
            "MOS",
            "Chipset",
            "Motherboard",
            "System",
            "AUX",
            "DIMM",
            "Memory",
            "GPU",
            "GT Cores",
            "PCI");

    private static bool IsExcludedMotherboardCpuTemperatureSensor(string? name) =>
        IsExcludedCpuTemperatureSensor(name) ||
        ContainsAny(name, "VRM", "MOS", "Chipset", "Motherboard", "System", "AUX", "DIMM", "Memory", "GPU", "PCI");

    private static bool IsCpuMotherboardTemperatureSensorName(string? name) =>
        ContainsAny(name, "CPU", "Tctl", "Tdie", "Package");

    private static bool IsPchTemperatureSensor(string? name) =>
        ContainsAny(name, "PCH");

    private static bool IsAmdRyzen9000DesktopCpu(string? hardwareName) =>
        ContainsAny(hardwareName, "AMD Ryzen") &&
        ContainsAny(hardwareName, " 9600X", " 9700X", " 9800X", " 9900X", " 9950X");

    private static bool IsCoreTemperatureSensor(string? name) =>
        ContainsAny(name, "Core #", "CPU Core", "Core Max", "Core Average") ||
        (!string.IsNullOrWhiteSpace(name) &&
         name.StartsWith("Core ", StringComparison.OrdinalIgnoreCase));

    private static GpuSensorSelection? PickHighestGpuByKeywords(
        IEnumerable<GpuSensorSelection> sensors,
        IReadOnlyCollection<string> keywords)
    {
        var match = sensors
            .Where(sensor => ContainsAny(sensor.Name, keywords))
            .OrderByDescending(sensor => sensor.Value)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(match.Name) ? null : match;
    }

    private static bool ContainsAny(string? text, params string[] keywords) =>
        ContainsAny(text, (IReadOnlyCollection<string>)keywords);

    private static bool ContainsAny(string? text, IReadOnlyCollection<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
