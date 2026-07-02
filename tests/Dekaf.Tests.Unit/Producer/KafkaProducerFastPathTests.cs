using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class KafkaProducerFastPathTests
{
    private const string Topic = "test-topic";

    [Test]
    public async Task TryProduceSyncCore_CustomPartitionerReentry_PreservesOuterKeyAndValue()
    {
        var partitioner = new ReentrantPartitioner();
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000,
            CustomPartitioner = partitioner
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var topicInfo = CreateTopicInfo();
        var innerCompletion = pool.Rent();
        var outerCompletion = pool.Rent();
        var innerTask = innerCompletion.Task;
        var outerTask = outerCompletion.Task;

        partitioner.OnFirstPartition = () =>
        {
            var innerResult = InvokeTryProduceSyncCore(
                producer,
                ProducerMessage<string, string>.Create(Topic, "inner", "inner-value"),
                topicInfo,
                innerCompletion);

            if (innerResult.ToString() != "Success")
                throw new InvalidOperationException($"Unexpected inner result: {innerResult}");
        };

        var outerResult = InvokeTryProduceSyncCore(
            producer,
            ProducerMessage<string, string>.Create(Topic, "outer", "outer-value"),
            topicInfo,
            outerCompletion);

        await Assert.That(outerResult.ToString()).IsEqualTo("Success");

        var readyBatch = CompleteCurrentBatch(producer.RecordAccumulator, new TopicPartition(Topic, 0));
        await Assert.That(readyBatch.RecordBatch.Records.Count).IsEqualTo(2);
        await Assert.That(GetKeyString(readyBatch.RecordBatch.Records[0])).IsEqualTo("inner");
        await Assert.That(GetValueString(readyBatch.RecordBatch.Records[0])).IsEqualTo("inner-value");
        await Assert.That(GetKeyString(readyBatch.RecordBatch.Records[1])).IsEqualTo("outer");
        await Assert.That(GetValueString(readyBatch.RecordBatch.Records[1])).IsEqualTo("outer-value");

        readyBatch.CompleteSend(baseOffset: 0, DateTimeOffset.UtcNow);
        _ = await innerTask;
        _ = await outerTask;
    }

    private static TopicInfo CreateTopicInfo() => new()
    {
        Name = Topic,
        Partitions =
        [
            new PartitionInfo
            {
                PartitionIndex = 0,
                LeaderId = 0,
                ReplicaNodes = [0],
                IsrNodes = [0]
            }
        ]
    };

    private static object InvokeTryProduceSyncCore(
        KafkaProducer<string, string> producer,
        ProducerMessage<string, string> message,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "TryProduceSyncCore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        try
        {
            return method!.Invoke(producer, [message, topicInfo, completion])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static ReadyBatch CompleteCurrentBatch(RecordAccumulator accumulator, TopicPartition topicPartition)
    {
        var deques = GetInstanceField<object>(accumulator, "_partitionDeques");

        var tryGetValueMethod = deques.GetType().GetMethod("TryGetValue");
        var parameters = new object[] { topicPartition, null! };
        var found = (bool)tryGetValueMethod!.Invoke(deques, parameters)!;
        if (!found)
            throw new InvalidOperationException("Partition deque was not found.");

        var partitionDeque = parameters[1];
        var partitionBatch = GetInstanceField<object?>(partitionDeque!, "CurrentBatch");
        if (partitionBatch is not null)
        {
            var completeMethod = partitionBatch.GetType().GetMethod("Complete");
            return (ReadyBatch)completeMethod!.Invoke(partitionBatch, null)!;
        }

        var peekFirstMethod = partitionDeque.GetType().GetMethod("PeekFirst");
        if (peekFirstMethod!.Invoke(partitionDeque, null) is ReadyBatch readyBatch)
            return readyBatch;

        throw new InvalidOperationException("Partition deque did not contain a current or sealed batch.");
    }

    private static async Task StopProducerBackgroundLoopsAsync(KafkaProducer<string, string> producer)
    {
        var cts = GetInstanceField<CancellationTokenSource>(producer, "_senderCts");
        var senderTask = GetInstanceField<Task>(producer, "_senderTask");
        var lingerTask = GetInstanceField<Task>(producer, "_lingerTask");

        await cts.CancelAsync();
        await Task.WhenAll(senderTask, lingerTask).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static T GetInstanceField<T>(object target, string name)
    {
        const BindingFlags instanceFieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, instanceFieldFlags);
        return (T)field!.GetValue(target)!;
    }

    private static string GetKeyString(Dekaf.Protocol.Records.Record record)
        => Encoding.UTF8.GetString(record.Key.Span);

    private static string GetValueString(Dekaf.Protocol.Records.Record record)
        => Encoding.UTF8.GetString(record.Value.Span);

    private sealed class ReentrantPartitioner : IPartitioner
    {
        private bool _hasReentered;

        public Action? OnFirstPartition { get; set; }

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            if (!_hasReentered)
            {
                _hasReentered = true;
                OnFirstPartition?.Invoke();
            }

            return 0;
        }
    }
}
