using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UniDesk.HardwareRepair;

internal interface IApplicationProcess : IDisposable
{
    int Id { get; }
    string ExecutablePath { get; }
    long CreationTime { get; }
    bool HasExited { get; }
    bool WaitForExit(int milliseconds);
}

internal interface IApplicationShutdownPlatform
{
    IReadOnlyList<IApplicationProcess> GetCandidates();
    bool RequestGracefulExit(IReadOnlyList<IApplicationProcess> processes);
}

internal sealed class ApplicationShutdownRunner(IApplicationShutdownPlatform platform)
{
    internal HardwareRepairExitCode Run(string executablePath, bool checkOnly = false)
    {
        if (!Path.IsPathFullyQualified(executablePath) ||
            !string.Equals(Path.GetFileName(executablePath), "UniDesk.exe", StringComparison.OrdinalIgnoreCase))
            return HardwareRepairExitCode.InvalidArguments;

        try
        {
            var target = Path.GetFullPath(executablePath);
            var candidates = platform.GetCandidates();
            try
            {
                var matching = candidates.Where(process => !process.HasExited &&
                    string.Equals(Path.GetFullPath(process.ExecutablePath), target, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matching.Length == 0) return HardwareRepairExitCode.Success;
                if (checkOnly || !platform.RequestGracefulExit(matching))
                    return HardwareRepairExitCode.ApplicationExitFailed;

                var timer = Stopwatch.StartNew();
                foreach (var process in matching)
                    if (!process.WaitForExit((int)Math.Max(0, 10000 - timer.ElapsedMilliseconds)))
                        return HardwareRepairExitCode.ApplicationExitFailed;
            }
            finally
            {
                foreach (var process in candidates) process.Dispose();
            }

            // A launcher or the user may have restarted the app while we were waiting.
            return Run(target, checkOnly: true);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or
                                   UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
        {
            return HardwareRepairExitCode.ApplicationExitFailed;
        }
    }
}

internal sealed class WindowsApplicationShutdownPlatform : IApplicationShutdownPlatform
{
    public IReadOnlyList<IApplicationProcess> GetCandidates() =>
        Process.GetProcessesByName("UniDesk").Select(process => (IApplicationProcess)new ApplicationProcess(process)).ToArray();

    public bool RequestGracefulExit(IReadOnlyList<IApplicationProcess> processes)
    {
        var identities = processes.Where(process => !process.HasExited).Select(process => new UniqueProcess
        {
            ProcessId = (uint)process.Id,
            CreationTime = process.CreationTime
        }).ToArray();
        if (identities.Length == 0) return true;
        if (RmStartSession(out var session, 0, new StringBuilder(33)) != 0) return false;
        try
        {
            return RmRegisterResources(session, 0, IntPtr.Zero, (uint)identities.Length, identities, 0, IntPtr.Zero) == 0 &&
                   RmShutdown(session, 0, IntPtr.Zero) == 0; // Never RmForceShutdown.
        }
        finally { RmEndSession(session); }
    }

    private sealed class ApplicationProcess(Process process) : IApplicationProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public long CreationTime => process.StartTime.ToUniversalTime().ToFileTimeUtc();
        public string ExecutablePath
        {
            get
            {
                var path = new StringBuilder(32768);
                var size = path.Capacity;
                // SafeHandle remains owned by Process until the entire operation completes.
                if (!QueryFullProcessImageName(process.SafeHandle, 0, path, ref size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return path.ToString();
            }
        }
        public bool WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);
        public void Dispose() => process.Dispose();
    }

    // FILETIME is DWORD-aligned, so its two halves must immediately follow the PID.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct UniqueProcess { public uint ProcessId; public long CreationTime; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint session, uint flags, StringBuilder sessionKey);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint session, uint fileCount, IntPtr files,
        uint processCount, [In] UniqueProcess[] processes, uint serviceCount, IntPtr services);
    [DllImport("rstrtmgr.dll")]
    private static extern int RmShutdown(uint session, uint flags, IntPtr callback);
    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint session);
}
