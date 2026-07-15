namespace Dekaf.Networking;

internal readonly struct KafkaConnectionLease(
    IKafkaConnection connection,
    IRetirableKafkaConnection? retirableConnection) : IDisposable
{
    public IKafkaConnection Connection { get; } = connection;

    public void Dispose() => retirableConnection?.ReleaseLease();

    internal static bool TryAcquire(IKafkaConnection connection, out KafkaConnectionLease lease)
    {
        if (connection is IRetirableKafkaConnection retirableConnection)
        {
            if (!retirableConnection.TryAcquireLease())
            {
                lease = default;
                return false;
            }

            lease = new KafkaConnectionLease(connection, retirableConnection);
            return true;
        }

        lease = new KafkaConnectionLease(connection, null);
        return true;
    }
}

internal static class ConnectionPoolLeaseExtensions
{
    private static readonly TimeSpan LeaseRetryDelay = TimeSpan.FromMilliseconds(1);

    public static async ValueTask<KafkaConnectionLease> LeaseConnectionAsync(
        this IConnectionPool connectionPool,
        int brokerId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var connection = await connectionPool.GetConnectionAsync(brokerId, cancellationToken)
                .ConfigureAwait(false);
            if (KafkaConnectionLease.TryAcquire(connection, out var lease))
                return lease;

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(LeaseRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async ValueTask<KafkaConnectionLease> LeaseConnectionAsync(
        this IConnectionPool connectionPool,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var connection = await connectionPool.GetConnectionAsync(host, port, cancellationToken)
                .ConfigureAwait(false);
            if (KafkaConnectionLease.TryAcquire(connection, out var lease))
                return lease;

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(LeaseRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async ValueTask<KafkaConnectionLease> LeaseConnectionByIndexAsync(
        this IConnectionPool connectionPool,
        int brokerId,
        int index,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var connection = await connectionPool.GetConnectionByIndexAsync(brokerId, index, cancellationToken)
                .ConfigureAwait(false);
            if (KafkaConnectionLease.TryAcquire(connection, out var lease))
                return lease;

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(LeaseRetryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

}
