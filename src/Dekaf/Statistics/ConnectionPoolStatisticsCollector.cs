using System.Collections.Concurrent;

namespace Dekaf.Statistics;

/// <summary>
/// Internal collector for connection pool statistics counters.
/// Thread-safe for concurrent access from multiple connection pool operations.
/// Uses Interlocked operations for all counter updates to avoid locks on hot paths.
/// </summary>
internal sealed class ConnectionPoolStatisticsCollector
{
    // Global counters
    private long _totalConnectionsCreated;
    private long _totalConnectionsClosed;
    private long _totalErrors;

    // Per-broker counters
    private readonly ConcurrentDictionary<int, BrokerConnectionCounters> _brokerCounters = new();

    /// <summary>
    /// Records that a connection was created to a broker.
    /// </summary>
    public void RecordConnectionCreated(int brokerId, string host, int port)
    {
        Interlocked.Increment(ref _totalConnectionsCreated);

        var counters = _brokerCounters.GetOrAdd(brokerId, static (_, args) => new BrokerConnectionCounters(args.Host, args.Port),
            (Host: host, Port: port));
        counters.IncrementConnectionsCreated();
        counters.IncrementActiveConnections();
    }

    /// <summary>
    /// Records that a connection to a broker was closed.
    /// </summary>
    public void RecordConnectionClosed(int brokerId)
    {
        Interlocked.Increment(ref _totalConnectionsClosed);

        if (_brokerCounters.TryGetValue(brokerId, out var counters))
        {
            counters.IncrementConnectionsClosed();
            counters.DecrementActiveConnections();
        }
    }

    /// <summary>
    /// Records that a request was sent to a broker.
    /// </summary>
    public void RecordRequestSent(int brokerId)
    {
        if (_brokerCounters.TryGetValue(brokerId, out var counters))
        {
            counters.IncrementRequestsSent();
            counters.IncrementPendingRequests();
        }
    }

    /// <summary>
    /// Records that a response was received from a broker.
    /// </summary>
    public void RecordResponseReceived(int brokerId)
    {
        if (_brokerCounters.TryGetValue(brokerId, out var counters))
        {
            counters.IncrementResponsesReceived();
            counters.DecrementPendingRequests();
        }
    }

    /// <summary>
    /// Records that an error occurred on a connection to a broker.
    /// </summary>
    public void RecordError(int brokerId)
    {
        Interlocked.Increment(ref _totalErrors);

        if (_brokerCounters.TryGetValue(brokerId, out var counters))
        {
            counters.IncrementErrors();
        }
    }

    /// <summary>
    /// Collects a snapshot of connection pool statistics.
    /// </summary>
    public ConnectionPoolStatistics Collect()
    {
        var totalConnectionsCreated = Interlocked.Read(ref _totalConnectionsCreated);
        var totalConnectionsClosed = Interlocked.Read(ref _totalConnectionsClosed);
        var totalErrors = Interlocked.Read(ref _totalErrors);

        var brokerStats = new Dictionary<int, BrokerConnectionStatistics>();
        var totalActive = 0;
        var totalIdle = 0;

        foreach (var (brokerId, counters) in _brokerCounters)
        {
            var (host, port, activeConnections, connectionsCreated, connectionsClosed,
                pendingRequests, requestsSent, responsesReceived, errors) = counters.GetStats();

            brokerStats[brokerId] = new BrokerConnectionStatistics
            {
                NodeId = brokerId,
                Host = host,
                Port = port,
                ConnectionCount = activeConnections,
                TotalConnectionsCreated = connectionsCreated,
                TotalConnectionsClosed = connectionsClosed,
                PendingRequests = pendingRequests,
                TotalRequestsSent = requestsSent,
                TotalResponsesReceived = responsesReceived,
                TotalErrors = errors
            };

            totalActive += activeConnections;
            if (activeConnections > 0 && pendingRequests == 0)
            {
                totalIdle += activeConnections;
            }
            else if (activeConnections > 0)
            {
                // When there are pending requests, some connections may still be idle
                // but we can't know exactly which ones. We estimate based on the minimum
                // of active connections and pending requests.
                totalIdle += Math.Max(0, activeConnections - pendingRequests);
            }
        }

        return new ConnectionPoolStatistics
        {
            ActiveConnections = totalActive,
            IdleConnections = totalIdle,
            TotalConnectionsCreated = totalConnectionsCreated,
            TotalConnectionsClosed = totalConnectionsClosed,
            TotalErrors = totalErrors,
            Brokers = brokerStats
        };
    }

    internal sealed class BrokerConnectionCounters
    {
        private readonly string _host;
        private readonly int _port;
        private int _activeConnections;
        private long _connectionsCreated;
        private long _connectionsClosed;
        private int _pendingRequests;
        private long _requestsSent;
        private long _responsesReceived;
        private long _errors;

        public BrokerConnectionCounters(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void IncrementActiveConnections() => Interlocked.Increment(ref _activeConnections);
        public void DecrementActiveConnections() => Interlocked.Decrement(ref _activeConnections);
        public void IncrementConnectionsCreated() => Interlocked.Increment(ref _connectionsCreated);
        public void IncrementConnectionsClosed() => Interlocked.Increment(ref _connectionsClosed);
        public void IncrementPendingRequests() => Interlocked.Increment(ref _pendingRequests);
        public void DecrementPendingRequests() => Interlocked.Decrement(ref _pendingRequests);
        public void IncrementRequestsSent() => Interlocked.Increment(ref _requestsSent);
        public void IncrementResponsesReceived() => Interlocked.Increment(ref _responsesReceived);
        public void IncrementErrors() => Interlocked.Increment(ref _errors);

        public (string Host, int Port, int ActiveConnections, long ConnectionsCreated, long ConnectionsClosed,
            int PendingRequests, long RequestsSent, long ResponsesReceived, long Errors) GetStats() =>
            (_host,
             _port,
             Volatile.Read(ref _activeConnections),
             Interlocked.Read(ref _connectionsCreated),
             Interlocked.Read(ref _connectionsClosed),
             Volatile.Read(ref _pendingRequests),
             Interlocked.Read(ref _requestsSent),
             Interlocked.Read(ref _responsesReceived),
             Interlocked.Read(ref _errors));
    }
}
