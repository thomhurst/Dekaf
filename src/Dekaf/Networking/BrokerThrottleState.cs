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
/// Mirrors Apache Kafka's <c>AbstractResponse.shouldClientThrottle(version)</c>
/// gates. Older response versions were already delayed by the broker.
/// </summary>
internal static class BrokerThrottlePolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldClientThrottle(ApiKey apiKey, short version) => apiKey switch
    {
        ApiKey.Produce => version >= 6,
        ApiKey.Fetch => version >= 8,
        ApiKey.ListOffsets => version >= 3,
        ApiKey.Metadata => version >= 6,
        ApiKey.OffsetCommit => version >= 4,
        ApiKey.OffsetFetch => version >= 4,
        ApiKey.FindCoordinator => version >= 2,
        ApiKey.JoinGroup => version >= 3,
        ApiKey.Heartbeat => version >= 2,
        ApiKey.LeaveGroup => version >= 2,
        ApiKey.SyncGroup => version >= 2,
        ApiKey.DescribeGroups => version >= 2,
        ApiKey.ListGroups => version >= 2,
        ApiKey.ApiVersions => version >= 2,
        ApiKey.CreateTopics => version >= 3,
        ApiKey.DeleteTopics => version >= 2,
        ApiKey.DeleteRecords => version >= 1,
        ApiKey.InitProducerId => version >= 1,
        ApiKey.AddPartitionsToTxn => version >= 1,
        ApiKey.AddOffsetsToTxn => version >= 1,
        ApiKey.EndTxn => version >= 1,
        ApiKey.TxnOffsetCommit => version >= 1,
        ApiKey.DescribeAcls => version >= 1,
        ApiKey.CreateAcls => version >= 1,
        ApiKey.DeleteAcls => version >= 1,
        ApiKey.DescribeConfigs => version >= 2,
        ApiKey.AlterConfigs => version >= 1,
        ApiKey.AlterReplicaLogDirs => version >= 1,
        ApiKey.DescribeLogDirs => version >= 1,
        ApiKey.CreatePartitions => version >= 1,
        ApiKey.CreateDelegationToken => version >= 1,
        ApiKey.RenewDelegationToken => version >= 1,
        ApiKey.ExpireDelegationToken => version >= 1,
        ApiKey.DescribeDelegationToken => version >= 1,
        ApiKey.DeleteGroups => version >= 1,
        ApiKey.ElectLeaders => true,
        ApiKey.IncrementalAlterConfigs => true,
        ApiKey.AlterPartitionReassignments => true,
        ApiKey.ListPartitionReassignments => true,
        ApiKey.OffsetDelete => true,
        ApiKey.DescribeUserScramCredentials => true,
        ApiKey.AlterUserScramCredentials => true,
        ApiKey.UnregisterBroker => true,
        ApiKey.DescribeTopicPartitions => true,
        _ => false
    };
}

internal interface IBrokerThrottleProvider
{
    int GetRemainingBrokerThrottleMilliseconds(int brokerId);
}
