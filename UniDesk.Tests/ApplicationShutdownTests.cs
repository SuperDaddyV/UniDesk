using UniDesk.HardwareRepair;

namespace UniDesk.Tests;

public sealed class ApplicationShutdownTests
{
    private const string Target = @"C:\Apps\UniDesk\UniDesk.exe";

    [Fact]
    public void Shutdown_OnlyRequestsExactExecutablePath_AndDisposesAllHandles()
    {
        var owned = new Candidate(Target.ToUpperInvariant());
        var unrelated = new Candidate(@"D:\Portable\UniDesk.exe");
        var platform = new Platform([owned, unrelated], [unrelated]);
        Assert.Equal(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target));
        Assert.Same(owned, Assert.Single(platform.Requested));
        Assert.Equal(1, owned.WaitCount);
        Assert.True(owned.Disposed);
        Assert.True(unrelated.Disposed);
    }

    [Theory]
    [InlineData("UniDesk.exe")]
    [InlineData(@"C:\Apps\Other.exe")]
    [InlineData("")]
    public void Shutdown_RejectsInvalidTargetBeforeEnumeration(string path)
    {
        var platform = new Platform([]);
        Assert.Equal(HardwareRepairExitCode.InvalidArguments, new ApplicationShutdownRunner(platform).Run(path));
        Assert.Equal(0, platform.Enumerations);
    }

    [Fact]
    public void CheckOnly_NeverSendsAnExitRequest()
    {
        var platform = new Platform([new Candidate(Target)]);
        Assert.NotEqual(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target, checkOnly: true));
        Assert.Empty(platform.Requested);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Shutdown_RefusalOrTimeoutStopsUpdate(bool acceptsRequest, bool exits)
    {
        var candidate = new Candidate(Target) { Exits = exits };
        var platform = new Platform([candidate]) { AcceptsRequest = acceptsRequest };
        Assert.NotEqual(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target));
        Assert.True(candidate.Disposed);
    }

    [Fact]
    public void Shutdown_RestartDuringExitStopsUpdate()
    {
        var platform = new Platform([new Candidate(Target)], [new Candidate(Target)]);
        Assert.NotEqual(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target));
        Assert.Equal(2, platform.Enumerations);
    }

    [Fact]
    public void Shutdown_InaccessibleCandidateFailsWithoutSendingRequests()
    {
        var owned = new Candidate(Target);
        var inaccessible = new Candidate(Target) { Inaccessible = true };
        var platform = new Platform([owned, inaccessible]);
        Assert.NotEqual(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target));
        Assert.Empty(platform.Requested);
        Assert.True(owned.Disposed);
        Assert.True(inaccessible.Disposed);
    }

    [Fact]
    public void Shutdown_AlreadyExitedCandidateDoesNotBlockUpdate()
    {
        var candidate = new Candidate(Target) { HasExited = true };
        var platform = new Platform([candidate], []);
        Assert.Equal(HardwareRepairExitCode.Success, new ApplicationShutdownRunner(platform).Run(Target));
        Assert.Empty(platform.Requested);
        Assert.True(candidate.Disposed);
    }

    private sealed class Candidate(string path) : IApplicationProcess
    {
        public int Id => 42;
        public long CreationTime => 1234;
        public string ExecutablePath => Inaccessible ? throw new UnauthorizedAccessException() : path;
        public bool Inaccessible { get; init; }
        public bool HasExited { get; set; }
        public bool Exits { get; init; } = true;
        public bool Disposed { get; private set; }
        public int WaitCount { get; private set; }
        public bool WaitForExit(int milliseconds) { Assert.InRange(milliseconds, 0, 10000); WaitCount++; return HasExited = Exits; }
        public void Dispose() => Disposed = true;
    }

    private sealed class Platform(params IReadOnlyList<IApplicationProcess>[] snapshots) : IApplicationShutdownPlatform
    {
        public int Enumerations { get; private set; }
        public bool AcceptsRequest { get; init; } = true;
        public List<IApplicationProcess> Requested { get; } = [];
        public IReadOnlyList<IApplicationProcess> GetCandidates() => snapshots[Math.Min(Enumerations++, snapshots.Length - 1)];
        public bool RequestGracefulExit(IReadOnlyList<IApplicationProcess> processes)
        {
            Requested.AddRange(processes);
            return AcceptsRequest;
        }
    }
}
