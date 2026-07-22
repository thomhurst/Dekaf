using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;

namespace Dekaf.Producer;

/// <summary>
/// Per-producer bridge from internal controller state (buffer memory, per-broker
/// delivery budgets, connection scaling, in-flight depth) to the "Dekaf" meter's
/// observable instruments. Registered by the <c>KafkaProducer</c> constructor and
/// unregistered on dispose via <see cref="DekafMetrics"/>.
/// </summary>
/// <remarks>
/// Observable instruments are pull-based: these methods run only when a metrics
/// listener polls (e.g. dotnet-counters at 1 Hz), never on the produce hot path.
/// All reads go through the volatile-published accessors — the raw send-loop-owned
/// estimator fields are single-writer and must not be read from here.
/// </remarks>
internal sealed class ProducerStateSource
{
    private readonly RecordAccumulator _accumulator;
    private readonly ConcurrentDictionary<int, BrokerSender> _brokerSenders;
    private readonly KeyValuePair<string, object?> _clientIdTag;

    internal ProducerStateSource(
        string? clientId,
        RecordAccumulator accumulator,
        ConcurrentDictionary<int, BrokerSender> brokerSenders)
    {
        _accumulator = accumulator;
        _brokerSenders = brokerSenders;
        _clientIdTag = new KeyValuePair<string, object?>(DekafDiagnostics.MessagingClientId, clientId);
    }

    internal Measurement<long> BufferUsedBytes()
        => new(_accumulator.BufferedBytes, _clientIdTag);

    internal Measurement<long> BufferLimitBytes()
        => new((long)_accumulator.MaxBufferMemory, _clientIdTag);

    internal Measurement<long> BufferPressureEvents()
        => new(_accumulator.BufferPressureEvents, _clientIdTag);

    internal IEnumerable<Measurement<T>> BudgetValues<T>(Func<BrokerUnackedByteBudget, T> selector)
        where T : struct
    {
        foreach (var (brokerId, budget) in _accumulator.BrokerUnackedBudgets)
        {
            yield return new Measurement<T>(selector(budget), _clientIdTag, BrokerTag(brokerId));
        }
    }

    internal IEnumerable<Measurement<long>> SenderValues(Func<BrokerSender, long> selector)
    {
        foreach (var (brokerId, sender) in _brokerSenders)
        {
            yield return new Measurement<long>(selector(sender), _clientIdTag, BrokerTag(brokerId));
        }
    }

    private static KeyValuePair<string, object?> BrokerTag(int brokerId)
        => new(DekafDiagnostics.DekafBrokerId, brokerId);
}
