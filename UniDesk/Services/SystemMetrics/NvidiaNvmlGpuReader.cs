using System.IO;
using System.Runtime.InteropServices;

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
