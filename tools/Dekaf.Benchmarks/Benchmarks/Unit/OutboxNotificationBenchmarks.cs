using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// One committed transaction notification consumed by an idle relay. The buffered path
/// excludes actual database/Kafka work and asynchronous wait/timer costs.
/// </summary>
[MemoryDiagnoser]
public class OutboxNotificationBenchmarks
{
    private ServiceProvider _provider = null!;
    private IOutboxNotifier _notifier = null!;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDekafOutboxRelay();
        _provider = services.BuildServiceProvider();
        _notifier = _provider.GetRequiredService<IOutboxNotifier>();
        _notifier.NotifyCommitted();
        var ready = _notifier.WaitAsync(_timeout);
        if (!ready.IsCompletedSuccessfully)
            throw new InvalidOperationException("A notification before waiting must complete synchronously.");
        ready.GetAwaiter().GetResult();
    }

    [Benchmark]
    public ValueTask NotifyAndConsume()
    {
        _notifier.NotifyCommitted();
        return _notifier.WaitAsync(_timeout);
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
