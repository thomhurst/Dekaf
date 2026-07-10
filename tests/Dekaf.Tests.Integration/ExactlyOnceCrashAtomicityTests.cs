using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class ExactlyOnceCrashAtomicityTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    private const int MessageCount = 4;

    [Test]
    [Timeout(180_000)]
    [Arguments(TransactionalEosCrashPoint.AfterInputConsumed)]
    [Arguments(TransactionalEosCrashPoint.AfterOutputProduced)]
    [Arguments(TransactionalEosCrashPoint.AfterOffsetsSent)]
    [Arguments(TransactionalEosCrashPoint.CommitAcknowledgementRace)]
    [Arguments(TransactionalEosCrashPoint.AfterCommitAcknowledged)]
    public async Task CrashAndRestart_CommitsEveryTransformationExactlyOnce(
        TransactionalEosCrashPoint crashPoint,
        CancellationToken cancellationToken)
    {
        var inputTopic = await KafkaContainer.CreateTestTopicAsync();
        var outputTopic = await KafkaContainer.CreateTestTopicAsync();
        var runId = Guid.NewGuid().ToString("N");
        var consumerGroupId = $"eos-crash-group-{runId}";
        var transactionId = $"eos-crash-txn-{runId}";

        await SeedInputAsync(inputTopic, cancellationToken);
        await using (var consumerGroupAnchor = await CreateConsumerGroupAnchorAsync(
                         inputTopic,
                         consumerGroupId,
                         cancellationToken))
        {
            await using (var crashClient = TransactionalCrashClientProcess.StartExactlyOnce(
                             KafkaContainer.BootstrapServers,
                             inputTopic,
                             outputTopic,
                             consumerGroupId,
                             transactionId,
                             MessageCount,
                             crashPoint))
            {
                await crashClient.WaitUntilReadyAsync(
                    TransactionalCrashClientProcess.SignalFor(crashPoint),
                    TimeSpan.FromSeconds(60));
                await crashClient.TerminateAsync();
            }

            await RunRecoveryAsync(
                inputTopic,
                outputTopic,
                consumerGroupId,
                transactionId);
        }

        var committedOffset = await ReadBrokerCommittedOffsetAsync(
            inputTopic,
            consumerGroupId,
            cancellationToken);
        var output = await ReadCommittedOutputAsync(outputTopic, cancellationToken);

        ThrowIfAtomicityViolated(crashPoint, committedOffset, output);
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerGroupAnchorAsync(
        string inputTopic,
        string consumerGroupId,
        CancellationToken cancellationToken)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(consumerGroupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        try
        {
            consumer.Subscribe(inputTopic);
            var firstMessage = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (firstMessage is null)
            {
                throw new TimeoutException("Could not establish the EOS consumer group before the crash run.");
            }

            await consumer.CommitAsync(
                [new TopicPartitionOffset(inputTopic, 0, 0)],
                cancellationToken);
            var committedOffset = await consumer.GetCommittedOffsetAsync(
                new TopicPartition(inputTopic, 0),
                cancellationToken);
            if (committedOffset != 0)
            {
                throw new InvalidOperationException(
                    $"EOS consumer group bootstrap committed {committedOffset?.ToString() ?? "<null>"}, expected 0.");
            }

            return consumer;
        }
        catch
        {
            await consumer.DisposeAsync();
            throw;
        }
    }

    private async Task SeedInputAsync(string inputTopic, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        for (var index = 0; index < MessageCount; index++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = inputTopic,
                Partition = 0,
                Key = InputKey(index),
                Value = InputValue(index)
            }, cancellationToken);
        }
    }

    private async Task RunRecoveryAsync(
        string inputTopic,
        string outputTopic,
        string consumerGroupId,
        string transactionId)
    {
        await using var recoveryClient = TransactionalCrashClientProcess.StartExactlyOnce(
            KafkaContainer.BootstrapServers,
            inputTopic,
            outputTopic,
            consumerGroupId,
            transactionId,
            MessageCount,
            crashPoint: null);
        await recoveryClient.WaitForSuccessfulExitAsync(TimeSpan.FromSeconds(90));
    }

    private async Task<long?> ReadBrokerCommittedOffsetAsync(
        string inputTopic,
        string consumerGroupId,
        CancellationToken cancellationToken)
    {
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        var offsets = await admin.ListConsumerGroupOffsetsAsync(consumerGroupId, cancellationToken);
        return offsets.TryGetValue(new TopicPartition(inputTopic, 0), out var offset)
            ? offset
            : null;
    }

    private async Task<List<ConsumeResult<string, string>>> ReadCommittedOutputAsync(
        string outputTopic,
        CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"eos-crash-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        consumer.Subscribe(outputTopic);
        var messages = new List<ConsumeResult<string, string>>();

        while (messages.Count <= MessageCount)
        {
            var timeout = messages.Count == 0
                ? TimeSpan.FromSeconds(30)
                : TimeSpan.FromSeconds(3);
            var message = await consumer.ConsumeOneAsync(timeout, cancellationToken);
            if (message is null)
            {
                break;
            }

            messages.Add(message.Value);
        }

        return messages;
    }

    private static void ThrowIfAtomicityViolated(
        TransactionalEosCrashPoint crashPoint,
        long? committedOffset,
        IReadOnlyCollection<ConsumeResult<string, string>> output)
    {
        var expected = Enumerable.Range(0, MessageCount)
            .ToDictionary(InputKey, InputValue, StringComparer.Ordinal);
        var actualGroups = output
            .GroupBy(message => message.Key ?? "<null>", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var missingKeys = expected.Keys.Where(key => !actualGroups.ContainsKey(key)).ToArray();
        var duplicateKeys = actualGroups
            .Where(pair => pair.Value.Length > 1)
            .Select(pair => $"{pair.Key} x{pair.Value.Length}")
            .ToArray();
        var unexpectedKeys = actualGroups.Keys.Where(key => !expected.ContainsKey(key)).ToArray();
        var invalidValues = actualGroups
            .Where(pair => expected.TryGetValue(pair.Key, out var inputValue)
                           && pair.Value.Any(message => message.Value != Transform(inputValue)))
            .Select(pair => pair.Key)
            .ToArray();

        if (committedOffset == MessageCount
            && missingKeys.Length == 0
            && duplicateKeys.Length == 0
            && unexpectedKeys.Length == 0
            && invalidValues.Length == 0
            && output.Count == MessageCount)
        {
            return;
        }

        throw new InvalidOperationException(
            $"EOS crash point '{crashPoint}' violated commit-offset atomicity. " +
            $"Committed offset: {committedOffset?.ToString() ?? "<null>"}; " +
            $"output count: {output.Count}; " +
            $"missing keys: [{string.Join(", ", missingKeys)}]; " +
            $"duplicate keys: [{string.Join(", ", duplicateKeys)}]; " +
            $"unexpected keys: [{string.Join(", ", unexpectedKeys)}]; " +
            $"invalid values: [{string.Join(", ", invalidValues)}].");
    }

    internal static string InputKey(int index) => $"input-{index}";

    internal static string InputValue(int index) => $"payload-{index}";

    internal static string Transform(string value) => $"transformed-{value}";
}
