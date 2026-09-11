using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;
using Dekaf.Outbox;

namespace Dekaf.StressTests.Tests;

public class OutboxWorkloadStateTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShutdownCancellationCannotHideAnUnrelatedPublisherFailure(bool initialize)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = new OutboxWorkloadState(24, 1, 42);
        await using var publisher = new ObservedOutboxPublisher(new FailedPublisher(), state);
        if (initialize)
            await Assert.That(async () => await publisher.InitializeAsync(cancellation.Token)).Throws<InvalidOperationException>();
        else
            await Assert.That(async () => await publisher.PublishAsync([], "message-id", cancellation.Token)).Throws<InvalidOperationException>();
        await Assert.That(() => state.ThrowIfFailed()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DeliveryCanRaceSaveCompletionButThePhaseMustDrainBoth()
    {
        var state = new OutboxWorkloadState(24, 1, 42);
        var throughput = new ThroughputTracker();
        state.BeginCycle(throughput);
        await Assert.That(state.TryReserve(out var sequence)).IsTrue();
        await Assert.That(state.Process(state.CreatePayload(sequence), 0, 0)).IsTrue();
        await Assert.That(() => state.BeginCycle(new ThroughputTracker())).Throws<InvalidOperationException>();
        state.RecordCommitted(1);
        state.BeginCycle(new ThroughputTracker());
        await Assert.That(throughput.MessageCount).IsEqualTo(1);
    }

    [Test]
    public async Task DuplicatePublicationDoesNotCountAsAnotherUniqueCompletion()
    {
        var state = new OutboxWorkloadState(24, 1, 42);
        var throughput = new ThroughputTracker();
        state.BeginCycle(throughput);
        state.TryReserve(out var sequence);
        var payload = state.CreatePayload(sequence);
        state.RecordCommitted(1);
        await Assert.That(state.Process(payload, 0, 0)).IsTrue();
        await Assert.That(state.Process(payload, 0, 1)).IsFalse();
        await Assert.That(state.Duplicates).IsEqualTo(1);
        await Assert.That(throughput.MessageCount).IsEqualTo(1);
        await Assert.That(state.HasReadThrough([2])).IsTrue();
    }

    [Test]
    public async Task MissingOldestIdentityPreventsSlotReuseEvenWhenOtherRecordsComplete()
    {
        var state = new OutboxWorkloadState(24, 1, 42);
        state.BeginCycle(new ThroughputTracker());
        for (var i = 0; i < OutboxWorkloadState.WindowSize; i++)
            await Assert.That(state.TryReserve(out _)).IsTrue();
        state.RecordCommitted(OutboxWorkloadState.WindowSize);
        for (var i = 1; i < OutboxWorkloadState.WindowSize; i++)
            state.Process(state.CreatePayload(i), 0, i - 1);
        await Assert.That(state.TryReserve(out _)).IsFalse();
        state.Process(state.CreatePayload(0), 0, OutboxWorkloadState.WindowSize - 1);
        await Assert.That(state.TryReserve(out var next)).IsTrue();
        await Assert.That(next).IsEqualTo(OutboxWorkloadState.WindowSize);
    }

    [Test]
    public async Task ForeignUnreservedAndSkippedKafkaRecordsAreRejected()
    {
        var state = new OutboxWorkloadState(24, 1, 42);
        state.BeginCycle(new ThroughputTracker());
        await Assert.That(() => state.Process(state.CreatePayload(0), 0, 0)).Throws<InvalidOperationException>();
        state.TryReserve(out _);
        var foreign = new OutboxWorkloadState(24, 1, 43).CreatePayload(0);
        await Assert.That(() => state.Process(foreign, 0, 0)).Throws<InvalidOperationException>();
        await Assert.That(() => state.Process(state.CreatePayload(0), 0, 1)).Throws<InvalidOperationException>();
        await Assert.That(state.Process(state.CreatePayload(0), 0, 0)).IsTrue();
    }

    private sealed class FailedPublisher : IOutboxPublisher
    {
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("Initialization failed during shutdown."));

        public ValueTask<OutboxPublishResult> PublishAsync(IReadOnlyList<OutboxMessage> messages,
            string messageIdHeaderName, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<OutboxPublishResult>(new InvalidOperationException("Publication failed during shutdown."));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
