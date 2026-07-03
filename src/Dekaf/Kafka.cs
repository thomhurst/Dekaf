namespace Dekaf;

using Admin;
using Consumer;
using Producer;
using ShareConsumer;

/// <summary>
/// Main entry point for creating Kafka clients.
/// </summary>
public static class Kafka
{
    /// <summary>
    /// Creates a root Kafka client builder.
    /// </summary>
    public static KafkaClientBuilder Connect()
    {
        return new KafkaClientBuilder();
    }

    /// <summary>
    /// Creates a root Kafka client for the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    public static KafkaClient Connect(string bootstrapServers)
    {
        return new KafkaClientBuilder()
            .WithBootstrapServers(bootstrapServers)
            .Build();
    }

    /// <summary>
    /// Creates a root Kafka client for the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="configure">Optional root-client configuration.</param>
    public static KafkaClient Connect(string bootstrapServers, Action<KafkaClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new KafkaClientBuilder()
            .WithBootstrapServers(bootstrapServers);
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a producer builder.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> CreateProducer<TKey, TValue>()
    {
        return new ProducerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a consumer builder.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        return new ConsumerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a share consumer builder.
    /// </summary>
    public static ShareConsumerBuilder<TKey, TValue> CreateShareConsumer<TKey, TValue>()
    {
        return new ShareConsumerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates an admin client builder.
    /// </summary>
    public static AdminClientBuilder CreateAdminClient()
    {
        return new AdminClientBuilder();
    }
}
