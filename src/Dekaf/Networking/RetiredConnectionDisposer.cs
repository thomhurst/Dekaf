namespace Dekaf.Networking;

/// <summary>
/// Drains a connection after pool routing has removed it, then disposes it.
/// </summary>
internal static class RetiredConnectionDisposer
{
    private static readonly TimeSpan DrainPollInterval = TimeSpan.FromMilliseconds(10);

    public static async ValueTask DrainAndDisposeAsync(
        IKafkaConnection connection,
        CancellationToken cancellationToken)
    {
        // Existing lease holders may still start operations while retirement is open.
        // Seal retirement only after every holder has returned its lease.
        if (connection is IRetirableKafkaConnection retirableConnection)
        {
            while (retirableConnection.LeaseCount > 0)
                await Task.Delay(DrainPollInterval, cancellationToken).ConfigureAwait(false);

            retirableConnection.CompleteRetirement();
        }

        // A send may have entered immediately before retirement was sealed.
        while (HasActiveWork(connection))
            await Task.Delay(DrainPollInterval, cancellationToken).ConfigureAwait(false);

        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private static bool HasActiveWork(IKafkaConnection connection)
        => connection is IRetirableKafkaConnection { ActiveOperationCount: > 0 }
            || connection is IIdleTrackedKafkaConnection { PendingRequestCount: > 0 };
}
