using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public class PartitionedOffsetGapTests
{
    [Test]
    [Arguments(0L, 1L, 2L)]
    [Arguments(10L, 12L, 13L)]
    [Arguments(1000L, 1002L, 1010L)]
    [Arguments(10L, 4_294_967_296L, 9_000_000_000L)]
    public async Task DeliveredPrefix_AdvancesAcrossGaps(long first, long second, long third)
    {
        var lane = CreateLane();
        foreach (var (offset, epoch) in new[] { (first, 1), (second, 2), (third, 3) })
        {
            var message = Deliver(lane, offset, epoch);
            lane.MarkProcessed(message);
            await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, offset + 1, epoch));
        }
        await Assert.That(lane.LastProcessedOffset).IsEqualTo(third);
    }

    [Test]
    [Arguments(0L, 1L, 2L)]
    [Arguments(10L, 12L, 13L)]
    [Arguments(1000L, 1002L, 1010L)]
    [Arguments(10L, 4_294_967_296L, 9_000_000_000L)]
    public async Task OutOfOrderCompletion_DoesNotPassEarlierDeliveredRecord(long first, long second, long third)
    {
        var lane = CreateLane();
        var firstMessage = Deliver(lane, first, 1);
        var secondMessage = Deliver(lane, second, 2);
        var thirdMessage = Deliver(lane, third, 3);
        lane.MarkProcessed(thirdMessage);
        await Assert.That(lane.GetCommitOffset()).IsNull();
        lane.MarkProcessed(firstMessage);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, first + 1, 1));
        lane.MarkProcessed(secondMessage);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, third + 1, 3));
        await Assert.That(lane.LastProcessedOffset).IsEqualTo(third);
    }

    [Test]
    public async Task UncompletedEarlierRecord_BlocksLaterOffsets()
    {
        var lane = CreateLane();
        _ = Deliver(lane, 10, 1);
        lane.MarkProcessed(Deliver(lane, 12, 2));
        lane.MarkProcessed(Deliver(lane, 13, 3));
        await Assert.That(lane.GetCommitOffset()).IsNull();
        await Assert.That(lane.LastProcessedOffset).IsEqualTo(13);
    }

    [Test]
    public async Task DuplicateCompletion_DoesNotReleaseAnotherPendingRecord()
    {
        var lane = CreateLane();
        var first = Deliver(lane, 10, 1);
        var second = Deliver(lane, 12, 2);
        var third = Deliver(lane, 13, 3);
        lane.MarkProcessed(first);
        lane.MarkProcessed(first);
        lane.MarkProcessed(third);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 11, 1));
        lane.MarkProcessed(second);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 14, 3));
    }

    [Test]
    public async Task LongStreamWithGaps_ReleasesCompletedBookkeeping()
    {
        var lane = CreateLane();
        for (var index = 0; index < 10_000; index++)
            lane.MarkProcessed(Deliver(lane, 10 + index * 2L, index));
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 20_009, 9_999));
        await Assert.That(Ranges(lane).Count).IsEqualTo(0);
    }

    [Test]
    public async Task BlockedFirstRecord_MergesCompletedTailWithBoundedStorage()
    {
        var lane = CreateLane();
        var first = Deliver(lane, 10, 1);
        for (var index = 1; index < 10_000; index++)
            lane.MarkProcessed(Deliver(lane, 10 + index * 2L, index));
        await Assert.That(lane.GetCommitOffset()).IsNull();
        await Assert.That(Ranges(lane).Count).IsEqualTo(1);
        lane.MarkProcessed(first);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 20_009, 9_999));
        await Assert.That(Ranges(lane).Count).IsEqualTo(0);
    }

    [Test]
    public async Task RandomCompletionOrder_MatchesDeliveredPrefixAndIgnoresDuplicates()
    {
        const int count = 512;
        var random = new Random(3047);
        for (var round = 0; round < 20; round++)
        {
            var lane = CreateLane();
            var messages = new ConsumeResult<string, string>[count];
            var order = new int[count];
            var done = new bool[count];
            long offset = 1000;
            for (var index = 0; index < count; index++)
            {
                offset += random.Next(1, 100);
                messages[index] = Deliver(lane, offset, index);
                order[index] = index;
            }
            random.Shuffle(order);
            var prefix = -1;
            foreach (var index in order)
            {
                lane.MarkProcessed(messages[index]);
                done[index] = true;
                while (prefix + 1 < count && done[prefix + 1])
                    prefix++;
                // Repeat arbitrary prior completions, including interiors of merged ranges.
                lane.MarkProcessed(messages[index]);
                TopicPartitionOffset? expected = prefix < 0 ? null
                    : new TopicPartitionOffset("gaps", 0, messages[prefix].Offset + 1, prefix);
                await Assert.That(lane.GetCommitOffset()).IsEqualTo(expected);
            }
            await Assert.That(Ranges(lane).Count).IsEqualTo(0);
        }
    }

    private static CompletedOffsetRanges Ranges(PartitionLane<string, string> lane) =>
        (CompletedOffsetRanges)typeof(PartitionLane<string, string>)
            .GetField("_completedRanges", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lane)!;

    [Test]
    public async Task ConcurrentPublicationAndCompletion_PreservesDeliveredPredecessors()
    {
        const int count = 10_000;
        var lane = CreateLane();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var writer = Task.Run(async () =>
        {
            for (var index = 0; index < count; index++)
            {
                var message = new ConsumeResult<string, string>("gaps", 0, 10 + index * 2L,
                    "key", "value", null, 0, TimestampType.CreateTime, index);
                while (!lane.TryEnqueueForTest(message))
                    await lane.WaitToWriteAsync(timeout.Token);
            }
        }, timeout.Token);
        for (var index = 0; index < count; index++)
        {
            ConsumeResult<string, string> message;
            while (!lane.TryReadMessage(out message))
                await lane.WaitToReadMessageAsync(timeout.Token);
            lane.MarkProcessed(message);
        }
        await writer.WaitAsync(timeout.Token);
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 20_009, 9_999));
        await Assert.That(Ranges(lane).Count).IsEqualTo(0);
    }

    [Test]
    public async Task RejectedEnqueue_DoesNotBecomeDeliveredPredecessor()
    {
        var lane = new PartitionLane<string, string>(new TopicPartition("gaps", 0), 1,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var first = new ConsumeResult<string, string>("gaps", 0, 10, "key", "value", null, 0,
            TimestampType.CreateTime, 1);
        var rejected = new ConsumeResult<string, string>("gaps", 0, 12, "key", "value", null, 0,
            TimestampType.CreateTime, 2);
        await Assert.That(lane.TryEnqueueForTest(first)).IsTrue();
        await Assert.That(lane.TryEnqueueForTest(rejected)).IsFalse();
        await Assert.That(lane.TryReadMessage(out var delivered)).IsTrue();
        lane.MarkProcessed(delivered);
        lane.MarkProcessed(Deliver(lane, 13, 3));
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 14, 3));
    }

    [Test]
    public async Task EofBetweenRecords_DoesNotBecomeDeliveredPredecessor()
    {
        var lane = CreateLane();
        lane.MarkProcessed(Deliver(lane, 10, 1));
        await Assert.That(lane.TryEnqueueForTest(ConsumeResult<string, string>.CreatePartitionEof("gaps", 0, 11))).IsTrue();
        await Assert.That(lane.TryReadMessage(out var eof)).IsTrue();
        lane.MarkProcessed(eof);
        lane.MarkProcessed(Deliver(lane, 13, 3));
        await Assert.That(lane.GetCommitOffset()).IsEqualTo(new TopicPartitionOffset("gaps", 0, 14, 3));
    }

    private static PartitionLane<string, string> CreateLane() =>
        new(new TopicPartition("gaps", 0), 16, static (_, _) => default, static _ => { }, static (_, _) => { });

    private static ConsumeResult<string, string> Deliver(PartitionLane<string, string> lane, long offset, int epoch)
    {
        var message = new ConsumeResult<string, string>("gaps", 0, offset, "key", "value", null, 0,
            TimestampType.CreateTime, epoch);
        if (!lane.TryEnqueueForTest(message) || !lane.TryReadMessage(out var delivered))
            throw new InvalidOperationException("Test lane did not deliver the expected record.");
        return delivered;
    }
}
