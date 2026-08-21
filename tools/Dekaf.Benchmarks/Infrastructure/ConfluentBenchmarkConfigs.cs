namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Shared Confluent.Kafka configs for the comparison benchmarks, kept in one place so
/// the fairness-critical settings cannot drift between benchmark classes.
/// </summary>
public static class ConfluentBenchmarkConfigs
{
    /// <summary>
    /// Producer config shared by comparison benchmarks so acknowledgement, batching,
    /// linger, and delivery-report settings stay explicit and consistent.
    /// </summary>
    public static Confluent.Kafka.ProducerConfig CreateProducerConfig(
        string bootstrapServers,
        string clientId,
        double lingerMs,
        bool enableDeliveryReports,
        int? queueBufferingMaxMessages = null)
    {
        var config = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = clientId,
            Acks = Confluent.Kafka.Acks.All,
            EnableIdempotence = true,
            LingerMs = lingerMs,
            BatchSize = 16384,
            EnableDeliveryReports = enableDeliveryReports
        };

        if (queueBufferingMaxMessages is { } maxMessages)
            config.QueueBufferingMaxMessages = maxMessages;

        return config;
    }

    /// <summary>
    /// Consumer config doing the same work as Dekaf's defaults. <c>CheckCrcs</c> is on
    /// because Dekaf validates batch CRCs by default (Java-client parity) while
    /// librdkafka defaults <c>check.crcs</c> off — without it the two clients are not
    /// doing the same work and the comparison is invalid.
    /// </summary>
    public static Confluent.Kafka.ConsumerConfig CreateConsumerConfig(
        string bootstrapServers,
        string clientId,
        string groupId,
        bool enableAutoCommit = true)
        => new()
        {
            BootstrapServers = bootstrapServers,
            ClientId = clientId,
            GroupId = groupId,
            AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = enableAutoCommit,
            CheckCrcs = true
        };
}
