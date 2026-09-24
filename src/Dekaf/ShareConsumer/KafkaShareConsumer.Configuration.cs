using Dekaf.Admin;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private async ValueTask ConfigureAutoOffsetResetAsync(CancellationToken cancellationToken)
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
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = _options.GroupId }] =
                [ConfigAlter.Set("share.auto.offset.reset", _autoOffsetResetConfig!)]
        }, new IncrementalAlterConfigsOptions { TimeoutMs = _options.RequestTimeoutMs }, cancellationToken).ConfigureAwait(false);
    }
}
