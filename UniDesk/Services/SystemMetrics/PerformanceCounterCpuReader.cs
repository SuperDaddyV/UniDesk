using System.Diagnostics;

namespace UniDesk.Services;

public sealed class PerformanceCounterCpuReader : IDisposable
{
    private readonly PerformanceCounter _counter = new("Processor", "% Processor Time", "_Total");

    public PerformanceCounterCpuReader()
    {
        try { _counter.NextValue(); } catch { }
    }

    public double? ReadUsage()
    {
        try
        {
            return SensorSelection.NormalizePercentage(_counter.NextValue());
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _counter.Dispose();
}
