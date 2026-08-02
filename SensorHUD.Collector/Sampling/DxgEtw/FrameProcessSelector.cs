namespace SensorHUD.Collector.Sampling.DxgEtw;

/// <summary>
/// Selects a presenting process while keeping an established foreground
/// renderer stable through short periods without enough frames.
/// </summary>
internal sealed class FrameProcessSelector(Predicate<int> isExcluded)
{
    private int _selectedProcessId;
    private int _selectionForegroundProcessId;

    public int? ChooseTargetProcess(
        int foregroundProcessId,
        Dictionary<int, Queue<double>> presentsByProcess,
        double cutoff,
        out int recentCount)
    {
        recentCount = 0;
        bool foregroundExcluded =
            foregroundProcessId <= 0 || isExcluded(foregroundProcessId);
        if (foregroundProcessId > 0 &&
            !foregroundExcluded &&
            presentsByProcess.TryGetValue(
                foregroundProcessId,
                out Queue<double>? foregroundQueue))
        {
            int foregroundCount = CountRecent(foregroundQueue, cutoff);
            if (foregroundCount >=
                FrameCaptureDefaults.MinimumPresentationCount)
            {
                _selectedProcessId = foregroundProcessId;
                _selectionForegroundProcessId = foregroundProcessId;
                recentCount = foregroundCount;
                return foregroundProcessId;
            }
        }

        // Keep a quiet renderer while retained events still tie it to the same
        // foreground window. Returning its identity with zero frames lets the
        // provider bridge the gap without changing the selected process.
        if (_selectedProcessId > 0 &&
            (foregroundProcessId == _selectionForegroundProcessId ||
                foregroundExcluded) &&
            presentsByProcess.TryGetValue(
                _selectedProcessId,
                out Queue<double>? selectedQueue))
        {
            int selectedCount = CountRecent(selectedQueue, cutoff);
            recentCount = selectedCount >=
                FrameCaptureDefaults.MinimumPresentationCount
                    ? selectedCount
                    : 0;
            return _selectedProcessId;
        }

        int? bestProcess = null;
        int bestCount =
            FrameCaptureDefaults.MinimumPresentationCount - 1;
        foreach ((int processId, Queue<double> queue) in presentsByProcess)
        {
            if (isExcluded(processId))
            {
                continue;
            }

            int count = CountRecent(queue, cutoff);
            if (count > bestCount)
            {
                bestCount = count;
                bestProcess = processId;
            }
        }

        recentCount = bestProcess is null ? 0 : bestCount;
        _selectedProcessId = bestProcess ?? 0;
        _selectionForegroundProcessId =
            bestProcess is null ? 0 : foregroundProcessId;
        return bestProcess;
    }

    private static int CountRecent(
        Queue<double> timestamps,
        double cutoff)
    {
        int count = 0;
        foreach (double timestamp in timestamps)
        {
            if (timestamp >= cutoff)
            {
                count++;
            }
        }

        return count;
    }
}
