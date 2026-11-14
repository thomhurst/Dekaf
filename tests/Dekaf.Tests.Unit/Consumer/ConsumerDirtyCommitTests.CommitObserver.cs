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
            var call = new ObservedCommit(observation.Reason, observation.Offsets.ToArray(), requests.Count);
            Calls.Add(call);
            return call;
        }

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
        {
            var call = Calls[^1];
            call.StateMatched = ReferenceEquals(state, call);
            call.Completed = true;
            call.Exception = exception;
        }
    }

    private sealed class ObservedCommit(OffsetCommitReason reason, TopicPartitionOffset[] offsets, int requestsAtStart)
    {
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
