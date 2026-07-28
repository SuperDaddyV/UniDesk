using System.Diagnostics;

namespace UniDesk.HardwareRepair;

internal sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false);

internal interface IProcessRunner
{
    ProcessExecutionResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout);
}

internal sealed class SystemProcessRunner : IProcessRunner
{
    public ProcessExecutionResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {Path.GetFileName(fileName)}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            return new ProcessExecutionResult(
                -1,
                outputTask.GetAwaiter().GetResult(),
                errorTask.GetAwaiter().GetResult(),
                TimedOut: true);
        }

        return new ProcessExecutionResult(
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }
}
