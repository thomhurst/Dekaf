using Dekaf.Admin;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    /// <summary>
    /// Builds the explicit group-level configs to apply before joining, validating the options.
    /// Returns null when every setting is left to the broker.
    /// </summary>
    internal static IReadOnlyList<ConfigAlter>? CreateGroupConfigAlters(ShareConsumerOptions options)
    {
        var autoOffsetReset = ShareAutoOffsetResetStrategy.GetConfigValue(options);
        ShareDeadLetterQueueConfiguration.Validate(options);

        List<ConfigAlter>? alters = null;
        if (autoOffsetReset is not null)
            (alters ??= []).Add(ConfigAlter.Set("share.auto.offset.reset", autoOffsetReset));
        if (options.DeadLetterQueueTopic is { } deadLetterQueueTopic)
            (alters ??= []).Add(ConfigAlter.Set(ShareDeadLetterQueueConfiguration.TopicNameConfig, deadLetterQueueTopic));
        if (options.DeadLetterQueueCopyRecord is { } copyRecord)
            (alters ??= []).Add(ConfigAlter.Set(ShareDeadLetterQueueConfiguration.CopyRecordConfig, copyRecord ? "true" : "false"));
        return alters;
    }

    private async ValueTask ConfigureGroupAsync(CancellationToken cancellationToken)
    {
        // Borrow the consumer's authenticated connections and metadata. The admin client owns
        // only its own telemetry; disposing it must not retire the consumer's infrastructure.
        var admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = _options.BootstrapServers,
            RequestTimeoutMs = _options.RequestTimeoutMs,
            RetryBackoffMs = _options.RetryBackoffMs,
            RetryBackoffMaxMs = _options.RetryBackoffMaxMs
        }, _connectionPool, _metadataManager);
        await using var adminScope = admin.ConfigureAwait(false);
        // One request keeps the alterations atomic for the group resource.
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = _options.GroupId }] = _groupConfigAlters!
        }, new IncrementalAlterConfigsOptions { TimeoutMs = _options.RequestTimeoutMs }, cancellationToken).ConfigureAwait(false);
    }
}
