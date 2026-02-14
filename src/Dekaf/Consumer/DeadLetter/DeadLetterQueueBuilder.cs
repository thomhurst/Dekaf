namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Fluent builder for configuring dead letter queue options.
/// </summary>
public sealed class DeadLetterQueueBuilder
{
    private string _topicSuffix = ".DLQ";
    private int _maxFailures = 1;
    private bool _includeExceptionInHeaders = true;
    private bool _awaitDelivery;
    private string? _bootstrapServers;
    private Action<ProducerBuilder<byte[]?, byte[]?>>? _configureProducer;

    /// <summary>
    /// Sets the topic suffix for DLQ topic names. Default: ".DLQ".
    /// </summary>
    /// <param name="suffix">The suffix to append to the source topic name.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithTopicSuffix(string suffix)
    {
        _topicSuffix = suffix;
        return this;
    }

    /// <summary>
    /// Sets the number of failures before a message is routed to the DLQ. Default: 1.
    /// </summary>
    /// <param name="maxFailures">The maximum number of processing failures before dead-lettering.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithMaxFailures(int maxFailures)
    {
        _maxFailures = maxFailures;
        return this;
    }

    /// <summary>
    /// Excludes exception details from DLQ message headers.
    /// By default, exception message and type are included.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder ExcludeExceptionFromHeaders()
    {
        _includeExceptionInHeaders = false;
        return this;
    }

    /// <summary>
    /// Awaits DLQ produce acknowledgment instead of fire-and-forget.
    /// By default, DLQ writes are fire-and-forget for minimal consumer impact.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder AwaitDelivery()
    {
        _awaitDelivery = true;
        return this;
    }

    /// <summary>
    /// Sets the bootstrap servers for the DLQ producer.
    /// If not set, uses the consumer's bootstrap servers.
    /// </summary>
    /// <param name="servers">The comma-separated list of bootstrap servers.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithBootstrapServers(string servers)
    {
        _bootstrapServers = servers;
        return this;
    }

    /// <summary>
    /// Configures the internal DLQ producer with custom settings.
    /// </summary>
    /// <param name="configure">An action to configure the DLQ producer builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithProducerConfig(Action<ProducerBuilder<byte[]?, byte[]?>> configure)
    {
        _configureProducer = configure;
        return this;
    }

    /// <summary>
    /// Builds the dead letter queue options.
    /// </summary>
    /// <returns>The configured <see cref="DeadLetterOptions"/> instance.</returns>
    internal DeadLetterOptions Build() => new()
    {
        TopicSuffix = _topicSuffix,
        MaxFailures = _maxFailures,
        IncludeExceptionInHeaders = _includeExceptionInHeaders,
        AwaitDelivery = _awaitDelivery,
        BootstrapServers = _bootstrapServers,
        ConfigureProducer = _configureProducer
    };
}
