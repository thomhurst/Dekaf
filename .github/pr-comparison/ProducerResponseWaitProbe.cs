using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

// Task-scoped allocation probe. Only the response-wait method is invoked on this
// fixture: it reads the explicitly installed signal and checks the supplied token.
// No sender loop, connection, accumulator, or uninitialized dependency is used.
[MemoryDiagnoser]
public class ProducerResponseWaitProbe
{
    private readonly AsyncAutoResetSignal _signal = new();
    private Func<int, CancellationToken, ValueTask<bool>> _wait = null!;

    [GlobalSetup]
    public void Setup()
    {
        var sender = (BrokerSender)RuntimeHelpers.GetUninitializedObject(typeof(BrokerSender));
        typeof(BrokerSender).GetField("_anyResponseCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(sender, _signal);
        _wait = typeof(BrokerSender).GetMethod("WaitForAnyResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<int, CancellationToken, ValueTask<bool>>>(sender);
    }

    [Benchmark(Baseline = true)]
    public ValueTask PreviousWrapper()
    {
        var wait = PreviousWait(Timeout.Infinite, CancellationToken.None);
        _signal.Signal();
        return wait;
    }

    [Benchmark]
    public ValueTask<bool> ProductionDirectWait()
    {
        var wait = _wait(Timeout.Infinite, CancellationToken.None);
        _signal.Signal();
        return wait;
    }

    private async ValueTask PreviousWait(int timeoutMs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _signal.WaitAsync(timeoutMs).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _signal.Dispose();
}
