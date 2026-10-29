using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures producer sampling with and without an outbox fallback. Disabled tracing must
/// remain allocation-free; sampled outbox sends replace their stored propagation headers.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProducerActivityDisabledBenchmarks
{
    private KafkaProducer<string, string> _producer = null!;
    private ProducerMessage<string, string> _message = null!;
    private StartPublishActivityDelegate _startPublishActivity = null!;
    private ActivityListener? _listener;
    private readonly Header _storedParent = new("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"u8.ToArray());
    private readonly Header _storedState = new("tracestate", "vendor=value"u8.ToArray());

    // -1: no listener, 0: declines activities, 3: samples activities.
    [Params(-1, 0, 3)]
    public int Sampling { get; set; }

    [Params(false, true)]
    public bool StoredContext { get; set; }

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
            Value = "value",
            Headers = new Headers(2)
        };
        _startPublishActivity = typeof(KafkaProducer<string, string>)
            .GetMethod("StartPublishActivity", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<StartPublishActivityDelegate>(_producer);
        if (Sampling >= 0)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => (ActivitySamplingResult)Sampling
            };
            ActivitySource.AddActivityListener(_listener);
        }
        if (StoredContext)
            DekafDiagnostics.SendParentContext.Value = ActivityContext.Parse(_storedParent.GetValueAsString()!, "vendor=value");
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        DekafDiagnostics.SendParentContext.Value = default;
        _listener?.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public Activity? StartPublishActivity()
    {
        var headers = _message.Headers!;
        headers.Clear();
        if (StoredContext)
        {
            headers.Add(_storedParent);
            headers.Add(_storedState);
        }
        var activity = _startPublishActivity(_message, out _);
        activity?.Dispose();
        return activity;
    }

    private delegate Activity? StartPublishActivityDelegate(ProducerMessage<string, string> message, out Headers? headers);
}
