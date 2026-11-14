using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerDirtyCommitTests
{
    [Test]
    public async Task CommitObserver_AutoCommit_ObservesPeriodicCommitAroundTheRequest()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await RunAutoCommitCycleAsync(consumer);

        var call = observer.Calls.Single();
        await Assert.That(call.Reason).IsEqualTo(OffsetCommitReason.Periodic);
        await Assert.That(call.Offsets).IsEquivalentTo([new TopicPartitionOffset("topic-a", 0, 10)]);
        await Assert.That(call.RequestsAtStart).IsEqualTo(0);
        await Assert.That(call.Completed).IsTrue();
        await Assert.That(call.Exception).IsNull();
        await Assert.That(call.StateMatched).IsTrue();
        await Assert.That(requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitObserver_AutoCommitFailure_ReportsTheExceptionThenTheRetry()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests,
            new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]),
            OffsetCommitMode.Auto,
            commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Count).IsEqualTo(2);
        await Assert.That(observer.Calls[0].Reason).IsEqualTo(OffsetCommitReason.Periodic);
        await Assert.That(observer.Calls[0].Exception).IsTypeOf<GroupException>();
        await Assert.That(observer.Calls[1].Reason).IsEqualTo(OffsetCommitReason.Periodic);
        await Assert.That(observer.Calls[1].Completed).IsTrue();
        await Assert.That(observer.Calls[1].Exception).IsNull();
    }

    [Test]
    public async Task CommitObserver_Close_ObservesStopCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await (ValueTask)typeof(KafkaConsumer<string, string>)
            .GetMethod("CommitPendingOffsetsOnCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [CancellationToken.None])!;

        var call = observer.Calls.Single();
        await Assert.That(call.Reason).IsEqualTo(OffsetCommitReason.Stop);
        await Assert.That(call.Offsets).IsEquivalentTo([new TopicPartitionOffset("topic-a", 0, 10)]);
        await Assert.That(call.Exception).IsNull();
    }

    [Test]
    public async Task CommitObserver_Revoke_ObservesRevokeCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await CommitRevokedOffsetsAsync(consumer, [new TopicPartition("topic-a", 0)]);

        var call = observer.Calls.Single();
        await Assert.That(call.Reason).IsEqualTo(OffsetCommitReason.Revoke);
        await Assert.That(call.Offsets).IsEquivalentTo([new TopicPartitionOffset("topic-a", 0, 10)]);
        await Assert.That(call.Exception).IsNull();
        await Assert.That(requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitObserver_ExplicitCommitAsync_IsNotObserved()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await consumer.CommitAsync(CancellationToken.None);
        await consumer.CommitAsync([new TopicPartitionOffset("topic-a", 1, 5)], CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(observer.Calls).IsEmpty();
    }

    [Test]
    public async Task CommitObserver_WhenObserverThrows_CommitProceedsUnchanged()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: new ThrowingCommitObserver());
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await RunAutoCommitCycleAsync(consumer);
        await RunAutoCommitCycleAsync(consumer);

        // Committed once: the second cycle finds nothing dirty, so the failure did not leave the
        // offset uncommitted.
        await Assert.That(requests.Count).IsEqualTo(1);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
            [new TopicPartitionOffset("topic-a", 0, 10)]);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_ReportsStoresSinceThePreviousCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, enableAutoOffsetStore: false, commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        // Offsets with gaps: the count is of stores, not an offset difference.
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 50));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 51));
        consumer.StoreOffsets([new TopicPartitionOffset("topic-a", 1, 7), new TopicPartitionOffset("topic-a", 1, 9)]);
        await RunAutoCommitCycleAsync(consumer);

        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 12));
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Count).IsEqualTo(2);
        await Assert.That(observer.Calls[0].RecordCount).IsEqualTo(5L);
        await Assert.That(observer.Calls[0].CompletedRecordCount).IsEqualTo(5L);
        await Assert.That(observer.Calls[1].Offsets).IsEquivalentTo([new TopicPartitionOffset("topic-a", 1, 12)]);
        await Assert.That(observer.Calls[1].RecordCount).IsEqualTo(1L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_FailedCommitKeepsItsCountForTheNextCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests,
            new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]),
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));

        await RunAutoCommitCycleAsync(consumer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 12));
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Count).IsEqualTo(3);
        await Assert.That(observer.Calls[0].Exception).IsTypeOf<GroupException>();
        await Assert.That(observer.Calls[0].RecordCount).IsEqualTo(2L);
        await Assert.That(observer.Calls[1].Exception).IsNull();
        await Assert.That(observer.Calls[1].RecordCount).IsEqualTo(2L);
        await Assert.That(observer.Calls[2].RecordCount).IsEqualTo(1L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_RevokeAndCloseCommitsReportStores()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, enableAutoOffsetStore: false, commitObserver: observer);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 5));

        await CommitRevokedOffsetsAsync(consumer, [new TopicPartition("topic-a", 0)]);
        await (ValueTask)typeof(KafkaConsumer<string, string>)
            .GetMethod("CommitPendingOffsetsOnCloseAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [CancellationToken.None])!;

        await Assert.That(observer.Calls.Count).IsEqualTo(2);
        await Assert.That(observer.Calls[0].Reason).IsEqualTo(OffsetCommitReason.Revoke);
        await Assert.That(observer.Calls[0].RecordCount).IsEqualTo(2L);
        await Assert.That(observer.Calls[1].Reason).IsEqualTo(OffsetCommitReason.Stop);
        await Assert.That(observer.Calls[1].RecordCount).IsEqualTo(1L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_ExplicitCommitEndsTheCountOfWhatItCommitted()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, enableAutoOffsetStore: false, commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));

        await consumer.CommitAsync(CancellationToken.None);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 12));
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Single().RecordCount).IsEqualTo(1L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_StoreRepeatedDuringACommitIsCountedByTheNext()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        var firstCommit = new HeldFirstCommit();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            onOffsetCommitAsync: firstCommit.OnCommitAsync,
            commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        var commit = RunAutoCommitCycleAsync(consumer);
        await firstCommit.Started;
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        firstCommit.Release();
        await commit;
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Count).IsEqualTo(2);
        await Assert.That(observer.Calls[0].RecordCount).IsEqualTo(1L);
        await Assert.That(observer.Calls[1].RecordCount).IsEqualTo(2L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_CommitOfAPartitionReassignedMeanwhileLeavesItsNewCount()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        var firstCommit = new HeldFirstCommit();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            onOffsetCommitAsync: firstCommit.OnCommitAsync,
            commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        var partition = new TopicPartition("topic-a", 0);
        consumer.Assign(partition);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));

        var commit = RunAutoCommitCycleAsync(consumer);
        await firstCommit.Started;
        consumer.Unassign();
        consumer.Assign(partition);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 30));
        firstCommit.Release();
        await commit;
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Count).IsEqualTo(2);
        await Assert.That(observer.Calls[0].RecordCount).IsEqualTo(2L);
        await Assert.That(observer.Calls[1].RecordCount).IsEqualTo(1L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_ExplicitOffsetCommitEndsTheCountOfTheStoresBeforeIt()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        var firstCommit = new HeldFirstCommit();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            onOffsetCommitAsync: firstCommit.OnCommitAsync,
            commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));
        // Stored with a leader epoch the explicit commit below does not name.
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 5, 2));

        var commit = consumer.CommitAsync(
            [new TopicPartitionOffset("topic-a", 0, 11), new TopicPartitionOffset("topic-a", 1, 5)],
            CancellationToken.None);
        await firstCommit.Started;
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 12));
        firstCommit.Release();
        await commit;
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 1, 6, 2));
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Single().RecordCount).IsEqualTo(2L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_ExplicitCommitBelowTheStoredOffsetKeepsTheCount()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, enableAutoOffsetStore: false, commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 11));

        await consumer.CommitAsync([new TopicPartitionOffset("topic-a", 0, 5)], CancellationToken.None);
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Single().RecordCount).IsEqualTo(2L);
    }

    [Test]
    public async Task CommitObserver_WithAutoOffsetStore_ReportsZeroRecords()
    {
        var requests = new List<OffsetCommitRequest>();
        var observer = new RecordingCommitObserver(requests);
        await using var consumer = CreateConsumer(
            requests, ErrorCode.None, OffsetCommitMode.Auto, commitObserver: observer);
        SetCoordinatorState(consumer, CoordinatorState.Stable);
        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10));

        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(observer.Calls.Single().RecordCount).IsEqualTo(0L);
        await Assert.That(observer.Calls.Single().CompletedRecordCount).IsEqualTo(0L);
    }

    [Test]
    public async Task WithCommitObserver_SetsConsumerOption()
    {
        var observer = new ThrowingCommitObserver();
        await using var consumer = (KafkaConsumer<string, string>)Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithCommitObserver(observer)
            .Build();

        var options = (ConsumerOptions)typeof(KafkaConsumer<string, string>)
            .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;
        await Assert.That(options.CommitObserver).IsSameReferenceAs(observer);
    }

    private sealed class RecordingCommitObserver(List<OffsetCommitRequest> requests) : IOffsetCommitObserver
    {
        public List<ObservedCommit> Calls { get; } = [];

        public object? OnCommitStarting(OffsetCommitObservation observation)
        {
            var call = new ObservedCommit(
                observation.Reason, observation.Offsets.ToArray(), requests.Count, observation.CompletedRecordCount);
            Calls.Add(call);
            return call;
        }

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
        {
            var call = Calls[^1];
            call.StateMatched = ReferenceEquals(state, call);
            call.Completed = true;
            call.Exception = exception;
            call.CompletedRecordCount = observation.CompletedRecordCount;
        }
    }

    /// <summary>Holds the first OffsetCommit request in flight until released.</summary>
    private sealed class HeldFirstCommit
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        public Task Started => _started.Task;

        public void Release() => _release.SetResult();

        public async ValueTask OnCommitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) != 1)
                return;

            _started.SetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ObservedCommit(
        OffsetCommitReason reason,
        TopicPartitionOffset[] offsets,
        int requestsAtStart,
        long recordCount)
    {
        public long RecordCount { get; } = recordCount;
        public long? CompletedRecordCount { get; set; }
        public OffsetCommitReason Reason { get; } = reason;
        public TopicPartitionOffset[] Offsets { get; } = offsets;
        public int RequestsAtStart { get; } = requestsAtStart;
        public bool Completed { get; set; }
        public bool StateMatched { get; set; }
        public Exception? Exception { get; set; }
    }

    private sealed class ThrowingCommitObserver : IOffsetCommitObserver
    {
        public object? OnCommitStarting(OffsetCommitObservation observation)
            => throw new InvalidOperationException("observer start failed");

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
            => throw new InvalidOperationException("observer completion failed");
    }
}
