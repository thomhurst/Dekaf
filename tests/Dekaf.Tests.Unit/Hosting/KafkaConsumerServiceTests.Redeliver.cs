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
