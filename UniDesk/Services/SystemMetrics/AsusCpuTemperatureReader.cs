using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UniDesk.Services;

public sealed class AsusCpuTemperatureReader
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
