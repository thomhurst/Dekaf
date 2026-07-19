using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards the steady-state topic identity check that runs once per broker fetch cycle.
/// The metadata snapshot is unchanged during measurement, so the benchmark covers the
/// normal O(1) path rather than the rare delete/recreate recovery path.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class TopicIdentityCheckBenchmarks
{
    private static readonly Func<CancellationToken, string?, Guid, ValueTask> s_control =
        static (_, _, _) => ValueTask.CompletedTask;

    private KafkaConsumer<string, string> _consumer = null!;
    private Func<CancellationToken, string?, Guid, ValueTask> _identityCheck = null!;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual
            },
            Serializers.String,
            Serializers.String);

        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "HandleTopicIdentityChangesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Topic identity check method was not found.");
        _identityCheck = method.CreateDelegate<Func<CancellationToken, string?, Guid, ValueTask>>(_consumer);

        _identityCheck(CancellationToken.None, null, Guid.Empty).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask DelegateControl() =>
        s_control(CancellationToken.None, null, Guid.Empty);

    [Benchmark]
    public ValueTask UnchangedMetadataSnapshot() =>
        _identityCheck(CancellationToken.None, null, Guid.Empty);

    [GlobalCleanup]
    public void Cleanup() =>
        _consumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
