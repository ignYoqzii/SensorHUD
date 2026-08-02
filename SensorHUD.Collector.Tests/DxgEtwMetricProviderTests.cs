using SensorHUD.Collector.Sampling.DxgEtw;
using SensorHUD.Core.Metrics;

namespace SensorHUD.Collector.Tests;

public sealed class DxgEtwMetricProviderTests
{
    [Fact]
    public void KeepsLastFrameReadingsDuringBriefPresentationGap()
    {
        StubFrameCaptureSource source = new(
            Window(100, 1_000, 60),
            EmptyWindow(100));
        AdjustableTimeProvider clock = new();
        using DxgEtwMetricProvider provider = new(source, clock);

        MetricSampleResult initial = MetricSampleTestHarness.Sample(provider);
        clock.Advance(TimeSpan.FromSeconds(3));
        MetricSampleResult duringGap = MetricSampleTestHarness.Sample(provider);

        Assert.Equal(3, initial.Readings.Count);
        Assert.Equal(3, duringGap.Readings.Count);
        Assert.Equal(
            Value(initial, MetricRegistry.Fps),
            Value(duringGap, MetricRegistry.Fps));
        Assert.Equal(
            Value(initial, MetricRegistry.OnePercentLow),
            Value(duringGap, MetricRegistry.OnePercentLow));
        Assert.Equal(
            Value(initial, MetricRegistry.Frametime),
            Value(duringGap, MetricRegistry.Frametime));
    }

    [Fact]
    public void DropsFrameReadingsAfterPresentationGapExpires()
    {
        StubFrameCaptureSource source = new(
            Window(100, 1_000, 60),
            EmptyWindow(100));
        AdjustableTimeProvider clock = new();
        using DxgEtwMetricProvider provider = new(source, clock);

        Assert.Equal(
            3,
            MetricSampleTestHarness.Sample(provider).Readings.Count);
        clock.Advance(
            FrameCaptureDefaults.ReadingContinuityWindow +
            TimeSpan.FromMilliseconds(1));

        MetricSampleResult expired = MetricSampleTestHarness.Sample(provider);

        Assert.Empty(expired.Readings);
    }

    [Fact]
    public void DoesNotInventReadingsBeforeFirstValidFrameWindow()
    {
        StubFrameCaptureSource source = new(EmptyWindow(100));
        AdjustableTimeProvider clock = new();
        using DxgEtwMetricProvider provider = new(source, clock);

        MetricSampleResult result = MetricSampleTestHarness.Sample(provider);

        Assert.Empty(result.Readings);
    }

    [Fact]
    public void DoesNotCarryReadingsAcrossProcessSwitch()
    {
        StubFrameCaptureSource source = new(
            Window(100, 1_000, 60),
            EmptyWindow(200));
        AdjustableTimeProvider clock = new();
        using DxgEtwMetricProvider provider = new(source, clock);

        Assert.Equal(
            3,
            MetricSampleTestHarness.Sample(provider).Readings.Count);

        MetricSampleResult switched = MetricSampleTestHarness.Sample(provider);

        Assert.Empty(switched.Readings);
    }

    private static FrameCaptureWindow Window(
        int processId,
        double lastTimestamp,
        int framesPerSecond)
    {
        const int intervalCount = 60;
        double interval = 1d / framesPerSecond;
        double[] timestamps = new double[intervalCount + 1];
        double firstTimestamp = lastTimestamp - intervalCount * interval;
        for (int index = 0; index < timestamps.Length; index++)
        {
            timestamps[index] = firstTimestamp + index * interval;
        }

        return new FrameCaptureWindow(processId, timestamps);
    }

    private static FrameCaptureWindow EmptyWindow(int processId) =>
        new(processId, []);

    private static double Value(
        MetricSampleResult result,
        string metricId) =>
        Assert.Single(
            result.Readings,
            reading => reading.MetricId == metricId).Value;

    private sealed class StubFrameCaptureSource(
        params FrameCaptureWindow[] windows) : IFrameCaptureSource
    {
        private int _index;

        public FrameCaptureSubsystemHealth CaptureHealth => new(true);

        public FrameCaptureWindow Capture()
        {
            int index = Math.Min(_index, windows.Length - 1);
            _index++;
            return windows[index];
        }
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
