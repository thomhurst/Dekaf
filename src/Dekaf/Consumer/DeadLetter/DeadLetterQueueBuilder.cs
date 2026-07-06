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
    private readonly List<TimeSpan> _retryTopicDelays = [];
    private string _retryTopicSuffixFormat = "-retry-{0}";

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
        _bootstrapServers = string.Join(",", BootstrapServerList.FromCommaSeparated(servers));
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
    /// Enables tiered retry topics with the supplied retry delays.
    /// For example, delays of 5s and 30s produce topics such as <c>orders-retry-5s</c>
    /// and <c>orders-retry-30s</c> before routing to the DLQ.
    /// </summary>
    /// <param name="delays">Ordered retry delays.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithRetryTopics(params TimeSpan[] delays)
    {
        ArgumentNullException.ThrowIfNull(delays);
        if (delays.Length == 0)
            throw new ArgumentException("At least one retry topic delay is required.", nameof(delays));

        _retryTopicDelays.Clear();
        foreach (var delay in delays)
        {
            RetryTopicOptions.ValidateDelay(delay);
            _retryTopicDelays.Add(delay);
        }

        return this;
    }

    /// <summary>
    /// Sets the suffix format used for retry topic names. The <c>{0}</c> placeholder receives
    /// a compact delay label such as <c>5s</c>, <c>30s</c>, or <c>1m</c>.
    /// </summary>
    /// <param name="suffixFormat">Suffix format appended to the source topic.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeadLetterQueueBuilder WithRetryTopicSuffixFormat(string suffixFormat)
    {
        RetryTopicOptions.ValidateTopicSuffixFormat(suffixFormat);
        _retryTopicSuffixFormat = suffixFormat;
        return this;
    }

    /// <summary>
    /// Sets default bootstrap servers only if not already explicitly configured.
    /// Used by DI to inherit from the consumer's bootstrap servers.
    /// </summary>
    internal DeadLetterQueueBuilder WithDefaultBootstrapServers(string servers)
    {
        _bootstrapServers ??= string.Join(",", BootstrapServerList.FromCommaSeparated(servers));
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
        ConfigureProducer = _configureProducer,
        RetryTopics = _retryTopicDelays.Count == 0
            ? null
            : new RetryTopicOptions
            {
                Delays = _retryTopicDelays.ToArray(),
                TopicSuffixFormat = _retryTopicSuffixFormat
            }
    };
}
