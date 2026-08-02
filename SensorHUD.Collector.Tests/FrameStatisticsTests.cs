using SensorHUD.Collector.Sampling.DxgEtw;

namespace SensorHUD.Collector.Tests;

public sealed class FrameStatisticsTests
{
    [Fact]
    public void CalculatesSteadyFrameIntervals()
    {
        double[] timestamps = [0, 0.01, 0.02, 0.03];

        bool calculated = FrameStatistics.TryCalculate(
            timestamps,
            out FrameStatisticsResult result);

        Assert.True(calculated);
        Assert.Equal(100, result.Fps, 6);
        Assert.Equal(100, result.OnePercentLow, 6);
        Assert.Equal(10, result.Frametime, 6);
    }

    [Fact]
    public void IgnoresDuplicateAndNonFiniteIntervalsWithoutInflatingFps()
    {
        double[] timestamps =
            [0, 0.01, 0.01, double.NaN, 0.02, 0.03];

        bool calculated = FrameStatistics.TryCalculate(
            timestamps,
            out FrameStatisticsResult result);

        Assert.True(calculated);
        Assert.Equal(100, result.Fps, 6);
        Assert.Equal(100, result.OnePercentLow, 6);
        Assert.Equal(10, result.Frametime, 6);
    }

    [Fact]
    public void RejectsWindowWithoutAValidInterval()
    {
        double[] timestamps = [1, 1, double.NaN];

        bool calculated = FrameStatistics.TryCalculate(
            timestamps,
            out FrameStatisticsResult result);

        Assert.False(calculated);
        Assert.Equal(default, result);
    }
}
