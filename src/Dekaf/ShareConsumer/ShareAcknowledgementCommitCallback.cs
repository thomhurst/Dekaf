namespace Dekaf.ShareConsumer;

/// <summary>
/// Handles completed share-consumer acknowledgement results synchronously.
/// </summary>
/// <param name="results">
/// Ordered acknowledgement results. The span is valid only for the callback invocation;
/// copy individual result values when they must be retained.
/// </param>
public delegate void ShareAcknowledgementCommitCallback(
    ReadOnlySpan<ShareAcknowledgementCommitResult> results);
