using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

// Drives the real KafkaProducer async produce path with a serializer that implements
// IAsyncSerializerPreparer<T>, exercising the preparer gate in ProduceAsyncCore. Uses a
// process-wide ActivityListener, so it must not run alongside other listener tests.
[NotInParallel("ActivityListener")]
public class ProducerAsyncPreparerTests
{
    private const string Topic = "prepare-topic";
    private const string PublishActivityName = "send " + Topic;

    [Test]
    public async Task ActivityListener_IgnoresOtherDekafOperations()
    {
        var (started, stopped, listener) = ListenForActivities();
        using (listener)
        {
            using (var unrelated = DekafDiagnostics.Source.StartActivity(
                       "send other-topic", ActivityKind.Producer))
            {
                await Assert.That(unrelated).IsNotNull();
            }

            await Assert.That(started).IsEmpty();
            await Assert.That(stopped).IsEmpty();
        }
    }

    [Test]
    public async Task ActivityListener_CapturesConcurrentPublishOperations()
    {
        const int operationCount = 10_000;
        var (started, stopped, listener) = ListenForActivities();
        using (listener)
        {
            Parallel.For(0, operationCount, _ =>
            {
                using var activity = DekafDiagnostics.Source.StartActivity(
                    PublishActivityName, ActivityKind.Producer);
                if (activity is null)
                    throw new InvalidOperationException("Publish activity was not sampled.");
            });

            await Assert.That(started.Count).IsEqualTo(operationCount);
            await Assert.That(stopped.Count).IsEqualTo(operationCount);
        }
    }

    // --- Activity lifecycle when preparation faults (regression: a faulting preparer must still
    //     stop and error-tag the started span, matching every other completion path). ---

    [Test]
    public async Task ProduceAsync_PreparerFaultsAsynchronously_StopsAndErrorTagsActivity()
    {
        var (started, stopped, listener) = ListenForActivities();
        using (listener)
        {
            var valueSerializer = new PreparingSerializer(_ => new ValueTask(Task.FromException(
                new InvalidOperationException("schema registry unreachable"))));

            await using var producer = CreateProducer(Serializers.String, valueSerializer);
            await ReadyProducerAsync(producer);

            await Assert.That(async () => await producer.ProduceAsync(NewMessage()))
                .Throws<InvalidOperationException>();

            await Assert.That(started.Count).IsGreaterThan(0);
            // No leaked span: every started activity was also stopped.
            await Assert.That(stopped.Count).IsEqualTo(started.Count);
            await Assert.That(started.First().Status).IsEqualTo(ActivityStatusCode.Error);
        }
    }

    [Test]
    public async Task ProduceAsync_PreparerThrowsSynchronously_StopsAndErrorTagsActivity()
    {
        var (started, stopped, listener) = ListenForActivities();
        using (listener)
        {
            var valueSerializer = new PreparingSerializer(
                _ => throw new InvalidOperationException("schema resolution failed"));

            await using var producer = CreateProducer(Serializers.String, valueSerializer);
            await ReadyProducerAsync(producer);

            await Assert.That(async () => await producer.ProduceAsync(NewMessage()))
                .Throws<InvalidOperationException>();

            await Assert.That(started.Count).IsGreaterThan(0);
            await Assert.That(stopped.Count).IsEqualTo(started.Count);
            await Assert.That(started.First().Status).IsEqualTo(ActivityStatusCode.Error);
        }
    }

    [Test]
    public async Task ProduceAsync_NullValue_TagsTombstoneOnPublishSpan()
    {
        var (started, stopped, listener) = ListenForActivities();
        using (listener)
        {
            // Key preparer faults so the produce short-circuits after the span starts —
            // the tombstone tag is set in StartPublishActivity, before preparation.
            var keySerializer = new PreparingSerializer(
                _ => throw new InvalidOperationException("stop before append"));

            await using var producer = CreateProducer(keySerializer, Serializers.String);
            await ReadyProducerAsync(producer);

            var message = new ProducerMessage<string, string> { Topic = Topic, Key = "k", Value = null! };
            await Assert.That(async () => await producer.ProduceAsync(message))
                .Throws<InvalidOperationException>();

            await Assert.That(started.Count).IsGreaterThan(0);
            await Assert.That(stopped.Count).IsEqualTo(started.Count);
            await Assert.That((bool?)started.First().GetTagItem("messaging.kafka.message.tombstone"))
                .IsTrue();
        }
    }

    [Test]
    public async Task ProduceAsync_NonNullValue_DoesNotTagTombstone()
    {
        var (started, _, listener) = ListenForActivities();
        using (listener)
        {
            var keySerializer = new PreparingSerializer(
                _ => throw new InvalidOperationException("stop before append"));

            await using var producer = CreateProducer(keySerializer, Serializers.String);
            await ReadyProducerAsync(producer);

            await Assert.That(async () => await producer.ProduceAsync(NewMessage()))
                .Throws<InvalidOperationException>();

            await Assert.That(started.Count).IsGreaterThan(0);
            await Assert.That(started.First().GetTagItem("messaging.kafka.message.tombstone")).IsNull();
        }
    }

    // --- Both preparers observed even when one faults first (regression: the value preparer must
    //     not be left unawaited/unobserved when the key preparer faults). ---

    [Test]
    public async Task ProduceAsync_KeyPreparerFaultsFirst_StillAwaitsValuePreparer()
    {
        var keyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var valueGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var keySerializer = new PreparingSerializer(_ => new ValueTask(keyGate.Task));
        var valueSerializer = new PreparingSerializer(_ => new ValueTask(valueGate.Task));

        await using var producer = CreateProducer(keySerializer, valueSerializer);
        await ReadyProducerAsync(producer);

        var produce = producer.ProduceAsync(NewMessage()).AsTask();

        // Key preparation fails first while value preparation is still in flight.
        keyGate.SetException(new InvalidOperationException("key schema failed"));

        // A sequential "await key; await value" would surface the key fault and complete the produce
        // right here, leaving the value preparer unobserved. Give that path ample time to appear;
        // awaiting both keeps the produce pending until the value preparer also completes.
        for (var i = 0; i < 100 && !produce.IsCompleted; i++)
            await Task.Delay(10);

        await Assert.That(produce.IsCompleted).IsFalse();

        valueGate.SetException(new InvalidOperationException("value schema failed"));

        await Assert.That(async () => await produce).Throws<Exception>();
    }

    // --- Cancellation during preparation is "pre-append": it throws and nothing is buffered. ---

    [Test]
    public async Task ProduceAsync_CancelledDuringPreparation_ThrowsAndDoesNotAppend()
    {
        using var cts = new CancellationTokenSource();
        var valueSerializer = new PreparingSerializer(ct => new ValueTask(Task.Delay(Timeout.Infinite, ct)));

        await using var producer = CreateProducer(Serializers.String, valueSerializer);
        await ReadyProducerAsync(producer);

        var produce = producer.ProduceAsync(NewMessage(), cts.Token).AsTask();
        await Task.Yield();
        await Assert.That(produce.IsCompleted).IsFalse();

        cts.Cancel();

        await Assert.That(async () => await produce).Throws<OperationCanceledException>();
        // Pre-append semantics: the message never reached the accumulator.
        await Assert.That(producer.RecordAccumulator.BufferedBytes).IsEqualTo(0L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProduceAsync_UsesExactPreparationAdmission(bool componentwise)
    {
        var valueSerializer = new AdmittedPreparingSerializer();
        await using var producer = CreateProducer(Serializers.String, valueSerializer);
        await ReadyProducerAsync(producer);
        SeedProducerMetadata(producer);

        await Assert.That(async () =>
                await (componentwise
                    ? producer.ProduceAsync(Topic, "k", "v")
                    : producer.ProduceAsync(NewMessage())))
            .Throws<AdmittedSerializationException>();

        await Assert.That(valueSerializer.PrepareForSerializationCount).IsEqualTo(1);
        await Assert.That(valueSerializer.SerializePreparedCount).IsEqualTo(1);
        await Assert.That(valueSerializer.SerializeCount).IsEqualTo(0);
        await Assert.That(valueSerializer.ObservedSchemaId).IsEqualTo(17);
    }

    private static ProducerMessage<string, string> NewMessage() =>
        new() { Topic = Topic, Key = "k", Value = "v" };

    private static (ConcurrentQueue<Activity> Started, ConcurrentQueue<Activity> Stopped, ActivityListener Listener)
        ListenForActivities()
    {
        var started = new ConcurrentQueue<Activity>();
        var stopped = new ConcurrentQueue<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => CaptureActivity(started, activity),
            ActivityStopped = activity => CaptureActivity(stopped, activity)
        };
        ActivitySource.AddActivityListener(listener);
        return (started, stopped, listener);
    }

    private static void CaptureActivity(ConcurrentQueue<Activity> activities, Activity activity)
    {
        if (activity.OperationName == PublishActivityName)
            activities.Enqueue(activity);
    }

    private static KafkaProducer<string, string> CreateProducer(
        ISerializer<string> keySerializer,
        ISerializer<string> valueSerializer)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "prepare-test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 4096,
            LingerMs = 10,
            RequestTimeoutMs = 500,
            DeliveryTimeoutMs = 1000,
            CloseTimeoutMs = 1000
        };

        return new KafkaProducer<string, string>(options, keySerializer, valueSerializer);
    }

    // Stops the background loops (so nothing tries to reach a broker) and marks the producer
    // initialized, so ProduceAsync reaches the preparer gate instead of the not-initialized guard.
    private static async Task ReadyProducerAsync(KafkaProducer<string, string> producer)
    {
        await producer.StopSenderLoopsForTestingAsync();

        SetField(producer, "_initialized", true);
    }

    private static void SeedProducerMetadata(KafkaProducer<string, string> producer)
    {
        var metadataManager = GetField<MetadataManager>(producer, "_metadataManager");
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

    private static T GetField<T>(object target, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return (T)target.GetType().GetField(name, flags)!.GetValue(target)!;
    }

    private static void SetField<T>(object target, string name, T value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        target.GetType().GetField(name, flags)!.SetValue(target, value);
    }

    // A serializer with a configurable async prerequisite, so a test can make preparation complete,
    // fault, or block on cancellation. Serialization itself defers to the real string serializer.
    private sealed class PreparingSerializer(Func<CancellationToken, ValueTask> prepare)
        : ISerializer<string>, IAsyncSerializerPreparer<string>
    {
        public ValueTask PrepareAsync(string value, SerializationContext context, CancellationToken cancellationToken = default)
            => prepare(cancellationToken);

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
            => Serializers.String.Serialize(value, ref destination, context);
    }

    private sealed class AdmittedPreparingSerializer :
        ISerializer<string>,
        IAsyncSerializerPreparationAdmission<string>
    {
        private static readonly object PreparedSchema = new();

        public int PrepareForSerializationCount { get; private set; }
        public int SerializePreparedCount { get; private set; }
        public int SerializeCount { get; private set; }
        public int ObservedSchemaId { get; private set; }

        public ValueTask PrepareAsync(
            string value,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Producer must request an operation admission.");

        public ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
            string value,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareForSerializationCount++;
            return new ValueTask<SerializerPreparationAdmission>(
                new SerializerPreparationAdmission("subject-v1", 17, PreparedSchema));
        }

        public void Serialize<TWriter>(
            string value,
            ref TWriter destination,
            SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            SerializeCount++;
            throw new InvalidOperationException("Invalidated cache path was used.");
        }

        public void SerializePrepared<TWriter>(
            string value,
            ref TWriter destination,
            SerializationContext context,
            in SerializerPreparationAdmission admission)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            SerializePreparedCount++;
            ObservedSchemaId = admission.SchemaId;
            throw new AdmittedSerializationException();
        }
    }

    private sealed class AdmittedSerializationException : Exception;
}
