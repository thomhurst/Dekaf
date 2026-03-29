using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests that the heartbeat loop uses PeriodicTimer for resilience against thread pool starvation.
/// These tests verify that heartbeats are sent at regular intervals and that cancellation is prompt.
/// </summary>
public sealed class HeartbeatTimerResilienceTests : IAsyncDisposable
{
    private readonly IConnectionPool _connectionPool;
    private readonly IKafkaConnection _connection;
    private readonly MetadataManager _metadataManager;
    private int _heartbeatCount;
    private readonly TaskCompletionSource _heartbeatThresholdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HeartbeatTimerResilienceTests()
    {
        _connectionPool = Substitute.For<IConnectionPool>();
        _connection = Substitute.For<IKafkaConnection>();

        _connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _metadataManager = new MetadataManager(_connectionPool, ["localhost:9092"]);

        _metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "test-topic",
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _metadataManager.DisposeAsync();
    }

    private void SetupSuccessfulJoinFlowWithHeartbeatCounting()
    {
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 1,
                Leader = "member-1",
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var count = Interlocked.Increment(ref _heartbeatCount);
                if (count >= 3)
                    _heartbeatThresholdTcs.TrySetResult();
                return ValueTask.FromResult(new HeartbeatResponse
                {
                    ErrorCode = ErrorCode.None
                });
            });
    }

    [Test]
    public async Task HeartbeatLoop_SendsMultipleHeartbeats_WithPeriodicTimer()
    {
        // Arrange - use a short heartbeat interval to verify periodic behavior
        SetupSuccessfulJoinFlowWithHeartbeatCounting();
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "test-group",
            HeartbeatIntervalMs = 50 // 50ms interval
        };

        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        // Act - join group (starts heartbeat loop)
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Wait for at least 3 heartbeats using deterministic signaling instead of polling.
        // Use a generous timeout — CI runners under thread pool starvation can delay PeriodicTimer ticks.
        await _heartbeatThresholdTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Assert - at least 3 heartbeats sent, confirming periodic timer works
        var count = Volatile.Read(ref _heartbeatCount);
        await Assert.That(count).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task HeartbeatLoop_CancellationIsPrompt()
    {
        // Arrange - verify that stopping the heartbeat loop completes quickly
        // (PeriodicTimer.WaitForNextTickAsync respects cancellation tokens promptly)
        SetupSuccessfulJoinFlowWithHeartbeatCounting();
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "test-group",
            HeartbeatIntervalMs = 60_000 // 60 second interval - very long
        };

        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Act - stop heartbeat immediately (should not wait for the 60s timer tick)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await coordinator.StopHeartbeatAsync();
        stopwatch.Stop();

        // Assert - cancellation should complete well under the 60s heartbeat interval
        // Using 5 seconds as threshold to account for CI variability
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(5000);
    }

}
