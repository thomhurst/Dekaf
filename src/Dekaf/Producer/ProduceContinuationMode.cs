namespace Dekaf.Producer;

/// <summary>
/// How a produce operation's pooled per-message completion runs its continuation when the
/// broker ack completes it.
/// </summary>
internal enum ProduceContinuationMode : byte
{
    /// <summary>
    /// Continuations hop to the thread pool. Default for all public produce paths so user
    /// code never runs on the broker sender's ack thread.
    /// </summary>
    Async = 0,

    /// <summary>
    /// Run the continuation inline on the ack thread, but only while it is the caller's own
    /// bounded harvest attached directly to the completion (<c>ProduceAllAsync</c>; see
    /// <see cref="ProduceAllCompletion"/> remarks). Instrumented or retry awaits
    /// (<c>AwaitWithMetrics</c>, <c>AwaitWithActivity</c>, <c>ProduceAsyncWithRetry</c>)
    /// interpose their own state machine, so the producer upgrades this mode to
    /// <see cref="Async"/> whenever such a wrapper is used.
    /// </summary>
    InlineWhenDirect = 1,

    /// <summary>
    /// Always run continuations inline on the ack thread, including through instrumented
    /// awaits — the documented <c>InlineTransactionCompletions</c> opt-in contract.
    /// </summary>
    Inline = 2,
}
