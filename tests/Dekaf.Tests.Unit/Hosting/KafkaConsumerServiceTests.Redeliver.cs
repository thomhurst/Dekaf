using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

public sealed partial class KafkaConsumerServiceTests
{
    [Test]
    public async Task ProcessWithRetriesAsync_DefaultDisposition_RewindsPausesAndResumesWithoutStoringOffset()
    {
        var consumer = CreateConsumerSubstitute();
        ConfigureStrictManualOffsetStore(consumer);
        var positions = Substitute.For<IConsumerPositions>();
        var partitions = Substitute.For<IConsumerPartitions>();
        consumer.Positions.Returns(positions);
        consumer.Partitions.Returns(partitions);
        var service = new FailingConsumerService(
            consumer,
            ["orders"],
            serviceOptions: new KafkaConsumerServiceOptions { PollRetryBackoff = TimeSpan.FromMilliseconds(1) });
        var result = CreateResult("orders", partition: 1, offset: 42);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await WaitForResumeAsync(partitions);
        positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == "orders" && offset.Partition == 1 && offset.Offset == 42));
        partitions.Received(1).Pause(Arg.Is<TopicPartition[]>(items =>
            items != null && items.Length == 1 && items[0] == new TopicPartition("orders", 1)));
        consumer.DidNotReceive().StoreOffset(Arg.Any<ConsumeResult<string, string>>());
        await Assert.That(service.FailureContexts).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_Redeliver_KeepsConsumingWithoutFaulting()
    {
        var consumer = CreateConsumerSubstitute();
        var partitions = Substitute.For<IConsumerPartitions>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        consumer.Partitions.Returns(partitions);
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.ConsumeAsync(Arg.Any<CancellationToken>()).Returns(CreateResultsAndSignal(
            consumed,
            CreateResult("orders", partition: 0, offset: 7),
            CreateResult("orders", partition: 1, offset: 9)));
        var service = new FailingConsumerService(
            consumer,
            ["orders"],
            serviceOptions: new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromHours(1),
                MaxPollRetryBackoff = TimeSpan.FromHours(1)
            },
            failureDisposition: MessageFailureDisposition.Redeliver);

        await service.StartAsync(CancellationToken.None);
        await consumed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.ExecuteTask.IsCompletedSuccessfully).IsTrue();
        await Assert.That(service.FailureContexts.Select(context => context.Result.Offset)).IsEquivalentTo([7L, 9L]);
        partitions.Received(2).Pause(Arg.Any<TopicPartition[]>());
        partitions.DidNotReceive().Resume(Arg.Any<TopicPartition[]>());
        // The seek staged each failed offset as its partition position, so no record is in doubt.
        await consumer.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessWithRetriesAsync_RedeliveryAfterUnobservedRebalance_RewindsAgain()
    {
        // A decorated consumer cannot forward rebalance events, so a revoked partition's
        // postponement survives. When the reassigned partition redelivers the same failing
        // record, it must be rewound again rather than treated as covered and moved past.
        var consumer = CreateConsumerSubstitute();
        var positions = Substitute.For<IConsumerPositions>();
        var partitions = Substitute.For<IConsumerPartitions>();
        consumer.Positions.Returns(positions);
        consumer.Partitions.Returns(partitions);
        var service = new FailingConsumerService(
            consumer,
            ["orders"],
            serviceOptions: new KafkaConsumerServiceOptions
            {
                PollRetryBackoff = TimeSpan.FromHours(1),
                MaxPollRetryBackoff = TimeSpan.FromHours(1)
            });
        using var stopping = new CancellationTokenSource();
        try
        {
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), stopping.Token);
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), stopping.Token);
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 43), stopping.Token);
        }
        finally
        {
            await stopping.CancelAsync();
        }

        positions.Received(3).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == "orders" && offset.Partition == 1 && offset.Offset == 42));
        positions.DidNotReceive().Seek(Arg.Is<TopicPartitionOffset>(offset => offset.Offset == 43));
        partitions.Received(3).Pause(Arg.Any<TopicPartition[]>());
    }

    [Test]
    public async Task ProcessWithRetriesAsync_CoveredRedelivery_DoesNotCountAttempt()
    {
        var consumer = CreateConsumerSubstitute();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        consumer.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        var service = CreateLongBackoffRedeliveryService(consumer);
        using var stopping = new CancellationTokenSource();
        try
        {
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), stopping.Token);
            // Offset 42 is still postponed, so this failure only rewinds to it.
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 43), stopping.Token);
        }
        finally
        {
            await stopping.CancelAsync();
        }

        await Assert.That(GetRedeliveries(service)).IsEquivalentTo([(new TopicPartition("orders", 1), 42L, 1)]);
    }

    [Test]
    public async Task ProcessWithRetriesAsync_Redeliver_PrunesCountsForUnassignedPartitions()
    {
        // Without forwarded rebalance events, counts for partitions that moved away are pruned
        // the next time a redelivery is scheduled.
        var consumer = CreateConsumerSubstitute();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        consumer.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        var assignment = new HashSet<TopicPartition> { new("orders", 1), new("orders", 2) };
        consumer.Assignment.Returns(_ => assignment);
        var service = CreateLongBackoffRedeliveryService(consumer);
        using var stopping = new CancellationTokenSource();
        try
        {
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), stopping.Token);
            assignment.Remove(new TopicPartition("orders", 1));
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 2, offset: 7), stopping.Token);
        }
        finally
        {
            await stopping.CancelAsync();
        }

        await Assert.That(GetRedeliveries(service)).IsEquivalentTo([(new TopicPartition("orders", 2), 7L, 1)]);
    }

    private static FailingConsumerService CreateLongBackoffRedeliveryService(IKafkaConsumer<string, string> consumer)
        => new(consumer, ["orders"], serviceOptions: new KafkaConsumerServiceOptions
        {
            PollRetryBackoff = TimeSpan.FromHours(1),
            MaxPollRetryBackoff = TimeSpan.FromHours(1)
        });

    private static List<(TopicPartition Partition, long Offset, int Attempt)> GetRedeliveries(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string> service)
    {
        var redeliveries = (System.Collections.IDictionary)typeof(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>)
            .GetField("_redeliveries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;
        var entries = new List<(TopicPartition, long, int)>();
        foreach (System.Collections.DictionaryEntry entry in redeliveries)
        {
            var value = entry.Value!;
            entries.Add(((TopicPartition)entry.Key,
                (long)value.GetType().GetProperty("Offset")!.GetValue(value)!,
                (int)value.GetType().GetProperty("Attempt")!.GetValue(value)!));
        }
        return entries;
    }

    private static async Task WaitForResumeAsync(IConsumerPartitions partitions)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                partitions.Received(1).Resume(Arg.Any<TopicPartition[]>());
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        throw new TimeoutException("Partition was not resumed within timeout");
    }
}
