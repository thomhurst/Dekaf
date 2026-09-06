using BenchmarkDotNet.Attributes;
using Dekaf.Retry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class ExponentialBackoffRetryPolicyBenchmarks
{
    private readonly Exception _failure = new InvalidOperationException();
    private ExponentialBackoffRetryPolicy _policy = null!;

    [Params(2, 6, 65)]
    public int Attempt { get; set; }

    [Params(false, true)]
    public bool Jitter { get; set; }

    [GlobalSetup]
    public void Setup() => _policy = Create(Jitter);

    [Benchmark]
    public TimeSpan? NextDelay() => _policy.GetNextDelay(Attempt, _failure);

    private static ExponentialBackoffRetryPolicy Create(bool jitter) => new()
    {
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30),
        MaxAttempts = int.MaxValue,
        Jitter = jitter
    };
}
