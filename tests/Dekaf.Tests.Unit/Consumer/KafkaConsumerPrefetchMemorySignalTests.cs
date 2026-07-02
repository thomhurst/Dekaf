using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class KafkaConsumerPrefetchMemorySignalTests
{
    [Test]
    public async Task TrackPrefetchedBytes_RepeatedRelease_DoesNotAccumulateMemorySignals()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            QueuedMinMessages = 2
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        using var pending = PendingFetchData.Create("test-topic", 0,
        [
            new RecordBatch
            {
                BatchLength = 128,
                Records = []
            }
        ]);

        var semaphore = GetPrefetchMemoryAvailable(consumer);
        var trackPrefetchedBytes = GetTrackPrefetchedBytes();

        for (var i = 0; i < 3; i++)
        {
            trackPrefetchedBytes.Invoke(consumer, [pending, false]);
            trackPrefetchedBytes.Invoke(consumer, [pending, true]);
        }

        await Assert.That(semaphore.CurrentCount).IsEqualTo(1);
    }

    private static SemaphoreSlim GetPrefetchMemoryAvailable(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchMemoryAvailable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchMemoryAvailable field not found - was it renamed?");

        return (SemaphoreSlim)field.GetValue(consumer)!;
    }

    private static MethodInfo GetTrackPrefetchedBytes()
    {
        return typeof(KafkaConsumer<string, string>)
            .GetMethod("TrackPrefetchedBytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TrackPrefetchedBytes method not found - was it renamed?");
    }
}
