using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Assignment checks reached by buffered consumption, including asynchronous deserializers.</summary>
[MemoryDiagnoser]
public class StandardConsumerTelemetryBenchmarks
{
    private static readonly string[] MetricPrefixes = ["org.apache.kafka.consumer."];
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;

    [Params(false, true)]
    public bool Subscribed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _consumer = new(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, "telemetry", 0);
        if (Subscribed)
        {
            var collector = BufferedConsumerHarness.GetPrivateField(_consumer, "_telemetryMetricCollector")!;
            // Enable the new subscription on the candidate; the baseline has no standard recorder.
            collector.GetType().GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(collector, [MetricPrefixes]);
        }
    }

    [Benchmark]
    public ValueTask BufferedAssignment() => _consumer.EnsureAssignmentForPollAsync(default);

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
