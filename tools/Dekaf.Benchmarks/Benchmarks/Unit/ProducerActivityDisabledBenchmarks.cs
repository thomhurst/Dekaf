using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Isolates the tracing-disabled producer activity branch. Cluster identity is read only
/// after an activity has been created, so this path must remain allocation-free.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProducerActivityDisabledBenchmarks
{
    private KafkaProducer<string, string> _producer = null!;
    private ProducerMessage<string, string> _message = null!;
    private StartPublishActivityDelegate _startPublishActivity = null!;

    [GlobalSetup]
    public void Setup()
    {
        _producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "activity-disabled-benchmark"
            },
            Serializers.String,
            Serializers.String);
        _message = new ProducerMessage<string, string>
        {
            Topic = "benchmark-topic",
            Key = "key",
            Value = "value"
        };
        _startPublishActivity = typeof(KafkaProducer<string, string>)
            .GetMethod("StartPublishActivity", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<StartPublishActivityDelegate>(_producer);
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _producer.DisposeAsync().ConfigureAwait(false);

    [Benchmark]
    public Activity? StartPublishActivityWithoutListeners() => _startPublishActivity(ref _message);

    private delegate Activity? StartPublishActivityDelegate(ref ProducerMessage<string, string> message);
}
