using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Concurrency tests for the active consumed position seqlock
/// (<c>PublishActiveConsumedPosition</c> / <c>TryReadActiveConsumedPosition</c> /
/// <c>ClearActiveConsumedPosition</c>). In auto-commit mode this seqlock hands the
/// consume thread's position to commit/close/GetPosition readers; a torn read here
/// would commit an offset from one partition attributed to another — an offset-safety
/// violation. Covers issue #2061.
/// </summary>
public sealed class ActiveConsumedPositionSeqlockTests
{
    private delegate void PublishDelegate(TopicPartition partition, long position, int leaderEpoch);
    private delegate bool TryReadDelegate(out TopicPartition partition, out long position, out int leaderEpoch, out int version);
    private delegate void ClearDelegate(TopicPartition partition, long position);

    [Test]
    public async Task PublishThenRead_ReturnsExactPublishedTuple()
    {
        await using var consumer = CreateConsumer();
        var (publish, tryRead, clear) = CreateSeqlockDelegates(consumer);
        var partition = new TopicPartition("topic-a", 3);

        publish(partition, 42, 7);

        var read = tryRead(out var readPartition, out var position, out var leaderEpoch, out _);

        await Assert.That(read).IsTrue();
        await Assert.That(readPartition).IsEqualTo(partition);
        await Assert.That(position).IsEqualTo(42);
        await Assert.That(leaderEpoch).IsEqualTo(7);

        clear(partition, 42);

        await Assert.That(tryRead(out _, out _, out _, out _)).IsFalse();
    }

    [Test]
    public async Task Clear_WithStalePosition_DoesNotClearNewerPublish()
    {
        await using var consumer = CreateConsumer();
        var (publish, tryRead, clear) = CreateSeqlockDelegates(consumer);
        var partition = new TopicPartition("topic-a", 0);

        publish(partition, 10, 1);
        publish(partition, 11, 1);

        // A commit thread that snapshotted position 10 must not wipe the newer position 11.
        clear(partition, 10);

        var read = tryRead(out var readPartition, out var position, out _, out _);

        await Assert.That(read).IsTrue();
        await Assert.That(readPartition).IsEqualTo(partition);
        await Assert.That(position).IsEqualTo(11);
    }

    [Test]
    public async Task ConcurrentPublishReadClear_NeverExposesTornTuple()
    {
        await using var consumer = CreateConsumer();
        var (publish, tryRead, clear) = CreateSeqlockDelegates(consumer);

        // Every published tuple is fully derivable from its position, so any reader
        // observing a (topic, partition, epoch) that does not match its position has
        // seen a torn mix of two writes.
        const long requiredDistinctPositions = 1_001;
        using var workersStop = new CancellationTokenSource();
        var requiredPositionsObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observedPositions = new ConcurrentDictionary<long, byte>();
        var violations = new List<string>();
        var violationLock = new object();
        long distinctPositionCount = 0;

        void RecordViolation(string message)
        {
            lock (violationLock)
            {
                if (violations.Count < 10)
                    violations.Add(message);
            }
        }

        var readers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            while (!workersStop.IsCancellationRequested)
            {
                if (!tryRead(out var partition, out var position, out var leaderEpoch, out _))
                    continue;

                if (observedPositions.TryAdd(position, 0)
                    && Interlocked.Increment(ref distinctPositionCount) == requiredDistinctPositions)
                {
                    requiredPositionsObserved.TrySetResult();
                }

                var expected = ExpectedTupleFor(position);
                if (partition != expected.Partition || leaderEpoch != expected.LeaderEpoch)
                {
                    RecordViolation(
                        $"Read ({partition}, position {position}, epoch {leaderEpoch}) " +
                        $"but position {position} was published as ({expected.Partition}, epoch {expected.LeaderEpoch})");
                }
            }
        })).ToArray();

        var clearer = Task.Run(() =>
        {
            while (!workersStop.IsCancellationRequested)
            {
                if (tryRead(out var partition, out var position, out _, out _))
                    clear(partition, position);
            }
        });

        // Single publisher mirrors production: only the consume thread publishes.
        var publisher = Task.Run(() =>
        {
            for (long position = 1; !workersStop.IsCancellationRequested; position++)
            {
                var tuple = ExpectedTupleFor(position);
                publish(tuple.Partition, position, tuple.LeaderEpoch);
            }
        });

        try
        {
            await requiredPositionsObserved.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            // Assertions below report torn tuples first, then insufficient progress.
        }
        finally
        {
            workersStop.Cancel();
            await Task.WhenAll([publisher, .. readers, clearer]);
        }

        await Assert.That(violations).IsEmpty();
        // Distinct positions guarantee the readers crossed same-partition and full-write transitions.
        await Assert.That(Interlocked.Read(ref distinctPositionCount))
            .IsGreaterThanOrEqualTo(requiredDistinctPositions);
    }

    /// <summary>
    /// Derives the unique (partition, epoch) tuple published for a given position.
    /// Runs of 16 consecutive positions share a partition so both the same-partition
    /// fast path and the full seqlock write path in PublishActiveConsumedPosition are exercised.
    /// </summary>
    private static (TopicPartition Partition, int LeaderEpoch) ExpectedTupleFor(long position)
    {
        var partitionIndex = (int)((position / 16) % 5);
        var topic = partitionIndex % 2 == 0 ? "topic-even" : "topic-odd";
        return (new TopicPartition(topic, partitionIndex), partitionIndex * 31 + 7);
    }

    private static (PublishDelegate Publish, TryReadDelegate TryRead, ClearDelegate Clear) CreateSeqlockDelegates(
        KafkaConsumer<string, string> consumer)
    {
        var type = typeof(KafkaConsumer<string, string>);

        var publish = type
            .GetMethod("PublishActiveConsumedPosition", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<PublishDelegate>(consumer);

        var tryRead = type
            .GetMethod("TryReadActiveConsumedPosition", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<TryReadDelegate>(consumer);

        var clear = type
            .GetMethod(
                "ClearActiveConsumedPosition",
                BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(TopicPartition), typeof(long)])!
            .CreateDelegate<ClearDelegate>(consumer);

        return (publish, tryRead, clear);
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Auto
            },
            Serializers.String,
            Serializers.String);
    }
}
