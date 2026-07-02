using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerAutoCommitTests
{
    [Test]
    public async Task StartAutoCommitAsync_WhenAlreadyRunning_DoesNotRestartLoop()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer",
            AutoCommitIntervalMs = 60_000
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        var consumerType = typeof(KafkaConsumer<string, string>);
        var startAutoCommit = consumerType.GetMethod(
            "StartAutoCommitAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var taskField = consumerType.GetField(
            "_autoCommitTask",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctsField = consumerType.GetField(
            "_autoCommitCts",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)startAutoCommit.Invoke(consumer, [CancellationToken.None])!;
        var firstTask = (Task?)taskField.GetValue(consumer);
        var firstCts = (CancellationTokenSource?)ctsField.GetValue(consumer);

        await (Task)startAutoCommit.Invoke(consumer, [CancellationToken.None])!;
        var secondTask = (Task?)taskField.GetValue(consumer);
        var secondCts = (CancellationTokenSource?)ctsField.GetValue(consumer);

        await Assert.That(firstTask).IsNotNull();
        await Assert.That(firstCts).IsNotNull();
        await Assert.That(ReferenceEquals(secondTask, firstTask)).IsTrue();
        await Assert.That(ReferenceEquals(secondCts, firstCts)).IsTrue();
        await Assert.That(firstTask!.IsCompleted).IsFalse();
    }
}
