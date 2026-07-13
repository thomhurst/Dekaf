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
    private const int BufferMemoryLimit = 1024;

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
    public async Task TransactionProduceAsync_CompletesContinuationInlineOnSenderThread()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-transaction-producer",
            TransactionalId = "test-transaction-id",
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
        producer._transactionState = TransactionState.Ready;
        var transaction = producer.BeginTransaction();
        var produceTask = transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = "key",
            Value = "value"
        });
        var awaiter = produceTask.GetAwaiter();
        var continuationThreadId = 0;
        awaiter.UnsafeOnCompleted(() => continuationThreadId = Environment.CurrentManagedThreadId);
        var readyBatch = CompleteCurrentBatch(producer.RecordAccumulator, new TopicPartition(Topic, 0));
        var senderThreadId = Environment.CurrentManagedThreadId;

        readyBatch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);

        await Assert.That(continuationThreadId).IsEqualTo(senderThreadId);
        await Assert.That(awaiter.GetResult().Offset).IsEqualTo(7);
        producer._transactionState = TransactionState.Ready;
    }

    [Test]
    public async Task TransactionProduceAsync_SequentialAwaitCanReenterProducerOnSenderThread()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-sequential-transaction-producer",
            TransactionalId = "test-transaction-id",
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
        producer._transactionState = TransactionState.Ready;
        var transaction = producer.BeginTransaction();
        var produceTask = ProduceSequentiallyAsync(transaction);
        var firstBatch = CompleteCurrentBatch(producer.RecordAccumulator, new TopicPartition(Topic, 0));

        firstBatch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);

        var secondBatch = CompleteCurrentBatch(producer.RecordAccumulator, new TopicPartition(Topic, 0));
        secondBatch.CompleteSend(baseOffset: 8, DateTimeOffset.UtcNow);
        var offsets = await produceTask.ConfigureAwait(false);
        await Assert.That(offsets).IsEquivalentTo([7L, 8L]);
        producer._transactionState = TransactionState.Ready;

        static async ValueTask<long[]> ProduceSequentiallyAsync(ITransaction<string, string> transaction)
        {
            var first = await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key-1",
                Value = "value-1"
            }).ConfigureAwait(false);
            var second = await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "key-2",
                Value = "value-2"
            }).ConfigureAwait(false);
            return [first.Offset, second.Offset];
        }
    }

    [Test]
    public async Task FireAsync_KeyedMessageOnStickyPartition_DoesNotAdvanceUniformStickyCounter()
    {
        const int partitionCount = 3;
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 1024,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000,
            EnableAdaptivePartitioning = false
        };

        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer, partitionCount);
        SetInstanceField(producer, "_initialized", true);

        var partitioner = GetInstanceField<IPartitioner>(producer, "_partitioner");
        var stickyPartition = partitioner.Partition(Topic, ReadOnlySpan<byte>.Empty, keyIsNull: true, partitionCount);
        var keyOnStickyPartition = FindKeyForPartition(partitioner, stickyPartition, partitionCount);

        await producer.FireAsync(Topic, key: null, value: "sticky");
        var bufferedAfterStickyAppend = producer.RecordAccumulator.BufferedBytes;
        for (var i = 0; i < 6; i++)
        {
            await producer.FireAsync(Topic, keyOnStickyPartition, new string('v', 512));
        }

        await Assert.That(producer.RecordAccumulator.BufferedBytes)
            .IsGreaterThan(bufferedAfterStickyAppend + 1024);

        var partitionAfterKeyedAppends = partitioner.Partition(
            Topic,
            ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            partitionCount);

        await Assert.That(partitionAfterKeyedAppends).IsEqualTo(stickyPartition);
    }

    [Test]
    public async Task FireAsync_BufferMemoryExactlyFull_WaitsUntilSpaceIsReleased()
    {
        await using var producer = await CreateBufferBoundaryProducerAsync(maxBlockMs: 30_000);
        var accumulator = producer.RecordAccumulator;
        const string key = "key";
        const string value = "value";
        var recordSize = PartitionBatch.EstimateRecordSize(
            Encoding.UTF8.GetByteCount(key),
            Encoding.UTF8.GetByteCount(value),
            null,
            0);

        await Assert.That(accumulator.TryReserveMemoryForTest(BufferMemoryLimit)).IsTrue();
        var syntheticReservationRemaining = BufferMemoryLimit;

        try
        {
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(BufferMemoryLimit);
            await Assert.That(accumulator.TryReserveMemoryForTest(1)).IsFalse();

            var pressureBefore = accumulator.BufferPressureEvents;
            var fireTask = producer.FireAsync(Topic, key, value).AsTask();

            await TestWait.UntilAsync(
                () => accumulator.BufferPressureEvents > pressureBefore,
                TimeSpan.FromSeconds(5));
            await Assert.That(fireTask.IsCompleted).IsFalse();

            accumulator.ReleaseMemory(recordSize);
            syntheticReservationRemaining -= recordSize;

            await fireTask.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        }
        finally
        {
            if (syntheticReservationRemaining > 0)
                accumulator.ReleaseMemory(syntheticReservationRemaining);
        }
    }

    [Test]
    public async Task TransactionFastPath_BufferFull_RestoresAsyncModeBeforePoolReturn()
    {
        await using var producer = await CreateBufferBoundaryProducerAsync(maxBlockMs: 30_000);
        var accumulator = producer.RecordAccumulator;
        var pool = GetInstanceField<ValueTaskSourcePool<RecordMetadata>>(producer, "_valueTaskSourcePool");
        await Assert.That(accumulator.TryReserveMemoryForTest(BufferMemoryLimit)).IsTrue();

        try
        {
            var pooledBefore = pool.ApproximateCount;
            var usedFastPath = InvokeTryProduceSyncForAsync(
                producer,
                new ProducerMessage<string, string> { Topic = Topic, Key = "key", Value = "value" },
                runContinuationsAsynchronously: false,
                out var completion);

            await Assert.That(usedFastPath).IsFalse();
            await Assert.That(completion).IsNull();
            await Assert.That(pool.ApproximateCount).IsEqualTo(pooledBefore + 1);

            var reused = pool.Rent();
            var awaiter = reused.Task.GetAwaiter();
            var continuationThreadId = 0;
            var continuation = new TaskCompletionSource<RecordMetadata>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            awaiter.UnsafeOnCompleted(() =>
            {
                continuationThreadId = Environment.CurrentManagedThreadId;
                try
                {
                    continuation.SetResult(awaiter.GetResult());
                }
                catch (Exception ex)
                {
                    continuation.SetException(ex);
                }
            });
            var completionThreadId = 0;
            var completionThread = new Thread(() =>
            {
                completionThreadId = Environment.CurrentManagedThreadId;
                reused.SetResult(new RecordMetadata
                {
                    Topic = Topic,
                    Partition = 0,
                    Offset = 0,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }) { IsBackground = true };

            completionThread.Start();
            completionThread.Join();
            var metadata = await continuation.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            await Assert.That(metadata.Topic).IsEqualTo(Topic);
            await Assert.That(continuationThreadId).IsNotEqualTo(completionThreadId);
        }
        finally
        {
            accumulator.ReleaseMemory(BufferMemoryLimit);
        }
    }

    [Test]
    public async Task FireAsync_BufferMemoryFull_MaxBlockExpiryThrowsKafkaTimeoutException()
    {
        const int maxBlockMs = 100;
        await using var producer = await CreateBufferBoundaryProducerAsync(maxBlockMs);
        var accumulator = producer.RecordAccumulator;

        await Assert.That(accumulator.TryReserveMemoryForTest(BufferMemoryLimit)).IsTrue();
        try
        {
            var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
                await producer.FireAsync(Topic, "key", "value")
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(30)));

            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
            await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(maxBlockMs));
        }
        finally
        {
            accumulator.ReleaseMemory(BufferMemoryLimit);
        }
    }

    [Test]
    public async Task FireAsync_WithCallback_BufferMemoryFull_DeliversMaxBlockTimeoutToCallback()
    {
        const int maxBlockMs = 100;
        await using var producer = await CreateBufferBoundaryProducerAsync(maxBlockMs);
        var accumulator = producer.RecordAccumulator;
        var callbackResult = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await Assert.That(accumulator.TryReserveMemoryForTest(BufferMemoryLimit)).IsTrue();
        try
        {
            var fireTask = producer.FireAsync(
                new ProducerMessage<string, string>
                {
                    Topic = Topic,
                    Key = "key",
                    Value = "value"
                },
                (_, exception) => callbackResult.TrySetResult(exception)).AsTask();

            await fireTask.WaitAsync(TimeSpan.FromSeconds(30));
            var exception = await callbackResult.Task.WaitAsync(TimeSpan.FromSeconds(30));

            await Assert.That(exception).IsTypeOf<KafkaTimeoutException>();
            var timeout = (KafkaTimeoutException)exception!;
            await Assert.That(timeout.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
            await Assert.That(timeout.Configured).IsEqualTo(TimeSpan.FromMilliseconds(maxBlockMs));
        }
        finally
        {
            accumulator.ReleaseMemory(BufferMemoryLimit);
        }
    }

    private static async Task<KafkaProducer<string, string>> CreateBufferBoundaryProducerAsync(int maxBlockMs)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "buffer-boundary-test",
            BufferMemory = BufferMemoryLimit,
            BatchSize = 4096,
            LingerMs = 10,
            MaxBlockMs = maxBlockMs,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String);
        await StopProducerBackgroundLoopsAsync(producer);
        SeedProducerMetadata(producer);
        SetInstanceField(producer, "_initialized", true);
        return producer;
    }

    private static TopicInfo CreateTopicInfo(int partitionCount = 1) => new()
    {
        Name = Topic,
        Partitions = Enumerable.Range(0, partitionCount)
            .Select(partition => new PartitionInfo
            {
                PartitionIndex = partition,
                LeaderId = 0,
                ReplicaNodes = [0],
                IsrNodes = [0]
            })
            .ToArray()
    };

    private static void SeedProducerMetadata(KafkaProducer<string, string> producer, int partitionCount = 1)
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
                    Partitions = Enumerable.Range(0, partitionCount)
                        .Select(partition => new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = partition,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        })
                        .ToArray()
                }
            ]
        });
    }

    private static string FindKeyForPartition(IPartitioner partitioner, int targetPartition, int partitionCount)
    {
        for (var i = 0; i < 10_000; i++)
        {
            var key = $"key-{i}";
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (partitioner.Partition(Topic, keyBytes, keyIsNull: false, partitionCount) == targetPartition)
                return key;
        }

        throw new InvalidOperationException($"Could not find a key for partition {targetPartition}.");
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
        ProducerMessage<string, string> message,
        bool runContinuationsAsynchronously,
        out PooledValueTaskSource<RecordMetadata>? completion)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "TryProduceSyncForAsync",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [
                typeof(ProducerMessage<string, string>),
                typeof(bool),
                typeof(PooledValueTaskSource<RecordMetadata>).MakeByRefType()
            ],
            modifiers: null);
        object?[] arguments = [message, runContinuationsAsynchronously, null];

        try
        {
            var result = (bool)method!.Invoke(producer, arguments)!;
            completion = (PooledValueTaskSource<RecordMetadata>?)arguments[2];
            return result;
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
}
