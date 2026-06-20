using UniDesk.Models;
using UniDesk.Helpers;
using LibreHardwareMonitor.Hardware;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace UniDesk.Services;

public sealed class SystemMetricsService : ISystemMetricsService, IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly AsusHardwareReader _asusReader = new();
    private readonly LibreHardwareCpuReader _libreHardwareCpuReader = new();
    private readonly AmdAdlReader _amdReader = new();
    private readonly NvidiaNvmlReader _nvidiaReader = new();
    private readonly LibreHardwareGpuReader _libreHardwareReader = new();
    private readonly NetworkSpeedReader _networkReader = new();
    private bool _disposed;
#if DEBUG
    private DateTime _lastMemoryLogUtc = DateTime.MinValue;
#endif

    public SystemMetricsService()
    {
        try { _cpuCounter.NextValue(); } catch { }
    }

    public SystemMetricsSnapshot Read()
    {
        var cpuUsage = SafePercentage(() => _cpuCounter.NextValue());
        var cpuTemperature = NormalizeTemperature(_asusReader.ReadCpuPackageTemperature());
        var cpuFallback = CpuMetrics.Empty;
#if DEBUG
        cpuFallback = _libreHardwareCpuReader.Read();
#else
        if (!cpuUsage.HasValue || !cpuTemperature.HasValue)
        {
            cpuFallback = _libreHardwareCpuReader.Read();
        }
#endif
        var gpu = ReadGpuMetrics();
        var memory = ReadMemoryUsage();
        var network = _networkReader.Read();
        return new SystemMetricsSnapshot
        {
            CpuUsage = cpuUsage ?? cpuFallback.CpuUsage,
            CpuTemperature = cpuTemperature ?? cpuFallback.CpuTemperature,
            MemoryUsage = memory.UsagePercent,
            GpuUsage = gpu.GpuUsage,
            GpuTemperature = gpu.GpuTemperature,
            NetworkReceivedBytesPerSecond = network.ReceivedBytesPerSecond,
            NetworkSentBytesPerSecond = network.SentBytesPerSecond
        };
    }

    private GpuMetrics ReadGpuMetrics()
    {
        var candidates = new List<GpuMetrics>
        {
            _nvidiaReader.Read(),
            _amdReader.Read()
        };

#if DEBUG
        candidates.Add(_libreHardwareReader.Read());
#else
        if (!candidates.Any(candidate => candidate.HasAllValues))
        {
            candidates.Add(_libreHardwareReader.Read());
        }
#endif

        return SelectGpuMetrics(candidates);
    }

    private static GpuMetrics SelectGpuMetrics(IEnumerable<GpuMetrics> candidates)
    {
        var validCandidates = candidates
            .Where(candidate => candidate.HasAnyValue)
            .ToList();

        if (validCandidates.Count == 0)
        {
            return GpuMetrics.Empty;
        }

        var selected = validCandidates
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .First();

        if (selected.HasAllValues)
        {
            return selected;
        }

        return new GpuMetrics(
            selected.GpuUsage ?? SelectBestGpuUsage(validCandidates),
            selected.GpuTemperature ?? SelectBestGpuTemperature(validCandidates),
            selected.SourceName,
            selected.SourcePriority,
            selected.IsDiscrete);
    }

    private static double? SelectBestGpuUsage(IEnumerable<GpuMetrics> candidates)
    {
        return candidates
            .Where(candidate => candidate.GpuUsage.HasValue)
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Select(candidate => candidate.GpuUsage)
            .FirstOrDefault();
    }

    private static double? SelectBestGpuTemperature(IEnumerable<GpuMetrics> candidates)
    {
        return candidates
            .Where(candidate => candidate.GpuTemperature.HasValue)
            .OrderBy(candidate => candidate.SelectionRank)
            .ThenBy(candidate => candidate.SourcePriority)
            .Select(candidate => candidate.GpuTemperature)
            .FirstOrDefault();
    }

    private static double? SafePercentage(Func<float> read)
    {
        try { return NormalizePercentage(read()); }
        catch { return null; }
    }

    private MemoryMetrics ReadMemoryUsage()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status) || status.ullTotalPhys == 0)
        {
            LogMemoryMetrics("GlobalMemoryStatusEx", status.ullTotalPhys, status.ullAvailPhys, null);
            return ReadMemoryUsageFromPerformanceInfo();
        }

        return CreateMemoryMetrics("GlobalMemoryStatusEx", status.ullTotalPhys, status.ullAvailPhys);
    }

    private MemoryMetrics ReadMemoryUsageFromPerformanceInfo()
    {
        if (!GetPerformanceInfo(out var info, (uint)Marshal.SizeOf<PerformanceInformation>()) ||
            info.PhysicalTotal == UIntPtr.Zero ||
            info.PageSize == UIntPtr.Zero)
        {
            LogMemoryMetrics("GetPerformanceInfo", 0, 0, null);
            return MemoryMetrics.Empty;
        }

        var pageSize = info.PageSize.ToUInt64();
        var total = info.PhysicalTotal.ToUInt64() * pageSize;
        var available = info.PhysicalAvailable.ToUInt64() * pageSize;
        return CreateMemoryMetrics("GetPerformanceInfo", total, available);
    }

    private MemoryMetrics CreateMemoryMetrics(string source, ulong total, ulong available)
    {
        if (total == 0)
        {
            LogMemoryMetrics(source, total, available, null);
            return MemoryMetrics.Empty;
        }

        var normalizedAvailable = Math.Min(available, total);
        var used = total - normalizedAvailable;
        var percent = NormalizePercentage(used / (double)total * 100d);
        LogMemoryMetrics(source, total, normalizedAvailable, percent);
        return new MemoryMetrics(percent, total, normalizedAvailable, used);
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

    private static double? NormalizePercentage(double? value) =>
        IsValidPercentage(value) ? value!.Value : null;

    private static double? NormalizeTemperature(double? value) =>
        IsValidTemperature(value) ? value!.Value : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cpuCounter.Dispose();
        _libreHardwareCpuReader.Dispose();
        _nvidiaReader.Dispose();
        _libreHardwareReader.Dispose();
    }

    [Conditional("DEBUG")]
    private void LogMemoryMetrics(string source, ulong totalBytes, ulong availableBytes, double? usagePercent)
    {
#if DEBUG
        var now = DateTime.UtcNow;
        if (now - _lastMemoryLogUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastMemoryLogUtc = now;
        var usedBytes = totalBytes >= availableBytes ? totalBytes - availableBytes : 0;
        var message =
            $"内存来源：{source}; 总内存={totalBytes}; 可用内存={availableBytes}; 已用内存={usedBytes}; 使用率={(usagePercent.HasValue ? usagePercent.Value.ToString("0.0") : "null")}";
        Debug.WriteLine(message);
        Logger.LogInfo(message, "SystemMetricsService.Memory");
#endif
    }

    public readonly record struct CpuTemperatureSensorCandidate(string Name, double? Value);

    public readonly record struct CpuTemperatureSensorSelection(string Name, double Value);

    public readonly record struct CpuUsageSensorCandidate(string Name, double? Value);

    public readonly record struct CpuUsageSensorSelection(string Name, double Value);

    public readonly record struct CpuTemperatureProviderSelection(string Source, string Name, double Value, int Priority);

    public readonly record struct GpuSensorCandidate(string Name, double? Value);

    public readonly record struct GpuSensorSelection(string Name, double Value);

    public static CpuTemperatureSensorSelection? SelectCpuTemperatureSensor(
        IEnumerable<CpuTemperatureSensorCandidate> sensors,
        string? hardwareName = null)
    {
        var validSensors = sensors
            .Where(sensor => IsValidCpuTemperature(sensor.Value) && !IsExcludedCpuTemperatureSensor(sensor.Name))
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
            .Where(sensor => IsValidCpuTemperature(sensor.Value))
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

    public static CpuTemperatureSensorSelection? SelectCpuMotherboardTemperatureSensor(
        IEnumerable<CpuTemperatureSensorCandidate> sensors,
        string? cpuHardwareName = null)
    {
        var cpuNamedSensors = sensors
            .Where(sensor => IsValidCpuTemperature(sensor.Value) &&
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
            .Where(sensor => IsValidCpuTemperature(sensor.Value) && IsPchTemperatureSensor(sensor.Name))
            .Select(sensor => new CpuTemperatureSensorSelection(sensor.Name, sensor.Value!.Value))
            .OrderByDescending(sensor => sensor.Value)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(pchFallback.Name) ? null : pchFallback;
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

    private static bool IsValidCpuTemperature(double? value)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return IsValidTemperature(value);
    }

    public static CpuUsageSensorSelection? SelectCpuUsageSensor(IEnumerable<CpuUsageSensorCandidate> sensors)
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation performanceInformation, uint cb);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MemoryStatusEx()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint cb;
        public UIntPtr CommitTotal;
        public UIntPtr CommitLimit;
        public UIntPtr CommitPeak;
        public UIntPtr PhysicalTotal;
        public UIntPtr PhysicalAvailable;
        public UIntPtr SystemCache;
        public UIntPtr KernelTotal;
        public UIntPtr KernelPaged;
        public UIntPtr KernelNonpaged;
        public UIntPtr PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    private sealed class AsusHardwareReader
    {
        private const int TemperatureDataType = 3;
        private const int SensorRecordSize = 0x88;
        private const int CurrentValueOffset = 0x04;
        private const int SensorNameOffset = 0x24;
        private HMGetData2? _getData2;
        private bool _initialized;

        public double? ReadCpuPackageTemperature()
        {
            try
            {
                if (!EnsureInitialized() || _getData2 == null) return null;

                var count = _getData2(TemperatureDataType, IntPtr.Zero);
                if (count <= 0 || count > 64) return null;

                var buffer = Marshal.AllocHGlobal(count * SensorRecordSize);
                try
                {
                    for (var i = 0; i < count * SensorRecordSize; i++) Marshal.WriteByte(buffer, i, 0);
                    if (_getData2(TemperatureDataType, buffer) <= 0) return null;

                    double? cpu = null;
                    for (var i = 0; i < count; i++)
                    {
                        var record = IntPtr.Add(buffer, i * SensorRecordSize);
                        var name = ReadWideString(IntPtr.Add(record, SensorNameOffset), 48);
                        var temp = Marshal.ReadInt32(IntPtr.Add(record, CurrentValueOffset)) / 10.0;
                        if (temp <= 0 || temp >= 120) continue;

                        if (name.IndexOf("CPU Package", StringComparison.OrdinalIgnoreCase) >= 0) return temp;
                        if (!cpu.HasValue && name.Equals("CPU", StringComparison.OrdinalIgnoreCase)) cpu = temp;
                    }

                    return cpu;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return _getData2 != null;
            _initialized = true;

            var home = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"ASUS\Armoury Crate Service\MB_Home");
            var library = Path.Combine(home, "aaHMLib_x64.dll");
            if (!File.Exists(library)) return false;

            SetDllDirectory(home);
            var handle = LoadLibrary(library);
            if (handle == IntPtr.Zero) return false;

            var proc = GetProcAddress(handle, "HM_GetData2");
            if (proc == IntPtr.Zero) return false;

            _getData2 = Marshal.GetDelegateForFunctionPointer<HMGetData2>(proc);
            return true;
        }

        private static string ReadWideString(IntPtr ptr, int maxChars)
        {
            var bytes = new byte[maxChars * 2];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            var end = 0;
            for (; end + 1 < bytes.Length; end += 2)
            {
                if (bytes[end] == 0 && bytes[end + 1] == 0) break;
            }

            return Encoding.Unicode.GetString(bytes, 0, end);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int HMGetData2(int dataType, IntPtr data);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }

    private sealed class LibreHardwareCpuReader : IDisposable
    {
        private Computer? _computer;
        private bool _initialized;
        private DateTime _lastSensorLogUtc = DateTime.MinValue;
        private DateTime _lastReaderErrorLogUtc = DateTime.MinValue;
        private DateTime _lastWindowsThermalZoneErrorLogUtc = DateTime.MinValue;

        public CpuMetrics Read()
        {
            try
            {
                if (!EnsureInitialized() || _computer == null)
                {
                    return CpuMetrics.Empty;
                }

                var cpuHardwareNames = new List<string>();
                var loadSensors = new List<CpuUsageSensorCandidate>();
                var temperatureSensors = new List<CpuTemperatureSensorCandidate>();
                var motherboardTemperatureSensors = new List<CpuTemperatureSensorCandidate>();
                var windowsThermalZoneSensors = new List<CpuTemperatureSensorCandidate>();

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Cpu)
                    {
                        continue;
                    }

                    cpuHardwareNames.Add(hardware.Name);
                    UpdateHardwareTree(hardware);

                    var allSensors = GetSensors(hardware).ToList();
                    loadSensors.AddRange(allSensors
                        .Where(sensor => sensor.SensorType == SensorType.Load)
                        .Select(sensor => new CpuUsageSensorCandidate(sensor.Name, sensor.Value)));
                    temperatureSensors.AddRange(allSensors
                        .Where(sensor => sensor.SensorType == SensorType.Temperature)
                        .Select(sensor => new CpuTemperatureSensorCandidate(sensor.Name, sensor.Value)));
                }

                var cpuHardwareName = cpuHardwareNames.Count == 0
                    ? null
                    : string.Join("; ", cpuHardwareNames);
                var loadSelection = SelectCpuUsageSensor(loadSensors);
                var cpuTemperatureSelection = SelectCpuTemperatureSensor(temperatureSensors, cpuHardwareName);
                CpuTemperatureSensorSelection? motherboardTemperatureSelection = null;
                CpuTemperatureSensorSelection? windowsThermalZoneSelection = null;

                if (!cpuTemperatureSelection.HasValue)
                {
                    motherboardTemperatureSensors = ReadMotherboardTemperatureSensors().ToList();
                    motherboardTemperatureSelection = SelectCpuMotherboardTemperatureSensor(
                        motherboardTemperatureSensors,
                        cpuHardwareName);
                }

                if (!cpuTemperatureSelection.HasValue && !motherboardTemperatureSelection.HasValue)
                {
                    windowsThermalZoneSensors = ReadWindowsThermalZoneTemperatureSensors();
                    windowsThermalZoneSelection = SelectWindowsThermalZoneTemperatureSensor(windowsThermalZoneSensors);
                }

                var temperatureSelection = SelectCpuTemperatureProvider(
                    cpuTemperatureSelection,
                    motherboardTemperatureSelection,
                    windowsThermalZoneSelection);

                LogCpuSensors(
                    cpuHardwareName,
                    loadSensors,
                    temperatureSensors,
                    motherboardTemperatureSensors,
                    windowsThermalZoneSensors,
                    loadSelection,
                    cpuTemperatureSelection,
                    temperatureSelection,
                    motherboardTemperatureSelection,
                    windowsThermalZoneSelection);

                if (loadSelection.HasValue || temperatureSelection.HasValue)
                {
                    return new CpuMetrics(loadSelection?.Value, temperatureSelection?.Value);
                }
            }
            catch (Exception ex)
            {
                LogCpuTemperatureReaderError(ex);
            }

            return CpuMetrics.Empty;
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return _computer != null;
            }

            _initialized = true;

            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsMotherboardEnabled = true
                };
                _computer.Open();
                return true;
            }
            catch (Exception ex)
            {
                _computer = null;
                LogCpuTemperatureReaderError(ex);
                return false;
            }
        }

        private IEnumerable<CpuTemperatureSensorCandidate> ReadMotherboardTemperatureSensors()
        {
            if (_computer == null)
            {
                yield break;
            }

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Motherboard)
                {
                    continue;
                }

                UpdateHardwareTree(hardware);
                foreach (var sensor in GetSensors(hardware).Where(sensor => sensor.SensorType == SensorType.Temperature))
                {
                    yield return new CpuTemperatureSensorCandidate(sensor.Name, sensor.Value);
                }
            }
        }

        private List<CpuTemperatureSensorCandidate> ReadWindowsThermalZoneTemperatureSensors()
        {
            var sensors = new List<CpuTemperatureSensorCandidate>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

                foreach (ManagementObject item in searcher.Get())
                {
                    var name = item["InstanceName"]?.ToString();
                    var rawValue = item["CurrentTemperature"];
                    if (rawValue == null)
                    {
                        continue;
                    }

                    var celsius = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture) / 10d - 273.15d;
                    sensors.Add(new CpuTemperatureSensorCandidate(
                        string.IsNullOrWhiteSpace(name) ? "ACPI Thermal Zone" : $"ACPI {name}",
                        celsius));
                }
            }
            catch (Exception ex)
            {
                LogWindowsThermalZoneReaderError(ex);
            }

            return sensors;
        }

        private static void UpdateHardwareTree(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                UpdateHardwareTree(subHardware);
            }
        }

        private static IEnumerable<ISensor> GetSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                yield return sensor;
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                foreach (var sensor in GetSensors(subHardware))
                {
                    yield return sensor;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _computer?.Close();
            }
            catch
            {
            }
        }

        private void LogCpuSensors(
            string? hardwareName,
            IReadOnlyCollection<CpuUsageSensorCandidate> loadSensors,
            IReadOnlyCollection<CpuTemperatureSensorCandidate> temperatureSensors,
            IReadOnlyCollection<CpuTemperatureSensorCandidate> motherboardTemperatureSensors,
            IReadOnlyCollection<CpuTemperatureSensorCandidate> windowsThermalZoneSensors,
            CpuUsageSensorSelection? loadSelection,
            CpuTemperatureSensorSelection? cpuTemperatureSelection,
            CpuTemperatureProviderSelection? temperatureSelection,
            CpuTemperatureSensorSelection? motherboardTemperatureSelection,
            CpuTemperatureSensorSelection? windowsThermalZoneSelection)
        {
            var now = DateTime.UtcNow;
            if (now - _lastSensorLogUtc < TimeSpan.FromMinutes(5))
            {
                return;
            }

            _lastSensorLogUtc = now;

            var loadSensorText = loadSensors.Count == 0
                ? "未发现 Load 传感器。"
                : string.Join("; ", loadSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var temperatureSensorText = temperatureSensors.Count == 0
                ? "未发现 Temperature 传感器。部分硬件传感器可能需要管理员权限或主板驱动支持。"
                : string.Join("; ", temperatureSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var motherboardTemperatureSensorText = motherboardTemperatureSensors.Count == 0
                ? "未发现主板 Temperature 传感器。"
                : string.Join("; ", motherboardTemperatureSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var windowsThermalZoneSensorText = windowsThermalZoneSensors.Count == 0
                ? "未发现 Windows ACPI Thermal Zone。"
                : string.Join("; ", windowsThermalZoneSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var selectedLoadText = loadSelection.HasValue
                ? $"{loadSelection.Value.Name}={loadSelection.Value.Value:0.0}"
                : "未选择可用 CPU 使用率传感器。";
            var selectedCpuTemperatureText = cpuTemperatureSelection.HasValue
                ? $"{cpuTemperatureSelection.Value.Name}={cpuTemperatureSelection.Value.Value:0.0}"
                : "未选择 LibreHardwareMonitor CPU 温度传感器。";
            var selectedTemperatureText = temperatureSelection.HasValue
                ? $"{temperatureSelection.Value.Source}/{temperatureSelection.Value.Name}={temperatureSelection.Value.Value:0.0}"
                : "未选择可用 CPU 温度传感器。";
            var selectedMotherboardTemperatureText = motherboardTemperatureSelection.HasValue
                ? $"{motherboardTemperatureSelection.Value.Name}={motherboardTemperatureSelection.Value.Value:0.0}"
                : "未选择主板 CPU 温度传感器。";
            var selectedWindowsThermalZoneText = windowsThermalZoneSelection.HasValue
                ? $"{windowsThermalZoneSelection.Value.Name}={windowsThermalZoneSelection.Value.Value:0.0}"
                : "未选择 Windows ACPI Thermal Zone。";
            var message =
                $"CPU 硬件：{(string.IsNullOrWhiteSpace(hardwareName) ? "未发现 CPU 硬件" : hardwareName)}; " +
                $"进程权限：{GetProcessPrivilegeText()}; 架构：{RuntimeInformation.ProcessArchitecture}; " +
                $"Load 传感器：{loadSensorText}; CPU Temperature 传感器：{temperatureSensorText}; " +
                $"主板 Temperature 传感器：{motherboardTemperatureSensorText}; Windows ACPI Thermal Zone：{windowsThermalZoneSensorText}; " +
                $"最终 CPU 使用率：{selectedLoadText}; LHM CPU 温度：{selectedCpuTemperatureText}; " +
                $"主板兜底温度：{selectedMotherboardTemperatureText}; Windows 兜底温度：{selectedWindowsThermalZoneText}; " +
                $"最终 CPU 温度：{selectedTemperatureText}";

            Debug.WriteLine(message);
            Logger.LogInfo(message, "SystemMetricsService.Cpu");
        }

        private static string GetProcessPrivilegeText()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator) ? "管理员" : "普通用户";
            }
            catch
            {
                return "未知";
            }
        }

        private void LogCpuTemperatureReaderError(Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - _lastReaderErrorLogUtc < TimeSpan.FromMinutes(5))
            {
                return;
            }

            _lastReaderErrorLogUtc = now;
            Debug.WriteLine($"CPU 温度读取失败：{ex.GetType().Name}: {ex.Message}");
            Logger.LogWarning(
                $"CPU 温度读取失败：{ex.GetType().Name}: {ex.Message}。部分硬件传感器可能需要管理员权限或主板驱动支持。",
                "SystemMetricsService.CpuTemperature");
        }

        private void LogWindowsThermalZoneReaderError(Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - _lastWindowsThermalZoneErrorLogUtc < TimeSpan.FromMinutes(5))
            {
                return;
            }

            _lastWindowsThermalZoneErrorLogUtc = now;
            Logger.LogWarning(
                $"Windows ACPI Thermal Zone 读取失败：{ex.GetType().Name}: {ex.Message}。将继续使用其它 CPU 温度来源。",
                "SystemMetricsService.WindowsThermalZone");
        }
    }

    private sealed class AmdAdlReader
    {
        private const int SensorGpuTemperatureEdge = 8;
        private const int SensorGpuActivity = 19;
        private readonly ADLMainMemoryAlloc _allocCallback = Alloc;
        private bool _initialized;
        private IntPtr _context = IntPtr.Zero;

        public GpuMetrics Read()
        {
            try
            {
                if (!EnsureInitialized()) return GpuMetrics.Empty;
                if (ADL2_Adapter_NumberOfAdapters_Get(_context, out var count) != 0 || count <= 0) return GpuMetrics.Empty;

                var size = Marshal.SizeOf<AdapterInfo>();
                var ptr = Marshal.AllocHGlobal(size * count);
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        Marshal.StructureToPtr(new AdapterInfo { iSize = size }, IntPtr.Add(ptr, i * size), false);
                    }

                    if (ADL2_Adapter_AdapterInfo_Get(_context, ptr, size * count) != 0) return GpuMetrics.Empty;

                    for (var i = 0; i < count; i++)
                    {
                        var info = Marshal.PtrToStructure<AdapterInfo>(IntPtr.Add(ptr, i * size));
                        if (string.IsNullOrEmpty(info.strAdapterName) ||
                            info.strAdapterName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        ADL2_Adapter_Active_Get(_context, info.iAdapterIndex, out var active);
                        if (active != 1 && i != 0) continue;

                        var data = new ADLPMLogDataOutput
                        {
                            size = Marshal.SizeOf<ADLPMLogDataOutput>(),
                            sensors = new ADLSingleSensorData[256]
                        };

                        if (ADL2_New_QueryPMLogData_Get(_context, info.iAdapterIndex, ref data) != 0) continue;

                        double? temp = null;
                        double? usage = null;
                        if (data.sensors[SensorGpuTemperatureEdge].supported != 0 &&
                            IsValidTemperature(data.sensors[SensorGpuTemperatureEdge].value))
                        {
                            temp = data.sensors[SensorGpuTemperatureEdge].value;
                        }

                        if (data.sensors[SensorGpuActivity].supported != 0 &&
                            IsValidPercentage(data.sensors[SensorGpuActivity].value))
                        {
                            usage = data.sensors[SensorGpuActivity].value;
                        }

                        if (temp.HasValue || usage.HasValue)
                        {
                            return new GpuMetrics(usage, temp, info.strAdapterName, 20, true);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch
            {
            }

            return GpuMetrics.Empty;
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return _context != IntPtr.Zero;
            _initialized = true;
            try
            {
                return ADL2_Main_Control_Create(_allocCallback, 1, out _context) == 0 && _context != IntPtr.Zero;
            }
            catch
            {
                _context = IntPtr.Zero;
                return false;
            }
        }

        private static IntPtr Alloc(int size) => Marshal.AllocHGlobal(size);

        private delegate IntPtr ADLMainMemoryAlloc(int size);

        [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADLMainMemoryAlloc callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int count);

        [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, IntPtr info, int inputSize);

        [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_Active_Get(IntPtr context, int adapterIndex, out int active);

        [DllImport("atiadlxx.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, ref ADLPMLogDataOutput output);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct AdapterInfo
        {
            public int iSize;
            public int iAdapterIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strUDID;
            public int iBusNumber;
            public int iDeviceNumber;
            public int iFunctionNumber;
            public int iVendorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strDisplayName;
            public int iPresent;
            public int iExist;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strDriverPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strDriverPathExt;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strPNPString;
            public int iOSDisplayIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLSingleSensorData
        {
            public int supported;
            public int value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLPMLogDataOutput
        {
            public int size;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ADLSingleSensorData[] sensors;
        }
    }

    private sealed class NvidiaNvmlReader : IDisposable
    {
        private const int NvmlSuccess = 0;
        private const int NvmlTemperatureGpu = 0;
        private bool _initialized;
        private bool _available;

        public GpuMetrics Read()
        {
            try
            {
                if (!EnsureInitialized()) return GpuMetrics.Empty;
                if (nvmlDeviceGetCount_v2(out var count) != NvmlSuccess || count == 0) return GpuMetrics.Empty;

                for (uint i = 0; i < count; i++)
                {
                    if (nvmlDeviceGetHandleByIndex_v2(i, out var device) != NvmlSuccess ||
                        device == IntPtr.Zero)
                    {
                        continue;
                    }

                    double? temp = null;
                    double? usage = null;

                    if (nvmlDeviceGetTemperature(device, NvmlTemperatureGpu, out var rawTemp) == NvmlSuccess &&
                        IsValidTemperature(rawTemp))
                    {
                        temp = rawTemp;
                    }

                    if (nvmlDeviceGetUtilizationRates(device, out var utilization) == NvmlSuccess &&
                        IsValidPercentage(utilization.gpu))
                    {
                        usage = utilization.gpu;
                    }

                    if (temp.HasValue || usage.HasValue)
                    {
                        return new GpuMetrics(usage, temp, "NVIDIA NVML", 10, true);
                    }
                }
            }
            catch
            {
            }

            return GpuMetrics.Empty;
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return _available;
            _initialized = true;

            try
            {
                _available = nvmlInit_v2() == NvmlSuccess;
            }
            catch
            {
                _available = false;
            }

            return _available;
        }

        public void Dispose()
        {
            if (!_available) return;

            try
            {
                nvmlShutdown();
            }
            catch
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NvmlUtilization
        {
            public uint gpu;
            public uint memory;
        }

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetCount_v2(out uint deviceCount);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint temp);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);
    }

    private sealed class LibreHardwareGpuReader : IDisposable
    {
        private Computer? _computer;
        private bool _initialized;
#if DEBUG
        private readonly Dictionary<string, DateTime> _lastSensorLogUtcByHardware = new(StringComparer.OrdinalIgnoreCase);
#endif

        public GpuMetrics Read()
        {
            try
            {
                if (!EnsureInitialized() || _computer == null) return GpuMetrics.Empty;

                var candidates = new List<GpuMetrics>();
                foreach (var hardware in _computer.Hardware)
                {
                    if (!IsGpuHardware(hardware.HardwareType))
                    {
                        continue;
                    }

                    hardware.Update();
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();
                    }

                    var metrics = ReadHardware(hardware);
                    if (metrics.HasAnyValue)
                    {
                        candidates.Add(metrics);
                    }
                }

                return SelectGpuMetrics(candidates);
            }
            catch
            {
            }

            return GpuMetrics.Empty;
        }

        private bool EnsureInitialized()
        {
            if (_initialized) return _computer != null;
            _initialized = true;

            try
            {
                _computer = new Computer
                {
                    IsGpuEnabled = true
                };
                _computer.Open();
                return true;
            }
            catch
            {
                _computer = null;
                return false;
            }
        }

        private GpuMetrics ReadHardware(IHardware hardware)
        {
            var allSensors = GetSensors(hardware).ToList();
            var loadSensors = allSensors
                .Where(sensor => sensor.SensorType == SensorType.Load)
                .Select(sensor => new GpuSensorCandidate(sensor.Name, sensor.Value))
                .ToList();
            var temperatureSensors = allSensors
                .Where(sensor => sensor.SensorType == SensorType.Temperature)
                .Select(sensor => new GpuSensorCandidate(sensor.Name, sensor.Value))
                .ToList();
            var usageSelection = SelectGpuUsageSensor(loadSensors);
            var temperatureSelection = SelectGpuTemperatureSensor(temperatureSensors);
            LogGpuSensors(hardware, loadSensors, temperatureSensors, usageSelection, temperatureSelection);

            return new GpuMetrics(
                usageSelection?.Value,
                temperatureSelection?.Value,
                hardware.Name,
                GetGpuSourcePriority(hardware.HardwareType),
                hardware.HardwareType is not HardwareType.GpuIntel);
        }

        private static IEnumerable<ISensor> GetSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                yield return sensor;
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                foreach (var sensor in subHardware.Sensors)
                {
                    yield return sensor;
                }
            }
        }

        private static bool IsGpuHardware(HardwareType type) =>
            type is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel;

        private static int GetGpuSourcePriority(HardwareType type) => type switch
        {
            HardwareType.GpuNvidia => 10,
            HardwareType.GpuAmd => 20,
            HardwareType.GpuIntel => 60,
            _ => 100
        };

        [Conditional("DEBUG")]
        private void LogGpuSensors(
            IHardware hardware,
            IReadOnlyCollection<GpuSensorCandidate> loadSensors,
            IReadOnlyCollection<GpuSensorCandidate> temperatureSensors,
            GpuSensorSelection? usageSelection,
            GpuSensorSelection? temperatureSelection)
        {
#if DEBUG
            var key = $"{hardware.HardwareType}:{hardware.Name}";
            var now = DateTime.UtcNow;
            if (_lastSensorLogUtcByHardware.TryGetValue(key, out var last) &&
                now - last < TimeSpan.FromMinutes(5))
            {
                return;
            }

            _lastSensorLogUtcByHardware[key] = now;
            var loadSensorText = loadSensors.Count == 0
                ? "未发现 Load 传感器。"
                : string.Join("; ", loadSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var temperatureSensorText = temperatureSensors.Count == 0
                ? "未发现 Temperature 传感器。"
                : string.Join("; ", temperatureSensors.Select(sensor =>
                    $"{sensor.Name}={(sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "null")}"));
            var selectedUsageText = usageSelection.HasValue
                ? $"{usageSelection.Value.Name}={usageSelection.Value.Value:0.0}"
                : "未选择可用 GPU 使用率传感器。";
            var selectedTemperatureText = temperatureSelection.HasValue
                ? $"{temperatureSelection.Value.Name}={temperatureSelection.Value.Value:0.0}"
                : "未选择可用 GPU 温度传感器。";
            var message =
                $"GPU 硬件：{hardware.Name}; 类型：{hardware.HardwareType}; Load 传感器：{loadSensorText}; " +
                $"Temperature 传感器：{temperatureSensorText}; 最终 GPU 使用率：{selectedUsageText}; 最终 GPU 温度：{selectedTemperatureText}";

            Debug.WriteLine(message);
            Logger.LogInfo(message, "SystemMetricsService.Gpu");
#endif
        }

        public void Dispose()
        {
            try
            {
                _computer?.Close();
            }
            catch
            {
            }
        }
    }

    private readonly struct GpuMetrics
    {
        public static readonly GpuMetrics Empty = new(null, null, "None", 1000, false);

        public GpuMetrics(
            double? gpuUsage,
            double? gpuTemperature,
            string sourceName = "Unknown",
            int sourcePriority = 100,
            bool isDiscrete = true)
        {
            GpuUsage = gpuUsage;
            GpuTemperature = gpuTemperature;
            SourceName = sourceName;
            SourcePriority = sourcePriority;
            IsDiscrete = isDiscrete;
        }

        public double? GpuUsage { get; }
        public double? GpuTemperature { get; }
        public string SourceName { get; }
        public int SourcePriority { get; }
        public bool IsDiscrete { get; }
        public bool HasAnyValue => GpuUsage.HasValue || GpuTemperature.HasValue;
        public bool HasAllValues => GpuUsage.HasValue && GpuTemperature.HasValue;
        public int SelectionRank => (HasAllValues, IsDiscrete) switch
        {
            (true, true) => 0,
            (true, false) => 1,
            (false, true) => 2,
            (false, false) => 3
        };
    }

    private readonly struct CpuMetrics(double? cpuUsage, double? cpuTemperature)
    {
        public static readonly CpuMetrics Empty = new(null, null);
        public double? CpuUsage { get; } = cpuUsage;
        public double? CpuTemperature { get; } = cpuTemperature;
    }

    private readonly struct MemoryMetrics(double? usagePercent, ulong totalBytes, ulong availableBytes, ulong usedBytes)
    {
        public static readonly MemoryMetrics Empty = new(null, 0, 0, 0);
        public double? UsagePercent { get; } = usagePercent;
        public ulong TotalBytes { get; } = totalBytes;
        public ulong AvailableBytes { get; } = availableBytes;
        public ulong UsedBytes { get; } = usedBytes;
    }

    private sealed class NetworkSpeedReader
    {
        private static readonly string[] VirtualAdapterKeywords =
        [
            "virtual",
            "loopback",
            "pseudo",
            "hyper-v",
            "vethernet",
            "vmware",
            "virtualbox",
            "docker",
            "wintun",
            "tap",
            "npcap",
            "vpn",
            "wireguard",
            "tailscale",
            "zerotier",
            "bluetooth"
        ];

        private NetworkSample? _previous;

        public NetworkSpeed Read()
        {
            try
            {
                var current = ReadSample();
                var previous = _previous;
                _previous = current;

                if (previous == null) return NetworkSpeed.Zero;

                var seconds = (current.Timestamp - previous.Timestamp).TotalSeconds;
                var receivedDelta = current.ReceivedBytes - previous.ReceivedBytes;
                var sentDelta = current.SentBytes - previous.SentBytes;

                if (seconds <= 0 || receivedDelta < 0 || sentDelta < 0)
                {
                    return NetworkSpeed.Zero;
                }

                return new NetworkSpeed(receivedDelta / seconds, sentDelta / seconds);
            }
            catch
            {
                return NetworkSpeed.Empty;
            }
        }

        private static NetworkSample ReadSample()
        {
            double received = 0;
            double sent = 0;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsUsableAdapter(adapter)) continue;

                try
                {
                    var stats = adapter.GetIPv4Statistics();
                    received += Math.Max(0, stats.BytesReceived);
                    sent += Math.Max(0, stats.BytesSent);
                }
                catch
                {
                }
            }

            return new NetworkSample(DateTimeOffset.UtcNow, received, sent);
        }

        private static bool IsUsableAdapter(NetworkInterface adapter)
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) return false;

            var type = adapter.NetworkInterfaceType;
            if (type is NetworkInterfaceType.Loopback or
                NetworkInterfaceType.Tunnel or
                NetworkInterfaceType.Unknown)
            {
                return false;
            }

            if (type is not (NetworkInterfaceType.Ethernet or
                NetworkInterfaceType.FastEthernetFx or
                NetworkInterfaceType.FastEthernetT or
                NetworkInterfaceType.GigabitEthernet or
                NetworkInterfaceType.Wireless80211))
            {
                return false;
            }

            var text = $"{adapter.Name} {adapter.Description}".ToLowerInvariant();
            return !VirtualAdapterKeywords.Any(text.Contains);
        }
    }

    private sealed record NetworkSample(DateTimeOffset Timestamp, double ReceivedBytes, double SentBytes);

    private readonly struct NetworkSpeed(double? receivedBytesPerSecond, double? sentBytesPerSecond)
    {
        public static readonly NetworkSpeed Empty = new(null, null);
        public static readonly NetworkSpeed Zero = new(0, 0);
        public double? ReceivedBytesPerSecond { get; } = receivedBytesPerSecond;
        public double? SentBytesPerSecond { get; } = sentBytesPerSecond;
    }
}
