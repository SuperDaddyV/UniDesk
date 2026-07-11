using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UniDesk.Helpers;

public enum BackdropKind
{
    MainWindow = 2,
    TransientWindow = 3
}

public static class BackdropMaterialService
{
    private const int DwmwaSystemBackdropType = 38;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);

    public static bool IsSupported(Version version) =>
        version.Major >= 10 && version.Build >= 22621;

    public static bool Apply(Window window, BackdropKind kind)
    {
        if (!IsSupported(Environment.OSVersion.Version))
        {
            return false;
        }

        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            var backdrop = (int)kind;
            return DwmSetWindowAttribute(
                handle,
                DwmwaSystemBackdropType,
                ref backdrop,
                sizeof(int)) >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
