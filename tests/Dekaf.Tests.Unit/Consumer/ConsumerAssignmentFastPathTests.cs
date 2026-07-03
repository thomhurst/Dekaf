using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerAssignmentFastPathTests
{
    [Test]
    public async Task EnsureAssignmentAsync_UnchangedManualAssignment_SkipsAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await consumer.EnsureAssignmentAsync(cts.Token);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_ChangedManualAssignment_RequiresAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 1, 20)]);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await consumer.EnsureAssignmentAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.String,
            Serializers.String);
    }

    private static SemaphoreSlim GetAssignmentLock(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_assignmentLock",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_assignmentLock field not found.");

        return (SemaphoreSlim)field.GetValue(consumer)!;
    }
}
