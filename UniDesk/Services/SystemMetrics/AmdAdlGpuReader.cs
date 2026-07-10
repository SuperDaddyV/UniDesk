using System.Runtime.InteropServices;

namespace UniDesk.Services;

public sealed class AmdAdlGpuReader
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
                        SensorSelection.IsValidTemperature(data.sensors[SensorGpuTemperatureEdge].value))
                    {
                        temp = data.sensors[SensorGpuTemperatureEdge].value;
                    }

                    if (data.sensors[SensorGpuActivity].supported != 0 &&
                        SensorSelection.IsValidPercentage(data.sensors[SensorGpuActivity].value))
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
