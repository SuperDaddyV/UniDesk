using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class HardwareMetricResilienceTests
{
    [Fact]
    public void ReaderFailureBackoff_ShouldPauseAfterThreeFailuresAndRetryAfterDelay()
    {
        var now = DateTimeOffset.Parse("2026-07-12T00:00:00Z");
        var backoff = new ReaderFailureBackoff(TimeSpan.FromMinutes(5), () => now);

        backoff.RecordFailure("first");
        backoff.RecordFailure("second");
        Assert.True(backoff.CanAttempt);

        backoff.RecordFailure("third");
        Assert.False(backoff.CanAttempt);
        Assert.Equal(3, backoff.ConsecutiveFailures);
        Assert.Equal("third", backoff.LastFailureReason);

        now = now.AddMinutes(5);
        Assert.True(backoff.CanAttempt);
    }

    [Fact]
    public void ReaderFailureBackoff_SuccessShouldResetFailureState()
    {
        var backoff = new ReaderFailureBackoff(TimeSpan.FromMinutes(5));
        backoff.RecordFailure("failed");

        backoff.RecordSuccess();

        Assert.True(backoff.CanAttempt);
        Assert.Equal(0, backoff.ConsecutiveFailures);
        Assert.Null(backoff.LastFailureReason);
        Assert.Null(backoff.NextRetryAtUtc);
    }

    [Fact]
    public void ReaderFailureBackoff_PermanentFailureShouldNeverRetry()
    {
        var backoff = new ReaderFailureBackoff(TimeSpan.FromMinutes(5));

        backoff.RecordPermanentFailure("missing provider");

        Assert.False(backoff.CanAttempt);
        Assert.True(backoff.IsPermanentlyUnavailable);
        Assert.Equal("missing provider", backoff.LastFailureReason);
    }

    [Fact]
    public void TemperatureSpikeFilter_ShouldHoldOneOffSpikeAndAcceptSustainedTransition()
    {
        var filter = new TemperatureSpikeFilter();

        Assert.Equal(50, filter.Apply(50, "CPU Package"));
        Assert.Equal(50, filter.Apply(95, "CPU Package"));
        Assert.Equal(50, filter.Apply(96, "CPU Package"));
        Assert.Equal(94, filter.Apply(94, "CPU Package"));
    }

    [Fact]
    public void TemperatureSpikeFilter_SourceChangeShouldAcceptNewSourceImmediately()
    {
        var filter = new TemperatureSpikeFilter();
        _ = filter.Apply(50, "CPU Package");

        var result = filter.Apply(95, "ACPI TZ00");

        Assert.Equal(95, result);
    }

    [Fact]
    public void GpuMetrics_ShouldKeepUsageAndTemperatureIdentitySeparate()
    {
        var metrics = new GpuMetrics(
            40,
            65,
            "Combined",
            10,
            true,
            usageSource: "Windows GPU Engine",
            usageDeviceId: "luid:00000000:00012844",
            temperatureSource: "NVIDIA NVML",
            temperatureDeviceId: "luid:00000000:00012844");

        Assert.Equal("Windows GPU Engine", metrics.UsageSource);
        Assert.Equal("NVIDIA NVML", metrics.TemperatureSource);
        Assert.Equal(metrics.UsageDeviceId, metrics.TemperatureDeviceId);
        Assert.Equal(HardwareMetricAvailability.Available, metrics.UsageAvailability);
        Assert.Equal(HardwareMetricAvailability.Available, metrics.TemperatureAvailability);
    }

    [Fact]
    public void MissingMetric_ShouldPreserveUnavailableReason()
    {
        var metrics = new CpuMetrics(
            20,
            null,
            temperatureAvailability: HardwareMetricAvailability.NeedsElevation,
            temperatureReason: "sensor access denied");

        Assert.Equal(HardwareMetricAvailability.NeedsElevation, metrics.TemperatureAvailability);
        Assert.Equal("sensor access denied", metrics.TemperatureReason);
    }
}
