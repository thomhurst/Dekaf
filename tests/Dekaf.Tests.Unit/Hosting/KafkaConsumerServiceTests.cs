using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

public sealed class KafkaConsumerServiceTests
{
    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_SubscribesToTopics()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var service = new TestConsumerService(consumer, ["topic-a", "topic-b"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until Subscribe is called rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                consumer.Received(1).Subscribe(Arg.Is<string[]>(t => t.Length == 2 && t[0] == "topic-a" && t[1] == "topic-b"));
                break;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        await service.StopAsync(CancellationToken.None);

        consumer.Received(1).Subscribe(Arg.Is<string[]>(t => t.Length == 2 && t[0] == "topic-a" && t[1] == "topic-b"));
    }

    [Test]
    public async Task ExecuteAsync_ProcessesMessages()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(("topic-a", 0, 0), ("topic-a", 0, 1)));

        var service = new TestConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until messages are processed rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (service.ProcessedMessages.Count < 2 && !timeout.IsCancellationRequested)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_OnProcessError_CallsOnErrorAsync()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(("topic-a", 0, 0)));

        var service = new FailingConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until the error is recorded rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (service.Errors.Count == 0 && !timeout.IsCancellationRequested)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.Errors).Count().IsGreaterThanOrEqualTo(1);
    }

    #endregion

    #region StopAsync

    [Test]
    public async Task StopAsync_CommitsOffsets()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var service = new TestConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        await consumer.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StopAsync_CommitFails_DoesNotThrow()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));
        consumer.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Commit failed"));

        var service = new TestConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // StopAsync should not throw even if commit fails
        var act = async () => await service.StopAsync(CancellationToken.None);

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<ConsumeResult<string, string>> WaitForCancellation(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected - service is stopping
        }

        yield break;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateResults(
        params (string Topic, int Partition, long Offset)[] items)
    {
        foreach (var (topic, partition, offset) in items)
        {
            yield return new ConsumeResult<string, string>(
                topic: topic,
                partition: partition,
                offset: offset,
                keyData: default,
                isKeyNull: true,
                valueData: default,
                isValueNull: true,
                headers: null,
                timestamp: default,
                timestampType: TimestampType.NotAvailable,
                leaderEpoch: null,
                keyDeserializer: null,
                valueDeserializer: null);
        }

        await Task.CompletedTask;
    }

    private sealed class TestConsumerService : TestableKafkaConsumerService
    {
        public List<ConsumeResult<string, string>> ProcessedMessages { get; } = [];

        public TestConsumerService(IKafkaConsumer<string, string> consumer, string[] topics)
            : base(consumer, topics)
        {
        }

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            ProcessedMessages.Add(result);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingConsumerService : TestableKafkaConsumerService
    {
        public List<Exception> Errors { get; } = [];

        public FailingConsumerService(IKafkaConsumer<string, string> consumer, string[] topics)
            : base(consumer, topics)
        {
        }

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Processing failed");
        }

        protected override ValueTask OnErrorAsync(Exception exception, ConsumeResult<string, string>? result, CancellationToken cancellationToken)
        {
            Errors.Add(exception);
            return ValueTask.CompletedTask;
        }
    }

    private abstract class TestableKafkaConsumerService : Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>
    {
        private readonly string[] _topics;

        protected TestableKafkaConsumerService(IKafkaConsumer<string, string> consumer, string[] topics)
            : base(consumer, NullLogger.Instance)
        {
            _topics = topics;
        }

        protected override IEnumerable<string> Topics => _topics;
    }

    #endregion
}
