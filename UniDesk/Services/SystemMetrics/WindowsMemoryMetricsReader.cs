using System.Diagnostics;
using System.Runtime.InteropServices;
using UniDesk.Helpers;

namespace UniDesk.Services;

public sealed class WindowsMemoryMetricsReader : IMemoryMetricsReader
{
#if DEBUG
    private DateTime _lastLogUtc = DateTime.MinValue;
#endif

    public MemoryMetrics Read()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status) || status.ullTotalPhys == 0)
        {
            Log("GlobalMemoryStatusEx", status.ullTotalPhys, status.ullAvailPhys, null);
            return ReadFromPerformanceInfo();
        }

        return CreateAndLog("GlobalMemoryStatusEx", status.ullTotalPhys, status.ullAvailPhys);
    }

    public static MemoryMetrics CreateMetrics(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0)
        {
            return MemoryMetrics.Empty;
        }

        var normalizedAvailable = Math.Min(availableBytes, totalBytes);
        var used = totalBytes - normalizedAvailable;
        return new MemoryMetrics(
            SensorSelection.NormalizePercentage(used / (double)totalBytes * 100d),
            totalBytes,
            normalizedAvailable,
            used);
    }

    private MemoryMetrics ReadFromPerformanceInfo()
    {
        if (!GetPerformanceInfo(out var info, (uint)Marshal.SizeOf<PerformanceInformation>()) ||
            info.PhysicalTotal == UIntPtr.Zero ||
            info.PageSize == UIntPtr.Zero)
        {
            Log("GetPerformanceInfo", 0, 0, null);
            return MemoryMetrics.Empty;
        }

        var pageSize = info.PageSize.ToUInt64();
        return CreateAndLog(
            "GetPerformanceInfo",
            info.PhysicalTotal.ToUInt64() * pageSize,
            info.PhysicalAvailable.ToUInt64() * pageSize);
    }

    private MemoryMetrics CreateAndLog(string source, ulong totalBytes, ulong availableBytes)
    {
        var metrics = CreateMetrics(totalBytes, availableBytes);
        Log(source, metrics.TotalBytes, metrics.AvailableBytes, metrics.UsagePercent);
        return metrics;
    }

    [Conditional("DEBUG")]
    private void Log(string source, ulong totalBytes, ulong availableBytes, double? usagePercent)
    {
#if DEBUG
        var now = DateTime.UtcNow;
        if (now - _lastLogUtc < TimeSpan.FromMinutes(5)) return;
        _lastLogUtc = now;
        var usedBytes = totalBytes >= availableBytes ? totalBytes - availableBytes : 0;
        var message =
            $"内存来源：{source}; 总内存={totalBytes}; 可用内存={availableBytes}; 已用内存={usedBytes}; 使用率={(usagePercent.HasValue ? usagePercent.Value.ToString("0.0") : "null")}";
        Debug.WriteLine(message);
        Logger.LogInfo(message, "SystemMetricsService.Memory");
#endif
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation performanceInformation, uint cb);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
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
}
