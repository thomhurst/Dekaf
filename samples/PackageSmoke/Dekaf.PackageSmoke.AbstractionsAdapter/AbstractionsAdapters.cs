using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.PackageSmoke.AbstractionsAdapter;

public sealed class AdapterStringSerializer : ISerializer<string>
{
    public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.CopyTo(destination.GetSpan(bytes.Length));
        destination.Advance(bytes.Length);
    }
}

public sealed class NoopProducerAdapter : IKafkaProducer<string, string>
{
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<string, string> message,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        string? key,
        string value,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask FireAsync(ProducerMessage<string, string> message) => throw new NotSupportedException();

    public ValueTask FireAsync(string topic, string? key, string value) => throw new NotSupportedException();

    public ValueTask FireAsync(
        ProducerMessage<string, string> message,
        Action<RecordMetadata, Exception?> deliveryHandler) => throw new NotSupportedException();

    public Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<ProducerMessage<string, string>> messages,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<(string? Key, string Value)> messages,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) => default;

    public ValueTask PurgeAsync(
        PurgeOptions options,
        CancellationToken cancellationToken = default) => default;

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
    }

    public void UnregisterMetricFromSubscription(string name)
    {
    }

    public ITransaction<string, string> BeginTransaction() => throw new NotSupportedException();

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask InitTransactionsAsync(
        bool keepPreparedTransaction,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask CompletePreparedTransactionAsync(
        PreparedTransactionState preparedState,
        bool committed,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ITopicProducer<string, string> ForTopic(string topic) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => default;
}
