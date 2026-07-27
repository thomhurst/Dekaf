using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures eager, already-deserialized, and partition-EOF <see cref="ConsumeResult{TKey,TValue}"/>
/// construction without network or broker noise.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ConstructionJobConfig))]
public class ConsumeResultConstructionBenchmarks
{
    private const int InvocationsPerIteration = 20_000_000;
    private static readonly byte[] Data = "benchmark-value"u8.ToArray();
    private PendingFetchData _headerOwner = null!;

    private sealed class ConstructionJobConfig : ManualConfig
    {
        public ConstructionJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(8)
                .WithIterationCount(15)
                .WithInvocationCount(InvocationsPerIteration)
                .WithUnrollFactor(1));
        }
    }

    [GlobalSetup]
    public void Setup() =>
        _headerOwner = PendingFetchData.Create(
            "benchmark-topic",
            partitionIndex: 0,
            Array.Empty<RecordBatch>());

    [GlobalCleanup]
    public void Cleanup() => _headerOwner.Dispose();

    [Benchmark(Baseline = true)]
    public ConsumeResult<string, string> SynchronousDeserializers() =>
        Create(Serializers.String, Serializers.String);

    [Benchmark]
    public ConsumeResult<string, string> NullDeserializers() =>
        Create(keyDeserializer: null, valueDeserializer: null);

    [Benchmark]
    public ConsumeResult<string, string> AlreadyDeserializedConstructor() =>
        new(
            topic: "benchmark-topic",
            partition: 0,
            offset: 1,
            key: "benchmark-key",
            value: "benchmark-value",
            pooledHeaders: null,
            pooledHeaderCount: 0,
            _headerOwner,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null);

    [Benchmark]
    public ConsumeResult<string, string> PartitionEof() =>
        ConsumeResult<string, string>.CreatePartitionEof("benchmark-topic", partition: 0, offset: 1);

    private static ConsumeResult<string, string> Create(
        IDeserializer<string>? keyDeserializer,
        IDeserializer<string>? valueDeserializer,
        bool isPartitionEof = false) =>
        new(
            topic: "benchmark-topic",
            partition: 0,
            offset: 1,
            keyData: Data,
            isKeyNull: false,
            valueData: Data,
            isValueNull: false,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer,
            valueDeserializer,
            isPartitionEof);
}
