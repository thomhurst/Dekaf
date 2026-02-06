namespace Dekaf;

using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

/// <summary>
/// Main entry point for creating Kafka clients.
/// </summary>
public static class Kafka
{
    /// <summary>
    /// Creates a producer builder.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> CreateProducer<TKey, TValue>()
    {
        return new ProducerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a producer with the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    public static IKafkaProducer<TKey, TValue> CreateProducer<TKey, TValue>(string bootstrapServers)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .Build();
    }

    /// <summary>
    /// Creates a topic-specific producer with the specified bootstrap servers and topic.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <returns>A producer bound to the specified topic.</returns>
    public static ITopicProducer<TKey, TValue> CreateTopicProducer<TKey, TValue>(string bootstrapServers, string topic)
    {
        return new ProducerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .BuildForTopic(topic);
    }

    /// <summary>
    /// Creates a consumer builder.
    /// </summary>
    public static ConsumerBuilder<TKey, TValue> CreateConsumer<TKey, TValue>()
    {
        return new ConsumerBuilder<TKey, TValue>();
    }

    /// <summary>
    /// Creates a consumer with the specified bootstrap servers and group ID.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="groupId">The consumer group ID.</param>
    public static IKafkaConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string bootstrapServers, string groupId)
    {
        return new ConsumerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(groupId)
            .Build();
    }

    /// <summary>
    /// Creates a consumer with the specified bootstrap servers, group ID, and topic subscriptions.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    /// <param name="groupId">The consumer group ID.</param>
    /// <param name="topics">The topics to subscribe to.</param>
    public static IKafkaConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string bootstrapServers, string groupId, params string[] topics)
    {
        return new ConsumerBuilder<TKey, TValue>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(groupId)
            .SubscribeTo(topics)
            .Build();
    }

    /// <summary>
    /// Creates an admin client builder.
    /// </summary>
    public static AdminClientBuilder CreateAdminClient()
    {
        return new AdminClientBuilder();
    }

    /// <summary>
    /// Creates an admin client with the specified bootstrap servers.
    /// </summary>
    /// <param name="bootstrapServers">Comma-separated list of bootstrap servers.</param>
    public static IAdminClient CreateAdminClient(string bootstrapServers)
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(bootstrapServers)
            .Build();
    }
}
