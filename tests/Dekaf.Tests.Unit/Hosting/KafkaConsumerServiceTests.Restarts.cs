using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Extensions.Hosting;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

public sealed partial class KafkaConsumerServiceTests
{
    private static readonly KafkaConsumerServiceOptions FastRestartOptions = new()
    {
        DrainOnShutdown = false,
        PollRetryBackoff = TimeSpan.FromMilliseconds(1),
        MaxPollRetryBackoff = TimeSpan.FromMilliseconds(1)
    };

    [Test]
    public async Task ExecuteAsync_InitializationFails_RestartsUntilInitialized()
    {
        var consumer = CreateConsumerSubstitute();
        consumer.InitializeAsync(Arg.Any<CancellationToken>()).Returns(
            ValueTask.FromException(new KafkaTimeoutException("Broker unreachable")),
            ValueTask.FromException(new TimeoutException("Metadata timed out")),
            ValueTask.CompletedTask);
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(("orders", 0, 0)));

        var service = new TestConsumerService(consumer, ["orders"], FastRestartOptions);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        await consumer.Received(3).InitializeAsync(Arg.Any<CancellationToken>());
        consumer.Received(1).Subscribe(Arg.Any<string[]>());
        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_PollFails_RestartsConsumeLoopWithoutResubscribing()
    {
        var consumer = CreateConsumerSubstitute();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>()).Returns(
            ThrowAfterResults(new AuthorizationException("Group authorization failed"), ("orders", 0, 0)),
            CreateResults(("orders", 0, 1)));

        var service = new TestConsumerService(consumer, ["orders"], FastRestartOptions);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));

        consumer.Received(2).ConsumeAsync(Arg.Any<CancellationToken>());
        consumer.Received(1).Subscribe(Arg.Any<string[]>());
        await Assert.That(service.ProcessedMessages.Select(result => result.Offset)).IsEquivalentTo([0L, 1L]);
    }

    [Test]
    public async Task ExecuteAsync_ProcessingFailureWithRetryDisposition_FaultsWithoutRestart()
    {
        var consumer = CreateConsumerSubstitute();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(_ => CreateResults(("orders", 0, 0)));

        var service = new FailingConsumerService(consumer, ["orders"], serviceOptions: FastRestartOptions);

        await service.StartAsync(CancellationToken.None);

        InvalidOperationException? caught = null;
        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        consumer.Received(1).ConsumeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_SubscribeArgumentError_FaultsWithoutRestart()
    {
        var consumer = CreateConsumerSubstitute();
        var failure = new ArgumentException("Invalid topic name");
        consumer.When(c => c.Subscribe(Arg.Any<string[]>())).Do(_ => throw failure);

        var service = new TestConsumerService(consumer, ["orders"], FastRestartOptions);

        await service.StartAsync(CancellationToken.None);

        ArgumentException? caught = null;
        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsSameReferenceAs(failure);
        consumer.Received(1).Subscribe(Arg.Any<string[]>());
        consumer.DidNotReceive().ConsumeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task ExecuteAsync_NonTransientPollFailure_FaultsWithoutRestart(int kind)
    {
        Exception failure = kind switch
        {
            0 => new ObjectDisposedException("consumer"),
            1 => new InvalidOperationException("misconfigured"),
            2 => new KafkaException(Dekaf.Protocol.ErrorCode.UnsupportedVersion, "unsupported"),
            3 => new AuthenticationException("bad credentials"),
            _ => new SerializationException("poison record"),
        };
        var consumer = CreateConsumerSubstitute();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(_ => ThrowAfterResults(failure));

        var service = new TestConsumerService(consumer, ["orders"], FastRestartOptions);

        await service.StartAsync(CancellationToken.None);

        Exception? caught = null;
        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsSameReferenceAs(failure);
        consumer.Received(1).ConsumeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ConcurrentInitializationFailures_RestartOnlyWhenAllAreTransient()
    {
        await Assert.That(ServiceRestartPolicy.CanRestartAfter(new AggregateException(
            new KafkaTimeoutException("consumer"), new IOException("producer")))).IsTrue();
        await Assert.That(ServiceRestartPolicy.CanRestartAfter(new AggregateException(
            new KafkaTimeoutException("consumer"), new InvalidOperationException("producer")))).IsFalse();
    }

    [Test]
    public async Task CanRestartAfter_AuthorizationErrorCode_IsTransient()
    {
        await Assert.That(ServiceRestartPolicy.CanRestartAfter(new KafkaException(
            Dekaf.Protocol.ErrorCode.GroupAuthorizationFailed, "denied"))).IsTrue();
        await Assert.That(ServiceRestartPolicy.CanRestartAfter(new KafkaException(
            Dekaf.Protocol.ErrorCode.CoordinatorLoadInProgress, "loading"))).IsTrue();
        await Assert.That(ServiceRestartPolicy.CanRestartAfter(new KafkaException("unclassified"))).IsFalse();
    }

    [Test]
    public async Task StopAsync_DuringRestartDelay_CompletesWithoutFault()
    {
        var consumer = CreateConsumerSubstitute();
        var initializeAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.InitializeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            initializeAttempted.TrySetResult();
            return ValueTask.FromException(new KafkaTimeoutException("Broker unreachable"));
        });

        var service = new TestConsumerService(
            consumer,
            ["orders"],
            new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromMinutes(10),
                MaxPollRetryBackoff = TimeSpan.FromMinutes(10)
            });

        await service.StartAsync(CancellationToken.None);
        await initializeAttempted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(service.ExecuteTask!.IsCompletedSuccessfully).IsTrue();
        await consumer.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        // Nothing was consumed, so shutdown neither drains nor commits.
        await consumer.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(1, 100)]
    [Arguments(2, 200)]
    [Arguments(3, 400)]
    [Arguments(5, 1000)]
    [Arguments(64, 1000)]
    [Arguments(int.MaxValue, 1000)]
    public async Task GetRestartDelay_DoublesUpToMaximum(int restartAttempt, int expectedMilliseconds)
    {
        var delay = ServiceRestartPolicy.GetDelay(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1),
            restartAttempt);

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(expectedMilliseconds));
    }

    [Test]
    public async Task Constructor_PollRetryBackoffBelowOneMillisecond_Throws()
    {
        var consumer = CreateConsumerSubstitute();

        await Assert.That(() => new TestConsumerService(
                consumer,
                ["orders"],
                new KafkaConsumerServiceOptions { PollRetryBackoff = TimeSpan.Zero }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_MaxPollRetryBackoffBelowPollRetryBackoff_Throws()
    {
        var consumer = CreateConsumerSubstitute();

        await Assert.That(() => new TestConsumerService(
                consumer,
                ["orders"],
                new KafkaConsumerServiceOptions
                {
                    PollRetryBackoff = TimeSpan.FromSeconds(5),
                    MaxPollRetryBackoff = TimeSpan.FromSeconds(1)
                }))
            .Throws<ArgumentOutOfRangeException>();
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> ThrowAfterResults(
        Exception exception,
        params (string Topic, int Partition, long Offset)[] items)
    {
        foreach (var (topic, partition, offset) in items)
        {
            yield return CreateResult(topic, partition, offset);
        }

        await Task.CompletedTask;
        throw exception;
    }
}
