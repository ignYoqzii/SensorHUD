using System.Buffers;

namespace SensorHUD.Collector.Sampling.DxgEtw;

/// <summary>
/// Performs frame calculations without ETW, process, transport, or UI
/// dependencies.
/// </summary>
internal static class FrameStatistics
{
    public static bool TryCalculate(
        ReadOnlySpan<double> timestamps,
        out FrameStatisticsResult result)
    {
        result = default;
        if (timestamps.Length < 2)
        {
            return false;
        }

        int maximumIntervals = timestamps.Length - 1;
        double[] intervalBuffer =
            ArrayPool<double>.Shared.Rent(maximumIntervals);
        try
        {
            int validCount = 0;
            double intervalSum = 0;
            for (int index = 0; index < maximumIntervals; index++)
            {
                double milliseconds =
                    (timestamps[index + 1] - timestamps[index]) * 1000;
                if (milliseconds <= 0)
                {
                    continue;
                }

                intervalBuffer[validCount++] = milliseconds;
                intervalSum += milliseconds;
            }

            double duration =
                timestamps[^1] - timestamps[0];
            if (validCount == 0 || duration <= 0)
            {
                return false;
            }

            Array.Sort(intervalBuffer, 0, validCount);
            int slowFrameCount = Math.Max(
                1,
                (int)Math.Ceiling(validCount * 0.01));
            double slowSum = 0;
            for (int index = 0; index < slowFrameCount; index++)
            {
                slowSum += intervalBuffer[validCount - 1 - index];
            }

            double slowAverage = slowSum / slowFrameCount;
            result = new FrameStatisticsResult(
                Fps: maximumIntervals / duration,
                OnePercentLow:
                    slowAverage > 0 ? 1000 / slowAverage : 0,
                Frametime: intervalSum / validCount);
            return true;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(intervalBuffer);
        }
    }
}

/// <summary>
/// Calculated frame-rate metrics for one time window.
/// </summary>
internal readonly record struct FrameStatisticsResult(
    double Fps,
    double OnePercentLow,
    double Frametime);
