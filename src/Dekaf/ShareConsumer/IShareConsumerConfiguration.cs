namespace Dekaf.ShareConsumer;

/// <summary>Exposes acknowledgement configuration to hosted services and consumer wrappers.</summary>
public interface IShareConsumerConfiguration
{
    /// <summary>Gets the configured acknowledgement mode.</summary>
    ShareAcknowledgementMode AcknowledgementMode { get; }
}

// Raw copies are opt-in and scoped to the current poll round. The hosted service never
// advances the poll while a record is being processed or routed.
internal interface IRawShareRecordAccessor
{
    void EnableRawRecordTracking();
    bool TryGetRawRecord(TopicPartitionOffset record, out byte[]? key, out byte[]? value);
}

internal interface IHostedShareConsumer
{
    long AcquisitionStartedTimestamp { get; }
    // Hosted processing completes renewed work in place: terminal dispositions stop local
    // renewal replay while acknowledgement submission remains tracked independently.
    void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer);
    // Poll cancellation stops pre-write work. Started requests remain observed within the
    // host's shutdown budget, after which acknowledgement outcomes remain unconfirmed.
    void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer,
        CancellationToken requestCancellationToken);
}
