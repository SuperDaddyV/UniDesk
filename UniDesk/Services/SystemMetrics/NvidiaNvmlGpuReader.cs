using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UniDesk.Services;

public sealed class NvidiaNvmlGpuReader : IDisposable
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

            var candidates = new List<GpuMetrics>();
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
                    SensorSelection.IsValidTemperature(rawTemp))
                {
                    temp = rawTemp;
                }

                if (nvmlDeviceGetUtilizationRates(device, out var utilization) == NvmlSuccess &&
                    SensorSelection.IsValidPercentage(utilization.gpu))
                {
                    usage = utilization.gpu;
                }

                if (temp.HasValue || usage.HasValue)
                {
                    var deviceId = TryGetDeviceLuid(device);
                    var deviceName = TryGetDeviceName(device) ?? "NVIDIA NVML";
                    candidates.Add(new GpuMetrics(
                        usage,
                        temp,
                        deviceName,
                        10,
                        true,
                        usageSource: usage.HasValue ? "NVIDIA NVML" : null,
                        usageDeviceId: usage.HasValue ? deviceId : null,
                        temperatureSource: temp.HasValue ? "NVIDIA NVML" : null,
                        temperatureDeviceId: temp.HasValue ? deviceId : null));
                }
            }

            return GpuMetricsReader.SelectMetrics(candidates);
        }
        catch
        {
        }

        return GpuMetrics.Empty;
    }

    public static string? FormatDeviceLuid(byte[] luid)
    {
        if (luid.Length < 8)
        {
            return null;
        }

        var low = BitConverter.ToUInt32(luid, 0);
        var high = BitConverter.ToUInt32(luid, 4);
        return $"luid:{high:X8}:{low:X8}";
    }

    private static string? TryGetDeviceLuid(IntPtr device)
    {
        try
        {
            var luid = new byte[8];
            uint nodeMask = 0;
            return nvmlDeviceGetLuid(device, luid, ref nodeMask) == NvmlSuccess
                ? FormatDeviceLuid(luid)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetDeviceName(IntPtr device)
    {
        try
        {
            var buffer = new byte[96];
            if (nvmlDeviceGetName(device, buffer, (uint)buffer.Length) != NvmlSuccess)
            {
                return null;
            }

            var length = Array.IndexOf(buffer, (byte)0);
            return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length).Trim();
        }
        catch
        {
            return null;
        }
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

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetLuid(
        IntPtr device,
        [Out] byte[] luid,
        ref uint deviceNodeMask);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetName(IntPtr device, [Out] byte[] name, uint length);
}
