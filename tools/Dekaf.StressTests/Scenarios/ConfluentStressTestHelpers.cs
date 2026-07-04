using System.Diagnostics;
using Dekaf.StressTests.Metrics;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.StressTests.Scenarios;

internal static class ConfluentStressTestHelpers
{
    /// <summary>
    /// librdkafka local queue byte bound, derived from Dekaf's
    /// <see cref="StressTestHelpers.ProducerBufferMemoryBytes"/> so both clients absorb
    /// the same backlog before backpressure kicks in.
    /// </summary>
    internal const int QueueBufferingMaxKbytes = (int)(StressTestHelpers.ProducerBufferMemoryBytes / 1024);

    /// <summary>
    /// librdkafka's maximum, so the byte bound above is always the binding limit —
    /// matching Dekaf's bytes-only BufferMemory regardless of --message-size (the
    /// default 100k count cap would bind first for messages under ~10 KB).
    /// </summary>
    internal const int QueueBufferingMaxMessages = 10_000_000;

    // --- Consumer fetch sizing -------------------------------------------------------------
    // Mirrors Dekaf's ConsumerBuilder.ForHighThroughput() preset (src/Dekaf/Builders.cs) so the
    // Confluent consumer stress test fetches with the same bounds instead of librdkafka's much
    // smaller defaults (max.partition.fetch.bytes 1 MB, fetch.max.bytes 50 MB, fetch.wait.max.ms
    // 500 ms, queued.max.messages.kbytes 64 MB). Without this the "consumer" scenario measures a
    // configuration gap rather than the client — the producer scenarios already match buffers
    // this way via QueueBufferingMaxKbytes. Keep these in sync with ForHighThroughput() by hand:
    // the preset's fields are private, so there is no compile-time link (same constraint as
    // QueueBufferingMaxMessages above). librdkafka has no adaptive-fetch-sizing or explicit
    // max-poll-records equivalent, so these pin the preset's base fetch sizes; the consume loop
    // is single-message (consumer.Consume) either way.

    /// <summary>Dekaf <c>_fetchMinBytes</c> — fetch.min.bytes.</summary>
    internal const int FetchMinBytes = 1024;

    /// <summary>Dekaf <c>_fetchMaxWaitMs</c> — fetch.wait.max.ms.</summary>
    internal const int FetchMaxWaitMs = 200;

    /// <summary>Dekaf <c>_maxPartitionFetchBytes</c> (preset base, pre-adaptive) — max.partition.fetch.bytes.</summary>
    internal const int MaxPartitionFetchBytes = 4 * 1024 * 1024;

    /// <summary>Dekaf <c>_fetchMaxBytes</c> (preset base, pre-adaptive) — fetch.max.bytes.</summary>
    internal const int FetchMaxBytes = 100 * 1024 * 1024;

    /// <summary>
    /// librdkafka's local fetch queue is its prefetch pipeline (analogous to Dekaf's
    /// <c>_prefetchPipelineDepth = 5</c>). Size it well above the 64 MB default so prefetch, not
    /// the client's decode loop, is never the bottleneck. Range is 1..2097151 KB (≈2 GB).
    /// </summary>
    internal const int QueuedMaxMessagesKbytes = 1024 * 1024;

    /// <summary>Companion to <see cref="QueuedMaxMessagesKbytes"/> — raise the message-count prefetch cap above the 100k default.</summary>
    internal const int QueuedMinMessages = 1_000_000;

    /// <summary>
    /// Queries the high watermark of each partition, mirroring
    /// <see cref="StressTestHelpers.QueryEndOffsetsAsync"/> for Confluent scenarios
    /// (which stay Dekaf-free end to end).
    /// </summary>
    internal static long[] QueryEndOffsets<TKey, TValue>(
        ConfluentKafka.IConsumer<TKey, TValue> consumer,
        string topic,
        int partitionCount,
        TimeSpan timeout)
    {
        var endOffsets = new long[partitionCount];
        for (var p = 0; p < partitionCount; p++)
        {
            var watermarks = consumer.QueryWatermarkOffsets(new ConfluentKafka.TopicPartition(topic, p), timeout);
            endOffsets[p] = watermarks.High.Value;
        }
        return endOffsets;
    }

    /// <summary>
    /// Sums the high watermarks across all partitions of <paramref name="topic"/> via a
    /// short-lived Confluent consumer. See
    /// <see cref="StressTestHelpers.QueryTotalEndOffsetAsync"/> for why producer
    /// scenarios snapshot this before and after the run.
    /// </summary>
    internal static long? QueryTotalEndOffset(string bootstrapServers, string topic, int partitionCount)
    {
        try
        {
            var config = new ConfluentKafka.ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                ClientId = "stress-watermark-query",
                GroupId = $"stress-watermark-{Guid.NewGuid():N}"
            };

            using var consumer = new ConfluentKafka.ConsumerBuilder<ConfluentKafka.Ignore, ConfluentKafka.Ignore>(config).Build();
            return QueryEndOffsets(consumer, topic, partitionCount, TimeSpan.FromSeconds(10)).Sum();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: end offset query failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Produces one message, blocking briefly and retrying while librdkafka's local
    /// queue is full. This mirrors Dekaf's BufferMemory backpressure: without it,
    /// queue-full messages are silently dropped and counted as errors, which makes
    /// throughput numbers incomparable (drops vs. delivered goodput).
    /// </summary>
    internal static void ProduceWithBackpressure(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        ConfluentKafka.Message<string, string> message,
        Action<ConfluentKafka.DeliveryReport<string, string>>? deliveryHandler,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                producer.Produce(topic, message, deliveryHandler);
                return;
            }
            catch (ConfluentKafka.ProduceException<string, string> ex)
                when (ex.Error.Code == ConfluentKafka.ErrorCode.Local_QueueFull)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The background poll thread drains the queue; a short sleep is the
                // idiomatic librdkafka backpressure wait.
                Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// Produces one message and records the full delivery round-trip into
    /// <paramref name="latency"/> via the delivery report callback, without
    /// stalling the produce loop.
    /// </summary>
    internal static void SampleDeliveryLatency(
        ConfluentKafka.IProducer<string, string> producer,
        string topic,
        ConfluentKafka.Message<string, string> message,
        LatencyTracker latency,
        ThroughputTracker throughput,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        ProduceWithBackpressure(producer, topic, message, report =>
        {
            if (report.Error.IsError)
            {
                throughput.RecordError();
            }
            else
            {
                latency.RecordTicks(Stopwatch.GetTimestamp() - start);
            }
        }, cancellationToken);
    }
}
