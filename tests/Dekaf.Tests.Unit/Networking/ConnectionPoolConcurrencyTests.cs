using System.Collections.Concurrent;
using Dekaf.Errors;
using Dekaf.Networking;
using NSubstitute;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Concurrency regression tests for race conditions fixed in PR #612:
/// H4: Concurrent CreateConnectionGroupAsync deduplication
/// H5: Concurrent ReplaceConnectionInGroupAsync deduplication
/// M12: Concurrent ReplaceConnectionInGroupAsync + ShrinkConnectionGroupAsync orphan disposal
/// </summary>
public sealed class ConnectionPoolConcurrencyTests
{
    /// <summary>
    /// H4: When N concurrent callers trigger group creation for the same broker,
    /// only one group should be created — exactly connectionsPerBroker connections,
    /// not N x connectionsPerBroker.
    /// </summary>
    [Test]
    public async Task CreateConnectionGroup_ConcurrentCallers_CreatesExactlyConnectionsPerBrokerConnections()
    {
        const int connectionsPerBroker = 3;
        const int concurrentCallers = 10;
        var connectionCount = 0;

        // Factory that counts how many connections are created
        var pool = new ConnectionPool(
            clientId: "test",
            connectionOptions: new ConnectionOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) },
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, index, ct) =>
            {
                Interlocked.Increment(ref connectionCount);
                var conn = Substitute.For<IKafkaConnection>();
                conn.IsConnected.Returns(true);
                conn.BrokerId.Returns(brokerId);
                conn.Host.Returns(host);
                conn.Port.Returns(port);
                return ValueTask.FromResult(conn);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            // Fire N concurrent GetConnectionAsync calls, all of which
            // should trigger CreateConnectionGroupAsync for broker 1
            using var barrier = new Barrier(concurrentCallers);
            var tasks = Enumerable.Range(0, concurrentCallers).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await pool.GetConnectionAsync(1);
            })).ToArray();

            var connections = await Task.WhenAll(tasks);

            // All callers should get a valid connection
            foreach (var conn in connections)
            {
                await Assert.That(conn).IsNotNull();
                await Assert.That(conn.IsConnected).IsTrue();
            }

            // Only connectionsPerBroker connections should have been created,
            // not concurrentCallers x connectionsPerBroker
            await Assert.That(connectionCount).IsEqualTo(connectionsPerBroker);
        }
    }

    /// <summary>
    /// H5: When N concurrent callers detect a dead connection at the same index,
    /// only 1 replacement connection should be created.
    /// </summary>
    [Test]
    public async Task ReplaceConnectionInGroup_ConcurrentCallers_CreatesExactlyOneReplacement()
    {
        const int connectionsPerBroker = 3;
        const int concurrentCallers = 10;
        const int deadIndex = 1;
        var replacementCount = 0;
        var initialCreationDone = 0;

        var pool = new ConnectionPool(
            clientId: "test",
            connectionOptions: new ConnectionOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) },
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, index, ct) =>
            {
                if (Volatile.Read(ref initialCreationDone) != 0)
                {
                    // After initial group creation, count replacements
                    Interlocked.Increment(ref replacementCount);
                }

                var conn = Substitute.For<IKafkaConnection>();
                conn.IsConnected.Returns(true);
                conn.BrokerId.Returns(brokerId);
                conn.Host.Returns(host);
                conn.Port.Returns(port);
                return ValueTask.FromResult(conn);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            // Create the initial connection group
            await pool.GetConnectionAsync(1);
            Volatile.Write(ref initialCreationDone, 1);

            // Now mark connection at deadIndex as disconnected.
            // GetConnectionByIndexAsync will try to replace it.
            // We need to get the actual connection from the group and mark it dead.
            var existingConn = await pool.GetConnectionByIndexAsync(1, deadIndex);
            existingConn.IsConnected.Returns(false);

            // Fire N concurrent calls all requesting the same dead index
            using var barrier = new Barrier(concurrentCallers);
            var tasks = Enumerable.Range(0, concurrentCallers).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await pool.GetConnectionByIndexAsync(1, deadIndex);
            })).ToArray();

            var connections = await Task.WhenAll(tasks);

            // All callers should get a valid connection
            foreach (var conn in connections)
            {
                await Assert.That(conn).IsNotNull();
                await Assert.That(conn.IsConnected).IsTrue();
            }

            // Only 1 replacement connection should have been created
            await Assert.That(replacementCount).IsEqualTo(1);
        }
    }

    /// <summary>
    /// M12: If ShrinkConnectionGroupAsync removes an index while
    /// ReplaceConnectionInGroupAsync is creating a replacement for that index,
    /// the orphaned connection should be disposed (not leaked).
    /// </summary>
    [Test]
    public async Task ReplaceAndShrink_ConcurrentExecution_DisposesOrphanedConnection()
    {
        const int connectionsPerBroker = 2;
        const int shrinkTarget = 1;
        var replacementCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedWithReplacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IKafkaConnection? orphanedConnection = null;
        var initialCreationDone = 0;

        var pool = new ConnectionPool(
            clientId: "test",
            connectionOptions: new ConnectionOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) },
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, index, ct) =>
            {
                var conn = Substitute.For<IKafkaConnection>();
                conn.IsConnected.Returns(true);
                conn.BrokerId.Returns(brokerId);
                conn.Host.Returns(host);
                conn.Port.Returns(port);

                // Only delay during the replacement phase (after initial group creation),
                // and only for the last index which will be shrunk away
                if (Volatile.Read(ref initialCreationDone) != 0 && index == connectionsPerBroker - 1)
                {
                    return new ValueTask<IKafkaConnection>(CreateDelayedConnection(conn));
                }

                return ValueTask.FromResult(conn);
            });

        async Task<IKafkaConnection> CreateDelayedConnection(IKafkaConnection conn)
        {
            orphanedConnection = conn;
            // Signal that the replacement connection has been created
            replacementCreated.SetResult();
            // Wait for the shrink to complete before returning
            await proceedWithReplacement.Task;
            return conn;
        }

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            // Create the initial connection group (2 connections)
            await pool.GetConnectionAsync(1);
            Volatile.Write(ref initialCreationDone, 1);

            // Mark the last connection (index 1) as dead to trigger replacement
            var lastConn = await pool.GetConnectionByIndexAsync(1, connectionsPerBroker - 1);
            lastConn.IsConnected.Returns(false);

            // Start replacement for index 1 (will be delayed by our factory)
            var replaceTask = Task.Run(async () =>
            {
                try
                {
                    await pool.GetConnectionByIndexAsync(1, connectionsPerBroker - 1);
                }
                catch (KafkaException)
                {
                    // Expected: "Connection slot was removed by a concurrent shrink"
                }
            });

            // Wait until the replacement connection has been created (but not yet stored)
            await replacementCreated.Task;

            // Now shrink the group, removing index 1
            await pool.ShrinkConnectionGroupAsync(1, shrinkTarget);

            // Let the replacement proceed — it should detect the shrink and dispose
            proceedWithReplacement.SetResult();

            await replaceTask;

            // The orphaned connection should have been disposed
            await Assert.That(orphanedConnection).IsNotNull();
            await orphanedConnection!.Received(1).DisposeAsync();
        }
    }

    /// <summary>
    /// H4 variant: Verifies that after concurrent group creation,
    /// all callers receive connections from the same group (same set of connections).
    /// </summary>
    [Test]
    public async Task CreateConnectionGroup_ConcurrentCallers_AllReceiveConnectionsFromSameGroup()
    {
        const int connectionsPerBroker = 2;
        const int concurrentCallers = 20;
        var createdConnections = new ConcurrentBag<IKafkaConnection>();

        var pool = new ConnectionPool(
            clientId: "test",
            connectionOptions: new ConnectionOptions { ConnectionTimeout = TimeSpan.FromSeconds(10) },
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, index, ct) =>
            {
                var conn = Substitute.For<IKafkaConnection>();
                conn.IsConnected.Returns(true);
                conn.BrokerId.Returns(brokerId);
                conn.Host.Returns(host);
                conn.Port.Returns(port);
                createdConnections.Add(conn);
                return ValueTask.FromResult(conn);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            using var barrier = new Barrier(concurrentCallers);
            var tasks = Enumerable.Range(0, concurrentCallers).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await pool.GetConnectionAsync(1);
            })).ToArray();

            var results = await Task.WhenAll(tasks);

            // All returned connections should be from the set of created connections
            var createdSet = createdConnections.ToHashSet();
            foreach (var conn in results)
            {
                await Assert.That(createdSet.Contains(conn)).IsTrue();
            }

            // Only connectionsPerBroker connections should exist
            await Assert.That(createdConnections.Count).IsEqualTo(connectionsPerBroker);
        }
    }
}
