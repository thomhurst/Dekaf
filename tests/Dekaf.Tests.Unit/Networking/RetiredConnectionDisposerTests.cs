using Dekaf.Networking;
using Dekaf.Tests.Unit.Producer;

namespace Dekaf.Tests.Unit.Networking;

public sealed class RetiredConnectionDisposerTests
{
    [Test]
    public async Task DrainAndDisposeAsync_CancelledWhileLeased_DoesNotDisposeConnection()
    {
        var connection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)connection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var exception = await Assert.That(async () =>
                await RetiredConnectionDisposer.DrainAndDisposeAsync(
                    connection,
                    cancellationSource.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(connection.DisposeCalls).IsEqualTo(0);
        retirableConnection.ReleaseLease();
    }

    [Test]
    public async Task DrainAndDisposeAsync_PendingRequests_DisposesOnlyAfterCountDrops()
    {
        var pendingCount = new DrainCount(initialValue: 1);
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new ConnectionPoolTests.TestIdleConnection(1, "localhost", 9092)
        {
            PendingRequestCountProvider = pendingCount.Read,
            DisposalStarted = disposalStarted,
        };

        var drain = RetiredConnectionDisposer.DrainAndDisposeAsync(connection, CancellationToken.None).AsTask();

        // A second nonzero read proves the disposer polled again instead of disposing.
        // Disposal also ends the wait, so a disposer that skips the drain fails instead of hanging.
        await Task.WhenAny(pendingCount.SecondNonZeroRead.Task, disposalStarted.Task);
        await Assert.That(connection.DisposeCount).IsEqualTo(0);
        await Assert.That(drain.IsCompleted).IsFalse();

        pendingCount.Value = 0;
        await drain;

        await Assert.That(connection.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task DrainAndDisposeAsync_ActiveOperations_DisposesOnlyAfterCountDrops()
    {
        var activeCount = new DrainCount(initialValue: 1);
        var connection = new TestKafkaConnection
        {
            ActiveOperationCountProvider = activeCount.Read,
        };

        var drain = RetiredConnectionDisposer.DrainAndDisposeAsync(connection, CancellationToken.None).AsTask();

        // A second nonzero read proves the disposer polled again instead of disposing.
        // Disposal also ends the wait, so a disposer that skips the drain fails instead of hanging.
        await Task.WhenAny(activeCount.SecondNonZeroRead.Task, connection.DisposeStarted.Task);
        await Assert.That(connection.DisposeCalls).IsEqualTo(0);
        await Assert.That(connection.CompleteRetirementCalls).IsEqualTo(1);
        await Assert.That(drain.IsCompleted).IsFalse();

        activeCount.Value = 0;
        await drain;

        await Assert.That(connection.DisposeCalls).IsEqualTo(1);
    }

    private sealed class DrainCount(int initialValue)
    {
        private int _value = initialValue;
        private int _nonZeroReads;

        public TaskCompletionSource SecondNonZeroRead { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int Value
        {
            get => Volatile.Read(ref _value);
            set => Volatile.Write(ref _value, value);
        }

        public int Read()
        {
            var value = Value;
            if (value > 0 && Interlocked.Increment(ref _nonZeroReads) == 2)
                SecondNonZeroRead.TrySetResult();

            return value;
        }
    }
}
