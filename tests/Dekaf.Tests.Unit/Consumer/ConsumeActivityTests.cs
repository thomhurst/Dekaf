using System.Diagnostics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

// Drives the real KafkaConsumer.StartConsumeActivity to pin the semconv contract at the
// call site (span kind, tombstone, body size), not just the constants it uses.
[NotInParallel("ActivityListener")]
public sealed class ConsumeActivityTests
{
    [Test]
    public async Task StartConsumeActivity_ProcessSpan_TombstoneRecord_SetsConsumerKindTombstoneAndZeroBodySize()
    {
        using var listener = CreateListener();

        await using var consumer = CreateConsumer();
        using var pending = PendingFetchData.Create("orders", partitionIndex: 0, batches: Array.Empty<RecordBatch>());

        using var activity = InvokeStartConsumeActivity(
            consumer, pending, offset: 42, valueLength: 0, isTombstone: true, isProcessSpan: true);

        await Assert.That(activity).IsNotNull();
        // Streaming-path process span: brackets the caller's handling of the record,
        // so the semconv span-kind table maps it to CONSUMER.
        await Assert.That(activity!.Kind).IsEqualTo(ActivityKind.Consumer);
        await Assert.That(activity.OperationName).IsEqualTo("process orders");
        await Assert.That(activity.GetTagItem("messaging.operation.name")).IsEqualTo("process");
        await Assert.That(activity.GetTagItem("messaging.operation.type")).IsEqualTo("process");
        await Assert.That((bool?)activity.GetTagItem("messaging.kafka.message.tombstone")).IsTrue();
        await Assert.That(activity.GetTagItem("messaging.message.body.size")).IsEqualTo(0);
        await Assert.That(activity.GetTagItem("messaging.kafka.offset")).IsEqualTo(42L);
    }

    [Test]
    public async Task StartConsumeActivity_ProcessSpan_RegularRecord_SetsBodySizeToValueLengthWithoutTombstone()
    {
        using var listener = CreateListener();

        await using var consumer = CreateConsumer();
        using var pending = PendingFetchData.Create("orders", partitionIndex: 0, batches: Array.Empty<RecordBatch>());

        using var activity = InvokeStartConsumeActivity(
            consumer, pending, offset: 7, valueLength: 512, isTombstone: false, isProcessSpan: true);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.Kind).IsEqualTo(ActivityKind.Consumer);
        await Assert.That(activity.GetTagItem("messaging.message.body.size")).IsEqualTo(512);
        await Assert.That(activity.GetTagItem("messaging.kafka.message.tombstone")).IsNull();
    }

    [Test]
    public async Task StartConsumeActivity_ReceiveSpan_UsesPollNameAndClientKind()
    {
        using var listener = CreateListener();

        await using var consumer = CreateConsumer();
        using var pending = PendingFetchData.Create("orders", partitionIndex: 0, batches: Array.Empty<RecordBatch>());

        using var activity = InvokeStartConsumeActivity(
            consumer, pending, offset: 9, valueLength: 64, isTombstone: false, isProcessSpan: false);

        await Assert.That(activity).IsNotNull();
        // ConsumeOne-path receive span: the activity ends before the record is
        // returned to the caller, so it is a "receive" operation — CLIENT kind.
        await Assert.That(activity!.Kind).IsEqualTo(ActivityKind.Client);
        await Assert.That(activity.OperationName).IsEqualTo("poll orders");
        await Assert.That(activity.GetTagItem("messaging.operation.name")).IsEqualTo("poll");
        await Assert.That(activity.GetTagItem("messaging.operation.type")).IsEqualTo("receive");
        await Assert.That(activity.GetTagItem("messaging.message.body.size")).IsEqualTo(64);
        await Assert.That(activity.GetTagItem("messaging.kafka.offset")).IsEqualTo(9L);
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static KafkaConsumer<string, string> CreateConsumer() =>
        new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "consume-activity-test"
            },
            Serializers.String,
            Serializers.String);

    private static Activity? InvokeStartConsumeActivity(
        KafkaConsumer<string, string> consumer,
        PendingFetchData pending,
        long offset,
        int valueLength,
        bool isTombstone,
        bool isProcessSpan)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "StartConsumeActivity",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (Activity?)method!.Invoke(
            consumer,
            [pending, null, offset, valueLength, isTombstone, isProcessSpan]);
    }
}
