using System.Net;
using System.Net.NetworkInformation;

namespace SensorHUD.Collector.Sampling.Icmp;

/// <summary>
/// Performs bounded, non-blocking ICMP probes against public anycast
/// endpoints and retains rolling Internet-path latency and loss statistics.
/// </summary>
internal sealed class InternetPathProbe : IDisposable
{
    private const int MaximumResults = 20;
    private const int PingTimeoutMilliseconds = 800;
    private const int FailuresBeforeReselection = 3;

    private static readonly IPAddress[] ProbeAddresses =
    [
        IPAddress.Parse("1.1.1.1"),
        IPAddress.Parse("8.8.8.8"),
    ];

    private readonly Lock _sync = new();
    private readonly ProbeTargetState[] _targets =
        [.. ProbeAddresses.Select(address =>
            new ProbeTargetState(address, MaximumResults))];

    private int _selectedTargetIndex = -1;
    private bool _probeInProgress;
    private bool _disposed;

    /// <summary>
    /// Starts one asynchronous probe when no previous probe is in progress.
    /// Both targets are sampled only during initial selection or failover.
    /// </summary>
    public void StartProbe()
    {
        bool selectTarget;
        int selectedTargetIndex;
        lock (_sync)
        {
            if (_disposed || _probeInProgress)
            {
                return;
            }

            selectTarget = _selectedTargetIndex < 0 ||
                _targets[_selectedTargetIndex].ConsecutiveFailures >=
                    FailuresBeforeReselection;
            selectedTargetIndex = _selectedTargetIndex;
            _probeInProgress = true;
        }

        _ = ProbeAsync(selectTarget, selectedTargetIndex);
    }

    /// <summary>
    /// Copies statistics for the currently selected public endpoint without
    /// waiting for network I/O.
    /// </summary>
    public InternetPathStatistics Capture()
    {
        lock (_sync)
        {
            if (_selectedTargetIndex < 0)
            {
                return new InternetPathStatistics(null, null);
            }

            ProbeTargetState target = _targets[_selectedTargetIndex];
            int totalCount = target.Results.Count;
            if (target.SuccessfulCount == 0)
            {
                return new InternetPathStatistics(null, null);
            }

            double ping =
                (double)target.RoundtripTotal / target.SuccessfulCount;
            double packetLoss =
                (totalCount - target.SuccessfulCount) * 100d /
                totalCount;
            return new InternetPathStatistics(
                ping,
                packetLoss);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (ProbeTargetState target in _targets)
            {
                target.ClearResults();
            }
        }

        foreach (ProbeTargetState target in _targets)
        {
            try
            {
                target.Dispose();
            }
            catch
            {
                // Continue releasing the remaining independent Ping handles.
            }
        }
    }

    private async Task ProbeAsync(
        bool selectTarget,
        int selectedTargetIndex)
    {
        if (!selectTarget)
        {
            TargetProbeResult result = await ProbeTargetAsync(
                selectedTargetIndex,
                _targets[selectedTargetIndex]).ConfigureAwait(false);
            lock (_sync)
            {
                _probeInProgress = false;
                if (!_disposed)
                {
                    _targets[result.TargetIndex].Add(result.Result);
                }
            }

            return;
        }

        Task<TargetProbeResult>[] probes = [.. _targets
            .Select((target, index) =>
                ProbeTargetAsync(index, target))];
        TargetProbeResult[] results =
            await Task.WhenAll(probes).ConfigureAwait(false);
        lock (_sync)
        {
            _probeInProgress = false;
            if (_disposed)
            {
                return;
            }

            foreach (TargetProbeResult result in results)
            {
                _targets[result.TargetIndex].Add(result.Result);
            }

            TargetProbeResult? best = null;
            foreach (TargetProbeResult result in results)
            {
                if (result.Result.IsSuccess &&
                    (best is null ||
                     result.Result.RoundtripMilliseconds <
                     best.Value.Result.RoundtripMilliseconds))
                {
                    best = result;
                }
            }

            if (best is not null)
            {
                int bestTargetIndex = best.Value.TargetIndex;
                if (_selectedTargetIndex >= 0 &&
                    _selectedTargetIndex != bestTargetIndex)
                {
                    _targets[bestTargetIndex].Reset(
                        best.Value.Result);
                }

                _selectedTargetIndex = bestTargetIndex;
            }
        }
    }

    private static async Task<TargetProbeResult> ProbeTargetAsync(
        int targetIndex,
        ProbeTargetState target)
    {
        try
        {
            PingReply reply = await target.Ping.SendPingAsync(
                target.Address,
                PingTimeoutMilliseconds).ConfigureAwait(false);
            return new TargetProbeResult(
                targetIndex,
                reply.Status == IPStatus.Success
                    ? new ProbeResult(true, reply.RoundtripTime)
                    : new ProbeResult(false, 0));
        }
        catch
        {
            return new TargetProbeResult(
                targetIndex,
                new ProbeResult(false, 0));
        }
    }

    private sealed class ProbeTargetState(
        IPAddress address,
        int maximumResults) : IDisposable
    {
        public IPAddress Address { get; } = address;

        public Ping Ping { get; } = new();

        public Queue<ProbeResult> Results { get; } = new Queue<ProbeResult>(maximumResults);

        public int ConsecutiveFailures { get; private set; }

        public int SuccessfulCount { get; private set; }

        public long RoundtripTotal { get; private set; }

        public void Add(ProbeResult result)
        {
            Results.Enqueue(result);
            if (result.IsSuccess)
            {
                SuccessfulCount++;
                RoundtripTotal += result.RoundtripMilliseconds;
            }

            while (Results.Count > maximumResults)
            {
                ProbeResult removed = Results.Dequeue();
                if (removed.IsSuccess)
                {
                    SuccessfulCount--;
                    RoundtripTotal -= removed.RoundtripMilliseconds;
                }
            }

            ConsecutiveFailures = result.IsSuccess
                ? 0
                : ConsecutiveFailures + 1;
        }

        public void Reset(ProbeResult result)
        {
            ClearResults();
            ConsecutiveFailures = 0;
            Add(result);
        }

        public void ClearResults()
        {
            Results.Clear();
            SuccessfulCount = 0;
            RoundtripTotal = 0;
        }

        public void Dispose() => Ping.Dispose();
    }

    private readonly record struct TargetProbeResult(
        int TargetIndex,
        ProbeResult Result);

    private readonly record struct ProbeResult(
        bool IsSuccess,
        long RoundtripMilliseconds);
}

/// <summary>
/// Rolling latency and packet-loss measurements to a public Internet endpoint.
/// </summary>
internal readonly record struct InternetPathStatistics(
    double? PingMilliseconds,
    double? PacketLossPercent);
