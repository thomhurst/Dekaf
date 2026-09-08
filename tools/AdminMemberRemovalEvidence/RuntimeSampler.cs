using System.Diagnostics;

namespace Dekaf.Benchmarks;

/// <summary>One-second runtime activity throughout BDN setup, workload and cleanup.</summary>
public sealed class RuntimeSampler : IAsyncDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly List<Probe.Snapshot> _rows = new(240);
    private readonly Timer _timer;
    private readonly object _gate = new();
    public IReadOnlyList<Probe.Snapshot> Rows => _rows;
    public long StartedTimestamp => _started;

    public RuntimeSampler() => _timer = new Timer(static state => ((RuntimeSampler)state!).Sample(), this, 0, 1000);

    private void Sample()
    {
        lock (_gate) _rows.Add(Probe.TakeSnapshot(_process, _started, 0));
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync();
        Sample();
        _process.Dispose();
    }
}
