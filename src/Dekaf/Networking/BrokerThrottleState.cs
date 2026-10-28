using System.Runtime.CompilerServices;
using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Networking;

/// <summary>
/// Shared KIP-219 throttle deadline for every physical connection to one broker.
/// </summary>
internal sealed class BrokerThrottleState(ClientTelemetryMetricCollector? telemetryMetricCollector = null)
{
    private long _throttleUntilMs;
    private int _maxObservedThrottleTimeMs;

    public int MaxObservedThrottleTimeMs => Volatile.Read(ref _maxObservedThrottleTimeMs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRemainingMilliseconds()
    {
        var throttleUntilMs = Volatile.Read(ref _throttleUntilMs);
        if (throttleUntilMs == 0)
            return 0;

        var remainingMs = throttleUntilMs - MonotonicClock.GetMilliseconds();
        if (remainingMs <= 0)
        {
            Interlocked.CompareExchange(ref _throttleUntilMs, 0, throttleUntilMs);
            return 0;
        }

        return (int)Math.Min(remainingMs, int.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WaitAsync(
        CancellationToken cancellationToken,
        CancellationToken connectionShutdownToken)
    {
        var remainingMs = GetRemainingMilliseconds();
        return remainingMs == 0
            ? ValueTask.CompletedTask
            : WaitSlowAsync(remainingMs, cancellationToken, connectionShutdownToken);
    }

    public void Observe(int throttleTimeMs)
    {
        telemetryMetricCollector?.RecordBrokerThrottle(throttleTimeMs);

        if (throttleTimeMs <= 0)
            return;

        var observedMaximum = Volatile.Read(ref _maxObservedThrottleTimeMs);
        while (throttleTimeMs > observedMaximum)
        {
            var previous = Interlocked.CompareExchange(
                ref _maxObservedThrottleTimeMs,
                throttleTimeMs,
                observedMaximum);
            if (previous == observedMaximum)
                break;

            observedMaximum = previous;
        }

        var nowMs = MonotonicClock.GetMilliseconds();
        var throttleUntilMs = throttleTimeMs >= long.MaxValue - nowMs
            ? long.MaxValue
            : nowMs + throttleTimeMs;
        var current = Volatile.Read(ref _throttleUntilMs);

        while (throttleUntilMs > current)
        {
            var observed = Interlocked.CompareExchange(
                ref _throttleUntilMs,
                throttleUntilMs,
                current);
            if (observed == current)
                return;

            current = observed;
        }
    }

    public void MergeFrom(BrokerThrottleState source)
    {
        if (ReferenceEquals(this, source))
            return;

        var sourceMaximum = Volatile.Read(ref source._maxObservedThrottleTimeMs);
        var observedMaximum = Volatile.Read(ref _maxObservedThrottleTimeMs);
        while (sourceMaximum > observedMaximum)
        {
            var previous = Interlocked.CompareExchange(
                ref _maxObservedThrottleTimeMs,
                sourceMaximum,
                observedMaximum);
            if (previous == observedMaximum)
                break;

            observedMaximum = previous;
        }

        var sourceDeadline = Volatile.Read(ref source._throttleUntilMs);
        var currentDeadline = Volatile.Read(ref _throttleUntilMs);
        while (sourceDeadline > currentDeadline)
        {
            var previous = Interlocked.CompareExchange(
                ref _throttleUntilMs,
                sourceDeadline,
                currentDeadline);
            if (previous == currentDeadline)
                break;

            currentDeadline = previous;
        }
    }

    private async ValueTask WaitSlowAsync(
        int initialDelayMs,
        CancellationToken cancellationToken,
        CancellationToken connectionShutdownToken)
    {
        CancellationTokenSource? linkedCts = null;
        var effectiveToken = cancellationToken;
        if (connectionShutdownToken.CanBeCanceled && connectionShutdownToken != cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    connectionShutdownToken);
                effectiveToken = linkedCts.Token;
            }
            else
            {
                effectiveToken = connectionShutdownToken;
            }
        }

        try
        {
            var delayMs = initialDelayMs;
            while (delayMs > 0)
            {
                await Task.Delay(delayMs, effectiveToken).ConfigureAwait(false);
                delayMs = GetRemainingMilliseconds();
            }
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }
}

/// <summary>
/// Mirrors Apache Kafka 4.4's <c>AbstractResponse.shouldClientThrottle(version)</c>.
/// Responses that predate KIP-219 keep their per-API version gates because the
/// broker already delayed them. Every other response version is client-throttled
/// when its schema carries <c>ThrottleTimeMs</c>.
/// </summary>
internal static class BrokerThrottlePolicy
{
    // Never is above every response version, so those APIs are never client-throttled.
    private const byte Never = byte.MaxValue;

    /// <summary>
    /// The first client-throttled response version, indexed by <see cref="ApiKey"/>.
    /// Keys past the end (FetchSnapshot onwards) carry <c>ThrottleTimeMs</c> from v0.
    /// A table keeps the per-response check to one bounds check and one compare.
    /// </summary>
    private static ReadOnlySpan<byte> FirstThrottledVersion =>
    [
        6,     // 0 Produce
        8,     // 1 Fetch
        3,     // 2 ListOffsets
        6,     // 3 Metadata
        Never, // 4 LeaderAndIsr
        Never, // 5 StopReplica
        Never, // 6 UpdateMetadata
        Never, // 7 ControlledShutdown
        4,     // 8 OffsetCommit
        4,     // 9 OffsetFetch
        2,     // 10 FindCoordinator
        3,     // 11 JoinGroup
        2,     // 12 Heartbeat
        2,     // 13 LeaveGroup
        2,     // 14 SyncGroup
        2,     // 15 DescribeGroups
        2,     // 16 ListGroups
        Never, // 17 SaslHandshake
        2,     // 18 ApiVersions
        3,     // 19 CreateTopics
        2,     // 20 DeleteTopics
        1,     // 21 DeleteRecords
        1,     // 22 InitProducerId
        2,     // 23 OffsetForLeaderEpoch (schema, no upstream override)
        1,     // 24 AddPartitionsToTxn
        1,     // 25 AddOffsetsToTxn
        1,     // 26 EndTxn
        Never, // 27 WriteTxnMarkers
        1,     // 28 TxnOffsetCommit
        1,     // 29 DescribeAcls
        1,     // 30 CreateAcls
        1,     // 31 DeleteAcls
        2,     // 32 DescribeConfigs
        1,     // 33 AlterConfigs
        1,     // 34 AlterReplicaLogDirs
        1,     // 35 DescribeLogDirs
        Never, // 36 SaslAuthenticate
        1,     // 37 CreatePartitions
        1,     // 38 CreateDelegationToken
        1,     // 39 RenewDelegationToken
        1,     // 40 ExpireDelegationToken
        1,     // 41 DescribeDelegationToken
        1,     // 42 DeleteGroups
        0,     // 43 ElectLeaders
        0,     // 44 IncrementalAlterConfigs
        0,     // 45 AlterPartitionReassignments
        0,     // 46 ListPartitionReassignments
        0,     // 47 OffsetDelete
        0,     // 48 DescribeClientQuotas
        0,     // 49 AlterClientQuotas
        0,     // 50 DescribeUserScramCredentials
        0,     // 51 AlterUserScramCredentials
        Never, // 52 Vote
        Never, // 53 BeginQuorumEpoch
        Never, // 54 EndQuorumEpoch
        Never, // 55 DescribeQuorum
        0,     // 56 AlterPartition
        0,     // 57 UpdateFeatures
        Never, // 58 Envelope
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldClientThrottle(ApiKey apiKey, short version)
    {
        var table = FirstThrottledVersion;
        var index = (uint)(short)apiKey;
        return index >= (uint)table.Length || version >= table[(int)index];
    }
}

internal interface IBrokerThrottleProvider
{
    int GetRemainingBrokerThrottleMilliseconds(int brokerId);
}
