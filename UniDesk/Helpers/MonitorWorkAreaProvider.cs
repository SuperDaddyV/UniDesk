using System.Runtime.InteropServices;
using System.Windows;

namespace UniDesk.Helpers;

public readonly record struct PixelRect(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public double CenterX => Left + (Width / 2);

    public double CenterY => Top + (Height / 2);

    public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Top) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
}

public readonly record struct PixelPoint(double X, double Y);

public readonly record struct LogicalRect(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public double CenterX => Left + (Width / 2);

    public double CenterY => Top + (Height / 2);

    public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Top) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
}

public readonly record struct MonitorWorkArea(
    nint Handle,
    PixelRect PixelWorkArea,
    LogicalRect WorkArea,
    double DpiX,
    double DpiY,
    bool IsPrimary);

public static class MonitorWorkAreaGeometry
{
    private const double DefaultDpi = 96;

    public static LogicalRect ConvertPixelsToDip(
        PixelRect pixels,
        double dpiX,
        double dpiY)
    {
        var scaleX = dpiX > 0 && double.IsFinite(dpiX) ? DefaultDpi / dpiX : 1;
        var scaleY = dpiY > 0 && double.IsFinite(dpiY) ? DefaultDpi / dpiY : 1;
        return new LogicalRect(
            pixels.Left * scaleX,
            pixels.Top * scaleY,
            pixels.Width * scaleX,
            pixels.Height * scaleY);
    }

    public static MonitorWorkArea SelectBestPhysical(
        PixelRect requestedWindow,
        IReadOnlyList<MonitorWorkArea> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var candidates = monitors
            .Where(monitor => monitor.PixelWorkArea.IsValid)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new ArgumentException("At least one valid monitor work area is required.", nameof(monitors));
        }

        if (!requestedWindow.IsValid)
        {
            return candidates.FirstOrDefault(monitor => monitor.IsPrimary, candidates[0]);
        }

        var intersecting = candidates
            .Select(monitor => new
            {
                Monitor = monitor,
                IntersectionArea = GetIntersectionArea(requestedWindow, monitor.PixelWorkArea)
            })
            .Where(candidate => candidate.IntersectionArea > 0)
            .OrderByDescending(candidate => candidate.IntersectionArea)
            .ThenByDescending(candidate => candidate.Monitor.IsPrimary)
            .FirstOrDefault();
        if (intersecting != null)
        {
            return intersecting.Monitor;
        }

        return candidates
            .OrderBy(monitor => GetDistanceSquaredToRect(
                requestedWindow.CenterX,
                requestedWindow.CenterY,
                monitor.PixelWorkArea))
            .ThenByDescending(monitor => monitor.IsPrimary)
            .First();
    }

    public static LogicalRect Clamp(LogicalRect requestedWindow, LogicalRect workArea)
    {
        if (!requestedWindow.IsValid)
        {
            throw new ArgumentException("The requested window bounds must be finite and non-empty.", nameof(requestedWindow));
        }
        if (!workArea.IsValid)
        {
            throw new ArgumentException("The monitor work area must be finite and non-empty.", nameof(workArea));
        }

        var width = Math.Min(requestedWindow.Width, workArea.Width);
        var height = Math.Min(requestedWindow.Height, workArea.Height);
        return new LogicalRect(
            Math.Clamp(requestedWindow.Left, workArea.Left, workArea.Right - width),
            Math.Clamp(requestedWindow.Top, workArea.Top, workArea.Bottom - height),
            width,
            height);
    }

    private static double GetIntersectionArea(PixelRect first, PixelRect second)
    {
        var width = Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        var height = Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
        return width * height;
    }

    private static double GetDistanceSquaredToRect(double x, double y, PixelRect rect)
    {
        var deltaX = x < rect.Left
            ? rect.Left - x
            : x > rect.Right
                ? x - rect.Right
                : 0;
        var deltaY = y < rect.Top
            ? rect.Top - y
            : y > rect.Bottom
                ? y - rect.Bottom
                : 0;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}

public interface IMonitorWorkAreaProvider
{
    IReadOnlyList<MonitorWorkArea> GetAll();

    MonitorWorkArea GetForWindow(nint windowHandle);

    MonitorWorkArea GetForPixelRect(PixelRect pixelBounds);

    MonitorWorkArea GetForPixelPoint(PixelPoint pixelPoint);
}

public sealed class Win32MonitorWorkAreaProvider : IMonitorWorkAreaProvider
{
    private const uint MonitorInfoPrimary = 1;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint MonitorDefaultToNearest = 2;
    private const double DefaultDpi = 96;

    public static Win32MonitorWorkAreaProvider Instance { get; } = new();

    private Win32MonitorWorkAreaProvider()
    {
    }

    public IReadOnlyList<MonitorWorkArea> GetAll()
    {
        var monitors = new List<MonitorWorkArea>();
        EnumDisplayMonitors(
            0,
            0,
            (monitor, _, _, _) =>
            {
                if (TryCreateMonitorWorkArea(monitor, out var workArea))
                {
                    monitors.Add(workArea);
                }

                return true;
            },
            0);

        if (monitors.Count == 0)
        {
            monitors.Add(CreateSystemParametersFallback());
        }

        return monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .ThenBy(monitor => monitor.WorkArea.Left)
            .ThenBy(monitor => monitor.WorkArea.Top)
            .ToArray();
    }

    public MonitorWorkArea GetForWindow(nint windowHandle)
    {
        var monitor = MonitorFromWindow(
            windowHandle,
            windowHandle == 0 ? MonitorDefaultToPrimary : MonitorDefaultToNearest);
        return monitor != 0 && TryCreateMonitorWorkArea(monitor, out var workArea)
            ? workArea
            : GetAll()[0];
    }

    public MonitorWorkArea GetForPixelRect(PixelRect pixelBounds)
    {
        if (!pixelBounds.IsValid)
        {
            return GetForWindow(0);
        }

        var nativeRect = new NativeRect
        {
            Left = checked((int)Math.Floor(pixelBounds.Left)),
            Top = checked((int)Math.Floor(pixelBounds.Top)),
            Right = checked((int)Math.Ceiling(pixelBounds.Right)),
            Bottom = checked((int)Math.Ceiling(pixelBounds.Bottom))
        };
        var monitor = MonitorFromRect(ref nativeRect, MonitorDefaultToNearest);
        return monitor != 0 && TryCreateMonitorWorkArea(monitor, out var workArea)
            ? workArea
            : GetAll()[0];
    }

    public MonitorWorkArea GetForPixelPoint(PixelPoint pixelPoint)
    {
        if (!double.IsFinite(pixelPoint.X) || !double.IsFinite(pixelPoint.Y))
        {
            return GetForWindow(0);
        }

        var point = new NativePoint
        {
            X = checked((int)Math.Round(pixelPoint.X)),
            Y = checked((int)Math.Round(pixelPoint.Y))
        };
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        return monitor != 0 && TryCreateMonitorWorkArea(monitor, out var workArea)
            ? workArea
            : GetAll()[0];
    }

    private static bool TryCreateMonitorWorkArea(
        nint monitor,
        out MonitorWorkArea workArea)
    {
        var info = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };
        if (!GetMonitorInfo(monitor, ref info))
        {
            workArea = default;
            return false;
        }

        var dpiX = DefaultDpi;
        var dpiY = DefaultDpi;
        try
        {
            if (GetDpiForMonitor(monitor, 0, out var reportedDpiX, out var reportedDpiY) == 0)
            {
                dpiX = reportedDpiX;
                dpiY = reportedDpiY;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        var pixels = new PixelRect(
            info.WorkArea.Left,
            info.WorkArea.Top,
            info.WorkArea.Right - info.WorkArea.Left,
            info.WorkArea.Bottom - info.WorkArea.Top);
        workArea = new MonitorWorkArea(
            monitor,
            pixels,
            MonitorWorkAreaGeometry.ConvertPixelsToDip(pixels, dpiX, dpiY),
            dpiX,
            dpiY,
            (info.Flags & MonitorInfoPrimary) != 0);
        return true;
    }

    private static MonitorWorkArea CreateSystemParametersFallback()
    {
        var workArea = SystemParameters.WorkArea;
        return new MonitorWorkArea(
            0,
            new PixelRect(workArea.Left, workArea.Top, workArea.Width, workArea.Height),
            new LogicalRect(workArea.Left, workArea.Top, workArea.Width, workArea.Height),
            DefaultDpi,
            DefaultDpi,
            true);
    }

    private delegate bool MonitorEnumProcedure(
        nint monitor,
        nint deviceContext,
        nint monitorRect,
        nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRect,
        MonitorEnumProcedure callback,
        nint data);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
