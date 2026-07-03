using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
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
                new ProducerMessage<string, string> { Topic = Topic, Key = "inner", Value = "inner-value" },
                topicInfo,
                innerCompletion);

            if (innerResult.ToString() != "Success")
                throw new InvalidOperationException($"Unexpected inner result: {innerResult}");
        };

        var outerResult = InvokeTryProduceSyncCore(
            producer,
            new ProducerMessage<string, string> { Topic = Topic, Key = "outer", Value = "outer-value" },
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

    [Test]
    public async Task ProduceAsync_TopicKeyValue_UsesHotPathWithCachedMetadata()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);

        var produceTask = producer.ProduceAsync(Topic, "key", "value");

        var readyBatch = CompleteCurrentBatch(producer.RecordAccumulator, new TopicPartition(Topic, 0));
        await Assert.That(readyBatch.RecordBatch.Records.Count).IsEqualTo(1);
        await Assert.That(GetKeyString(readyBatch.RecordBatch.Records[0])).IsEqualTo("key");
        await Assert.That(GetValueString(readyBatch.RecordBatch.Records[0])).IsEqualTo("value");

        readyBatch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);
        var metadata = await produceTask;

        await Assert.That(metadata.Topic).IsEqualTo(Topic);
        await Assert.That(metadata.Partition).IsEqualTo(0);
        await Assert.That(metadata.Offset).IsEqualTo(7);
    }

    [Test]
    public async Task ProduceAsync_InvalidExplicitPartition_FaultsBeforeAccumulatorAppend()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);

        var exception = await CaptureProduceExceptionAsync(() => producer.ProduceAsync(
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key",
                Value = "value",
                Partition = -1
            }));

        await Assert.That(exception.Topic).IsEqualTo(Topic);
        await Assert.That(exception.Partition).IsEqualTo(-1);
        await Assert.That(GetPartitionDequeCount(producer.RecordAccumulator)).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceAsync_InvalidPartitionerResult_FaultsBeforeAccumulatorAppend()
    {
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
            CustomPartitioner = new FixedPartitioner(int.MaxValue)
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);

        var exception = await CaptureProduceExceptionAsync(() => producer.ProduceAsync(
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key",
                Value = "value"
            }));

        await Assert.That(exception.Topic).IsEqualTo(Topic);
        await Assert.That(exception.Partition).IsEqualTo(int.MaxValue);
        await Assert.That(GetPartitionDequeCount(producer.RecordAccumulator)).IsEqualTo(0);
    }

    [Test]
    public async Task TryProduceSyncForAsync_ExplicitPartitionAboveCachedMetadata_FallsBackForRefresh()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);

        var result = InvokeTryProduceSyncForAsync(
            producer,
            Topic,
            "key",
            "value",
            partition: 1,
            out var completion);

        await Assert.That(result).IsFalse();
        await Assert.That(completion).IsNull();
        await Assert.That(GetPartitionDequeCount(producer.RecordAccumulator)).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceInternalAsync_InvalidExplicitPartition_FaultsBeforeAccumulatorAppend()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = pool.Rent();

        var exception = await CaptureProduceExceptionAsync(() => InvokeProduceInternalAsync(
            producer,
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key",
                Value = "value",
                Partition = -1
            },
            completion));

        await Assert.That(exception.Topic).IsEqualTo(Topic);
        await Assert.That(exception.Partition).IsEqualTo(-1);
        await Assert.That(GetPartitionDequeCount(producer.RecordAccumulator)).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceInternalAsync_InvalidPartitionerResult_FaultsBeforeAccumulatorAppend()
    {
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
            CustomPartitioner = new FixedPartitioner(int.MaxValue)
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = pool.Rent();

        var exception = await CaptureProduceExceptionAsync(() => InvokeProduceInternalAsync(
            producer,
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key",
                Value = "value"
            },
            completion));

        await Assert.That(exception.Topic).IsEqualTo(Topic);
        await Assert.That(exception.Partition).IsEqualTo(int.MaxValue);
        await Assert.That(GetPartitionDequeCount(producer.RecordAccumulator)).IsEqualTo(0);
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

    private static void SeedProducerMetadata(KafkaProducer<string, string> producer)
    {
        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata
                {
                    NodeId = 0,
                    Host = "localhost",
                    Port = 9092
                }
            ],
            ClusterId = "test-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });
    }

    private static object InvokeTryProduceSyncCore(
        KafkaProducer<string, string> producer,
        ProducerMessage<string, string> message,
        TopicInfo topicInfo,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "TryProduceSyncCore",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(ProducerMessage<string, string>), typeof(TopicInfo), typeof(PooledValueTaskSource<RecordMetadata>)],
            modifiers: null);

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

    private static bool InvokeTryProduceSyncForAsync(
        KafkaProducer<string, string> producer,
        string topic,
        string? key,
        string value,
        int? partition,
        out PooledValueTaskSource<RecordMetadata>? completion)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "TryProduceSyncForAsync",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(Headers),
                typeof(int?),
                typeof(DateTimeOffset?),
                typeof(PooledValueTaskSource<RecordMetadata>).MakeByRefType()
            ],
            modifiers: null);

        var args = new object?[] { topic, key, value, null, partition, null, null };
        var result = (bool)method!.Invoke(producer, args)!;
        completion = (PooledValueTaskSource<RecordMetadata>?)args[6];
        return result;
    }

    private static ValueTask InvokeProduceInternalAsync(
        KafkaProducer<string, string> producer,
        ProducerMessage<string, string> message,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "ProduceInternalAsync",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(ProducerMessage<string, string>), typeof(PooledValueTaskSource<RecordMetadata>), typeof(CancellationToken)],
            modifiers: null);

        try
        {
            return (ValueTask)method!.Invoke(producer, [message, completion, CancellationToken.None])!;
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

    private static int GetPartitionDequeCount(RecordAccumulator accumulator)
    {
        var deques = GetInstanceField<object>(accumulator, "_partitionDeques");
        return (int)deques.GetType().GetProperty("Count")!.GetValue(deques)!;
    }

    private static async Task<ProduceException> CaptureProduceExceptionAsync(Func<ValueTask<RecordMetadata>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
        }
        catch (ProduceException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected ProduceException was not thrown.");
    }

    private static async Task<ProduceException> CaptureProduceExceptionAsync(Func<ValueTask> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (ProduceException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected ProduceException was not thrown.");
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

    private static void SetInstanceField<T>(object target, string name, T value)
    {
        const BindingFlags instanceFieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, instanceFieldFlags);
        field!.SetValue(target, value);
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

    private sealed class FixedPartitioner(int partition) : IPartitioner
    {
        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
            => partition;
    }
}
