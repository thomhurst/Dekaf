using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

// Drives the real KafkaProducer async produce path with a serializer that implements
// IAsyncSerializerPreparer<T>, exercising the preparer gate in ProduceAsyncCore. Uses a
// process-wide ActivityListener, so it must not run alongside other producing tests.
[NotInParallel("ActivityListener")]
public class ProducerAsyncPreparerTests
{
    private const string Topic = "prepare-topic";

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
            await Assert.That(started[0].Status).IsEqualTo(ActivityStatusCode.Error);
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
            await Assert.That(started[0].Status).IsEqualTo(ActivityStatusCode.Error);
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

    private static ProducerMessage<string, string> NewMessage() =>
        new() { Topic = Topic, Key = "k", Value = "v" };

    private static (List<Activity> Started, List<Activity> Stopped, ActivityListener Listener) ListenForActivities()
    {
        var started = new List<Activity>();
        var stopped = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = started.Add,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        return (started, stopped, listener);
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
        var cts = GetField<CancellationTokenSource>(producer, "_senderCts");
        var senderTask = GetField<Task>(producer, "_senderTask");
        var lingerTask = GetField<Task>(producer, "_lingerTask");

        await cts.CancelAsync();
        await Task.WhenAll(senderTask, lingerTask).WaitAsync(TimeSpan.FromSeconds(5));

        SetField(producer, "_initialized", true);
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
            where TWriter : IBufferWriter<byte>, allows ref struct
            => Serializers.String.Serialize(value, ref destination, context);
    }
}
