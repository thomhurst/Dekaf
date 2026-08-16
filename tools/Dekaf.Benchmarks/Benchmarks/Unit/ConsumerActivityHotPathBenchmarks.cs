using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Broker-free allocation gate for the sampled, no-producer-context consumer activity path.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ConsumerActivityHotPathBenchmarks
{
    private KafkaConsumer<string, string> _consumer = null!;
    private PendingFetchData _pending = null!;
    private ActivityListener _listener = null!;
    private StartConsumeActivity _startActivity = null!;

    [GlobalSetup]
    public void Setup()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name == Diagnostics.DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);

        _consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "consumer-activity-hot-path",
                GroupId = "consumer-activity-hot-path"
            },
            Serializers.String,
            Serializers.String);
        _pending = PendingFetchData.Create("orders", 0, Array.Empty<RecordBatch>());

        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "StartConsumeActivity",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        _startActivity = method.CreateDelegate<StartConsumeActivity>();

        var metadataManager = GetInstanceField<MetadataManager>(_consumer, "_metadataManager");
        typeof(MetadataManager)
            .GetMethod(
                "UpdateMetadataClusterId",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                [typeof(string)],
                modifiers: null)?
            .Invoke(metadataManager, ["consumer-activity-hot-path"]);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _pending.Dispose();
        _listener.Dispose();
        await _consumer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public void StartAndStop()
    {
        using var activity = _startActivity(
            _consumer,
            _pending,
            headers: null,
            offset: 42,
            isTombstone: false,
            isProcessSpan: false);
    }

    private static T GetInstanceField<T>(object target, string name)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return (T)target.GetType().GetField(name, Flags)!.GetValue(target)!;
    }

    private delegate Activity? StartConsumeActivity(
        KafkaConsumer<string, string> consumer,
        PendingFetchData pending,
        IReadOnlyList<Header>? headers,
        long offset,
        bool isTombstone,
        bool isProcessSpan);
}
