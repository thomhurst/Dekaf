using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class InMemoryDeliveryCallbackBenchmarks
{
    private const int BatchSize = 1024;
    private readonly InMemoryKafkaCluster _cluster = new();
    private readonly ProducerMessage<string, string> _message = new()
    {
        Topic = "callbacks", Key = "key", Value = "value"
    };
    private InMemoryProducer<string, string> _producer = null!;
    private Action<RecordMetadata, Exception?> _handler = null!;
    private long _lastOffset;

    [GlobalSetup]
    public void Setup()
    {
        _cluster.CreateTopic(_message.Topic);
        _producer = new InMemoryProducer<string, string>(_cluster);
        _handler = ObserveDelivery;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long DeliverBatch()
    {
        for (var i = 0; i < BatchSize; i++)
            _producer.FireAsync(_message, _handler).GetAwaiter().GetResult();

        // Bound retained simulator records. Reset cost is amortized over the batch.
        _cluster.DeleteTopic(_message.Topic);
        _cluster.CreateTopic(_message.Topic);
        return _lastOffset;
    }

    private void ObserveDelivery(RecordMetadata metadata, Exception? error)
    {
        if (error is not null)
            throw error;
        _lastOffset = metadata.Offset;
    }

    [GlobalCleanup]
    public void Cleanup() => _producer.DisposeAsync().GetAwaiter().GetResult();
}
