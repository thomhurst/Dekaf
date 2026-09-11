using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class PartitionedEofRoutingTests
{
    [Test]
    public async Task FullyFilteredBatch_DoesNotReserveCompletionStorage()
    {
        var route = CreateRoute("filtered", out var lane);
        var filter = new RejectAllFilter();
        for (var index = 0; index < 100; index++)
        {
            using var warmPending = CreateFilteredInput();
            route(new ConsumeBatch<string, string>(warmPending, Serializers.String, Serializers.String,
                recordFilter: filter), default).GetAwaiter().GetResult();
        }

        using var pending = CreateFilteredInput();
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String,
            recordFilter: filter);
        filter.Calls = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        route(batch, default).GetAwaiter().GetResult();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(filter.Calls).IsEqualTo(2);
        await Assert.That(pending.IsExhausted).IsTrue();
        await Assert.That(lane.TryReadMessage(out _)).IsFalse();
        await Assert.That(lane.GetCommitOffset()).IsNull();
    }

    private static PendingFetchData CreateFilteredInput(string topic = "filtered")
    {
        var source = new RecordBatch
        {
            LastOffsetDelta = 1,
            Records =
            [
                new Record { OffsetDelta = 0, IsKeyNull = true, IsValueNull = true },
                new Record { OffsetDelta = 1, IsKeyNull = true, IsValueNull = true }
            ]
        };
        var pending = PendingFetchData.Create(topic, 0, [source]);
        pending.EagerParseAll();
        return pending;
    }

    private sealed class RejectAllFilter : IConsumerRecordFilter
    {
        public int Calls;

        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
        {
            Calls++;
            return false;
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonDeliveringBatch_DoesNotReserveCompletionStorage(bool invalidated)
    {
        var route = CreateRoute("empty", out var lane);
        using var pending = invalidated
            ? CreateFilteredInput("empty")
            : PendingFetchData.Create("empty", 0, Array.Empty<RecordBatch>());
        var epoch = new BatchIterationEpoch();
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String,
            new BatchIterationGuard(epoch, epoch.Version));
        if (invalidated)
            epoch.Invalidate();

        for (var i = 0; i < 100; i++)
            route(batch, default).GetAwaiter().GetResult();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            route(batch, default).GetAwaiter().GetResult();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(batch.IsPartitionEof).IsFalse();
        await Assert.That(lane.TryReadMessage(out _)).IsFalse();
        await Assert.That(lane.GetCommitOffset()).IsNull();
    }

    [Test]
    public async Task EofBatch_DoesNotReserveCompletionStorage()
    {
        var route = CreateRoute("eof", out var lane);
        using var pending = PendingFetchData.CreatePartitionEof("eof", 0, 42);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        for (var i = 0; i < 100; i++)
            route(batch, default).GetAwaiter().GetResult();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            route(batch, default).GetAwaiter().GetResult();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(lane.TryReadMessage(out _)).IsFalse();
        await Assert.That(lane.GetCommitOffset()).IsNull();
    }

    private static Func<ConsumeBatch<string, string>, CancellationToken, ValueTask> CreateRoute(
        string topic, out PartitionLane<string, string> lane)
    {
        var runtime = new PartitionedConsumerRuntime<string, string>(null!, static (_, _) => default,
            new PartitionedProcessingOptions { CommitPolicy = PartitionCommitPolicy.UserManaged }, null);
        var partition = new TopicPartition(topic, 0);
        lane = new PartitionLane<string, string>(partition, 1,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        var runtimeType = runtime.GetType();
        var lanes = (Dictionary<TopicPartition, PartitionLane<string, string>>)
            runtimeType.GetField("_lanes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
        lanes.Add(partition, lane);
        return runtimeType.GetMethod("RouteBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ConsumeBatch<string, string>, CancellationToken, ValueTask>>(runtime);
    }
}
