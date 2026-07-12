using UniDesk.Services;

namespace UniDesk.Tests;

public class GpuEngineCounterReaderTests
{
    [Fact]
    public void TryParseInstanceName_ShouldExtractLuidPhysicalAdapterAndEngine()
    {
        var parsed = GpuEngineCounterReader.TryParseInstanceName(
            "pid_1716_luid_0x00000000_0x00012844_phys_0_eng_3_engtype_copy",
            out var identity);

        Assert.True(parsed);
        Assert.Equal("luid:00000000:00012844", identity.DeviceId);
        Assert.Equal(0, identity.PhysicalAdapter);
        Assert.Equal(3, identity.EngineIndex);
        Assert.Equal("copy", identity.EngineType);
    }

    [Fact]
    public void AggregateSamples_ShouldSumProcessesPerEngineAndSelectBusiestEnginePerAdapter()
    {
        var adapters = GpuEngineCounterReader.AggregateSamples(
        [
            new("pid_1_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d", 60),
            new("pid_2_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d", 55),
            new("pid_1_luid_0x00000000_0x00000001_phys_0_eng_1_engtype_copy", 25),
            new("pid_3_luid_0x00000000_0x00000002_phys_0_eng_0_engtype_3d", 48)
        ]);

        Assert.Equal(2, adapters.Count);
        Assert.Equal(100, adapters.Single(item => item.UsageDeviceId == "luid:00000000:00000001").GpuUsage);
        Assert.Equal(48, adapters.Single(item => item.UsageDeviceId == "luid:00000000:00000002").GpuUsage);
    }

    [Fact]
    public void AggregateSamples_ShouldSkipMalformedAndInvalidSamples()
    {
        var adapters = GpuEngineCounterReader.AggregateSamples(
        [
            new("not-a-gpu-engine", 90),
            new("pid_1_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d", double.NaN),
            new("pid_2_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d", -1)
        ]);

        Assert.Empty(adapters);
    }

    [Fact]
    public void Read_ShouldWarmNewCountersBeforePublishingUsage()
    {
        using var source = new FakeCounterSource(
            "pid_1_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d",
            [0, 42]);
        using var reader = new GpuEngineCounterReader(source, TimeSpan.FromMinutes(5));

        var warmup = reader.Read();
        var sampled = reader.Read();

        Assert.Null(warmup.GpuUsage);
        Assert.Equal(42, sampled.GpuUsage);
        Assert.Equal("Windows GPU Engine", sampled.UsageSource);
    }

    [Fact]
    public void Read_ShouldBackOffAfterRepeatedCategoryFailures()
    {
        using var source = new ThrowingCounterSource();
        using var reader = new GpuEngineCounterReader(source, TimeSpan.FromMinutes(5));

        _ = reader.Read();
        _ = reader.Read();
        _ = reader.Read();
        _ = reader.Read();

        Assert.Equal(3, source.ReadCount);
        Assert.False(reader.DiagnosticStatus.CanAttempt);
        Assert.Equal(3, reader.DiagnosticStatus.ConsecutiveFailures);
    }

    [Fact]
    public void NvidiaLuid_ShouldUseTheSameCanonicalIdentityAsGpuEngine()
    {
        var luid = new byte[] { 0x44, 0x28, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

        var deviceId = NvidiaNvmlGpuReader.FormatDeviceLuid(luid);

        Assert.Equal("luid:00000000:00012844", deviceId);
    }

    [Fact]
    public void AmdPciIdentity_ShouldRemainAdapterSpecific()
    {
        var deviceId = AmdAdlGpuReader.FormatDeviceId(0x1002, 1, 0, 0);

        Assert.Equal("pci:1002:01:00:0", deviceId);
    }

    [Fact]
    public void Read_ShouldIsolateOneCounterCreationFailure()
    {
        using var source = new PartiallyFailingCounterSource();
        using var reader = new GpuEngineCounterReader(source, TimeSpan.FromMinutes(5));

        _ = reader.Read();
        var sampled = reader.Read();

        Assert.Equal(50, sampled.GpuUsage);
        Assert.Equal(2, source.SuccessfulCounterReads);
    }

    private sealed class FakeCounterSource : IGpuEngineCounterSource
    {
        private readonly string _instanceName;
        private readonly Queue<double> _values;

        public FakeCounterSource(string instanceName, IEnumerable<double> values)
        {
            _instanceName = instanceName;
            _values = new Queue<double>(values);
        }

        public IReadOnlyList<string> GetInstanceNames() => [_instanceName];
        public IGpuEngineCounter CreateCounter(string instanceName) => new FakeCounter(_values);
        public void Dispose() { }
    }

    private sealed class FakeCounter(Queue<double> values) : IGpuEngineCounter
    {
        public double NextValue() => values.Dequeue();
        public void Dispose() { }
    }

    private sealed class ThrowingCounterSource : IGpuEngineCounterSource
    {
        public int ReadCount { get; private set; }

        public IReadOnlyList<string> GetInstanceNames()
        {
            ReadCount++;
            throw new InvalidOperationException("counter category unavailable");
        }

        public IGpuEngineCounter CreateCounter(string instanceName) => throw new NotSupportedException();
        public void Dispose() { }
    }

    private sealed class PartiallyFailingCounterSource : IGpuEngineCounterSource
    {
        private const string Failing =
            "pid_1_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3d";
        private const string Working =
            "pid_2_luid_0x00000000_0x00000002_phys_0_eng_0_engtype_3d";
        private readonly Queue<double> _values = new([0, 50]);

        public int SuccessfulCounterReads { get; private set; }
        public IReadOnlyList<string> GetInstanceNames() => [Failing, Working];

        public IGpuEngineCounter CreateCounter(string instanceName)
        {
            if (instanceName == Failing)
            {
                throw new InvalidOperationException("one counter failed");
            }

            return new CountingCounter(_values, () => SuccessfulCounterReads++);
        }

        public void Dispose() { }

        private sealed class CountingCounter(Queue<double> values, Action onRead) : IGpuEngineCounter
        {
            public double NextValue()
            {
                onRead();
                return values.Dequeue();
            }

            public void Dispose() { }
        }
    }
}
