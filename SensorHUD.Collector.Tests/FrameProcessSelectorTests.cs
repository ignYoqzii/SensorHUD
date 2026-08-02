using SensorHUD.Collector.Sampling.DxgEtw;

namespace SensorHUD.Collector.Tests;

public sealed class FrameProcessSelectorTests
{
    [Fact]
    public void KeepsQuietRendererSelectedUntilForegroundChanges()
    {
        FrameProcessSelector selector = new(static _ => false);
        Dictionary<int, Queue<double>> presentations = new()
        {
            [100] = new Queue<double>([10, 10.1, 10.2]),
        };

        int? initial = selector.ChooseTargetProcess(
            foregroundProcessId: 10,
            presentations,
            cutoff: 10,
            out int initialCount);

        presentations[200] = new Queue<double>([11, 11.1, 11.2, 11.3]);
        int? quiet = selector.ChooseTargetProcess(
            foregroundProcessId: 10,
            presentations,
            cutoff: 11,
            out int quietCount);
        int? changedForeground = selector.ChooseTargetProcess(
            foregroundProcessId: 200,
            presentations,
            cutoff: 11,
            out int changedCount);

        Assert.Equal(100, initial);
        Assert.Equal(3, initialCount);
        Assert.Equal(100, quiet);
        Assert.Equal(0, quietCount);
        Assert.Equal(200, changedForeground);
        Assert.Equal(4, changedCount);
    }

    [Fact]
    public void PrefersPresentingForegroundProcessOverPreviousFallback()
    {
        FrameProcessSelector selector = new(static _ => false);
        Dictionary<int, Queue<double>> presentations = new()
        {
            [100] = new Queue<double>([10, 10.1, 10.2, 10.3]),
        };
        _ = selector.ChooseTargetProcess(
            foregroundProcessId: 10,
            presentations,
            cutoff: 10,
            out _);
        presentations[10] = new Queue<double>([11, 11.1, 11.2]);

        int? selected = selector.ChooseTargetProcess(
            foregroundProcessId: 10,
            presentations,
            cutoff: 11,
            out int count);

        Assert.Equal(10, selected);
        Assert.Equal(3, count);
    }

    [Fact]
    public void IgnoresTransientExcludedForegroundWindow()
    {
        FrameProcessSelector selector = new(static processId => processId == 50);
        Dictionary<int, Queue<double>> presentations = new()
        {
            [100] = new Queue<double>([10, 10.1, 10.2]),
        };
        _ = selector.ChooseTargetProcess(
            foregroundProcessId: 10,
            presentations,
            cutoff: 10,
            out _);
        presentations[200] = new Queue<double>([11, 11.1, 11.2, 11.3]);

        int? selected = selector.ChooseTargetProcess(
            foregroundProcessId: 50,
            presentations,
            cutoff: 11,
            out int count);

        Assert.Equal(100, selected);
        Assert.Equal(0, count);
    }
}
