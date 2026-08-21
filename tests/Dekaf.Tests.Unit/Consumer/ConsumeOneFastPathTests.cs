using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.Producer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumeOneFastPathTests
{
    private const string Topic = "test-topic";
    private const int Partition = 0;

    [Test]
    public async Task ConsumeOneAsync_WithPendingFetch_ReturnsSequentialRecordsWithoutAsyncIterator()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);

        await using var consumer = CreateInitializedConsumer(fetch);
        var tp = new TopicPartition(Topic, Partition);

        var firstTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        await Assert.That(firstTask.IsCompletedSuccessfully).IsTrue();
        var first = await firstTask;

        var secondTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        await Assert.That(secondTask.IsCompletedSuccessfully).IsTrue();
        var second = await secondTask;

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Offset).IsEqualTo(20L);
        await Assert.That(first.Value.Value).IsEqualTo("one");
        await Assert.That(second).IsNotNull();
        await Assert.That(second!.Value.Offset).IsEqualTo(21L);
        await Assert.That(second.Value.Value).IsEqualTo("two");
        await Assert.That(consumer.GetPosition(tp)).IsEqualTo(22L);
    }

    [Test]
    public async Task ConsumeOneAsync_ColdDeserializerPreparation_AwaitsThenUsesSynchronousDecoder()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var deserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await deserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(consume.IsCompleted).IsFalse();
        deserializer.ReleasePreparation();

        var result = await consume;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("one");
        await Assert.That(deserializer.PrepareCount).IsEqualTo(1);
        await Assert.That(deserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_PreparerNotRequired_UsesSynchronousDecoder()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var deserializer = new PreparationNotRequiredStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(consume.IsCompletedSuccessfully).IsTrue();
        var result = await consume;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("one");
        await Assert.That(deserializer.PrepareCallCount).IsEqualTo(0);
    }

    [Test]
    [NotInParallel("ActivityListener")]
    public async Task ConsumeOneAsync_ColdDeserializerPreparation_CreatesOneConsumeActivity()
    {
        const string activityTopic = "cold-preparation-activity-topic";
        var started = new ConcurrentQueue<Activity>();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateConsumeActivityListener(activityTopic, started, stopped);
        var fetch = PendingFetchData.Create(activityTopic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var deserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await deserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        deserializer.ReleasePreparation();

        var result = await consume;

        await Assert.That(result).IsNotNull();
        await Assert.That(started).Count().IsEqualTo(1);
        await Assert.That(stopped).Count().IsEqualTo(1);
        await Assert.That(stopped.Single().GetTagItem("messaging.kafka.offset")).IsEqualTo(20L);
    }

    [Test]
    [NotInParallel("ActivityListener")]
    public async Task ConsumeOneAsync_ReplacedFetchAfterColdPreparation_StartsNewConsumeActivity()
    {
        const string activityTopic = "replaced-cold-preparation-activity-topic";
        var started = new ConcurrentQueue<Activity>();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateConsumeActivityListener(activityTopic, started, stopped);
        var originalFetch = PendingFetchData.Create(activityTopic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var replacementFetch = PendingFetchData.Create(activityTopic, Partition,
        [
            CreateBatch(30, CreateRecord(0, "b", "two"))
        ]);
        var deserializer = new PreparedOnlyStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            originalFetch,
            valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);

        var initialConsumed = TryConsumeOneFromPendingFetchesWithPreparation(
            consumer,
            out var requiresAsyncPreparation,
            out var preparedKey);

        await Assert.That(initialConsumed).IsFalse();
        await Assert.That(requiresAsyncPreparation).IsTrue();

        var pendingFetches = GetPendingFetches(consumer);
        pendingFetches.Dequeue().Dispose();
        pendingFetches.Enqueue(replacementFetch);

        var result = await ConsumeOneFromPendingFetchesAsync(consumer, preparedKey);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(30L);
        await Assert.That(result.Value.Value).IsEqualTo("two");
        await Assert.That(started).Count().IsEqualTo(2);
        await Assert.That(stopped).Count().IsEqualTo(2);
        await Assert.That(stopped.Select(static activity =>
                (long)activity.GetTagItem("messaging.kafka.offset")!))
            .IsEquivalentTo([20L, 30L]);
    }

    [Test]
    [NotInParallel("ActivityListener")]
    public async Task ConsumeOneAsync_CanceledColdPreparation_StopsConsumeActivity()
    {
        const string activityTopic = "canceled-cold-preparation-activity-topic";
        var started = new ConcurrentQueue<Activity>();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateConsumeActivityListener(activityTopic, started, stopped);
        var fetch = PendingFetchData.Create(activityTopic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var deserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);
        using var cancellation = new CancellationTokenSource();

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellation.Token);
        await deserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.That(async () => await consume).Throws<OperationCanceledException>();
        await Assert.That(started).Count().IsEqualTo(1);
        await Assert.That(stopped).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_ColdDeserializerPreparation_AwaitsThenUsesSynchronousDecoder()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var deserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: deserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        var moveNext = records.MoveNextAsync();
        await deserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(moveNext.IsCompleted).IsFalse();
        deserializer.ReleasePreparation();

        await Assert.That(await moveNext).IsTrue();
        await Assert.That(records.Current.Value).IsEqualTo("one");
        await Assert.That(deserializer.PrepareCount).IsEqualTo(1);
        await Assert.That(deserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_ColdValuePreparation_DoesNotDeserializeKeyTwice()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new GatedPreparedStringDeserializer(initiallyPrepared: true);
        var valueDeserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        MarkManualAssignmentCurrent(consumer);

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await valueDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        valueDeserializer.ReleasePreparation();

        var result = await consume;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("a");
        await Assert.That(keyDeserializer.PrepareCallCount).IsEqualTo(0);
        await Assert.That(keyDeserializer.DeserializeCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_ColdValuePreparation_DoesNotDeserializeKeyTwice()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new GatedPreparedStringDeserializer(initiallyPrepared: true);
        var valueDeserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        var moveNext = records.MoveNextAsync();
        await valueDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        valueDeserializer.ReleasePreparation();

        await Assert.That(await moveNext).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("a");
        await Assert.That(keyDeserializer.PrepareCallCount).IsEqualTo(0);
        await Assert.That(keyDeserializer.DeserializeCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_ColdKeyPreparation_RetriesKeyDeserialization()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new GatedPreparedStringDeserializer();
        var valueDeserializer = new GatedPreparedStringDeserializer(initiallyPrepared: true);
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        var moveNext = records.MoveNextAsync();
        await keyDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        keyDeserializer.ReleasePreparation();

        await Assert.That(await moveNext).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("a");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(1);
        await Assert.That(keyDeserializer.DeserializeCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_ColdKeyAndValue_PreparesEachOnce()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new GatedPreparedStringDeserializer();
        var valueDeserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        MarkManualAssignmentCurrent(consumer);

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await keyDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        keyDeserializer.ReleasePreparation();
        await valueDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        valueDeserializer.ReleasePreparation();

        var result = await consume;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("a");
        await Assert.That(result.Value.Value).IsEqualTo("one");
        await Assert.That(keyDeserializer.PrepareCallCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.PrepareCallCount).IsEqualTo(1);
        await Assert.That(keyDeserializer.DeserializeCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_ColdKeyAndValue_PreparesEachOnce()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new GatedPreparedStringDeserializer();
        var valueDeserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        var moveNext = records.MoveNextAsync();
        await keyDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        keyDeserializer.ReleasePreparation();
        await valueDeserializer.PreparationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        valueDeserializer.ReleasePreparation();

        await Assert.That(await moveNext).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("a");
        await Assert.That(records.Current.Value).IsEqualTo("one");
        await Assert.That(keyDeserializer.PrepareCallCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.PrepareCallCount).IsEqualTo(1);
        await Assert.That(keyDeserializer.DeserializeCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.DeserializeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_MixedPreparedKeyUsesTryDeserialize()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        var keyDeserializer = new PreparedOnlyStringDeserializer();
        var valueDeserializer = new CallbackAsyncStringDeserializer(
            static (data, _, _) => new ValueTask<string>(Encoding.UTF8.GetString(data.Span)));
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            asyncValueDeserializer: valueDeserializer);
        MarkManualAssignmentCurrent(consumer);

        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        var second = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(first!.Value.Key).IsEqualTo("a");
        await Assert.That(first.Value.Value).IsEqualTo("one");
        await Assert.That(second!.Value.Key).IsEqualTo("b");
        await Assert.That(second.Value.Value).IsEqualTo("two");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(1);
        await Assert.That(keyDeserializer.TryDeserializeCount).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeAsync_MixedPreparedKeyUsesTryDeserialize()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);
        var keyDeserializer = new PreparedOnlyStringDeserializer();
        var valueDeserializer = new CallbackAsyncStringDeserializer(
            static (data, _, _) => new ValueTask<string>(Encoding.UTF8.GetString(data.Span)));
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            asyncValueDeserializer: valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("a");
        await Assert.That(records.Current.Value).IsEqualTo("one");
        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("b");
        await Assert.That(records.Current.Value).IsEqualTo("two");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(1);
        await Assert.That(keyDeserializer.TryDeserializeCount).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeOneAsync_PreparationInvalidatedBeforeRetry_PreparesAgain()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new InvalidatedPreparedStringDeserializer();
        var valueDeserializer = new CallbackAsyncStringDeserializer(
            static (data, _, _) => new ValueTask<string>(Encoding.UTF8.GetString(data.Span)));
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            asyncValueDeserializer: valueDeserializer);
        MarkManualAssignmentCurrent(consumer);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("a");
        await Assert.That(result.Value.Value).IsEqualTo("one");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(2);
        await Assert.That(keyDeserializer.TryDeserializeCount).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeOneAsync_PreparationRetriesAreScopedPerComponent()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new InvalidatedPreparedStringDeserializer();
        var valueDeserializer = new PreparedOnlyStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        MarkManualAssignmentCurrent(consumer);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("a");
        await Assert.That(result.Value.Value).IsEqualTo("one");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(2);
        await Assert.That(valueDeserializer.PrepareCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_PreparationRetriesAreScopedPerComponent()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        var keyDeserializer = new InvalidatedPreparedStringDeserializer();
        var valueDeserializer = new PreparedOnlyStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Key).IsEqualTo("a");
        await Assert.That(records.Current.Value).IsEqualTo("one");
        await Assert.That(keyDeserializer.PrepareCount).IsEqualTo(2);
        await Assert.That(valueDeserializer.PrepareCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_CurrentBufferedAssignment_DoesNotRentTimeoutCts()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);

        await using var consumer = CreateInitializedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);

        var resultTask = consumer.ConsumeOneAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        await Assert.That(resultTask.IsCompletedSuccessfully).IsTrue();
        var result = await resultTask;
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task ConsumeOneAsync_ManualAssignmentWithCoordinator_RecordsPollOnce()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedGroupedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);
        var coordinator = GetCoordinator(consumer);
        var initialPollVersion = GetCoordinatorPollVersion(coordinator);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(GetCoordinatorPollVersion(coordinator)).IsEqualTo(initialPollVersion + 1);
    }

    [Test]
    public async Task ConsumeOneAsync_PrefetchNotStarted_UsesSlowPath()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedConsumer(queuedMinMessages: 2, fetch);
        MarkManualAssignmentCurrent(consumer);

        var result = await consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(GetPrivateField<Task?>(consumer, "_prefetchTask") is not null).IsTrue();
    }

    [Test]
    public async Task ConsumeOneAsync_GroupedAutoCommitNotStarted_UsesSlowPath()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedGroupedConsumer(fetch, OffsetCommitMode.Auto);
        MarkManualAssignmentCurrent(consumer);

        var result = await consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(GetPrivateField<Task?>(consumer, "_autoCommitTask") is not null).IsTrue();
    }

    [Test]
    public async Task TopicFilterRefreshDue_TracksRefreshInterval()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedConsumer(fetch);
        SetPrivateField<Func<string, bool>>(consumer, "_topicFilter", static _ => true);

        await Assert.That(IsTopicFilterRefreshDue(consumer)).IsTrue();

        SetPrivateField(consumer, "_lastFilterRefreshTicks", Math.Max(1, Dekaf.MonotonicClock.GetMilliseconds()));
        await Assert.That(IsTopicFilterRefreshDue(consumer)).IsFalse();
    }

    [Test]
    public async Task ConsumeOneAsync_InvalidTimeoutsWithPendingFetch_ThrowBeforeConsuming()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);

        await using var consumer = CreateInitializedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);

        await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(-2), CancellationToken.None))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(0xffffffff), CancellationToken.None))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(GetPendingFetches(consumer).Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_FractionalNegativeTimeout_MatchesCancelAfterTruncation()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);

        var result = await consumer.ConsumeOneAsync(
            TimeSpan.FromTicks(-TimeSpan.TicksPerMillisecond - 1),
            CancellationToken.None);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task CalculateRemainingConsumeOneTimeout_IncludesBufferedDrainTime()
    {
        var drainStarted = Stopwatch.GetTimestamp() - Stopwatch.Frequency;

        var remaining = KafkaConsumer<string, string>.CalculateRemainingConsumeOneTimeout(
            TimeSpan.FromMilliseconds(100),
            drainStarted);

        await Assert.That(remaining).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task CalculateRemainingConsumeOneTimeout_PreservesInfiniteSentinel()
    {
        var timeout = TimeSpan.FromTicks(-TimeSpan.TicksPerMillisecond - 1);
        var drainStarted = Stopwatch.GetTimestamp() - Stopwatch.Frequency;

        var remaining = KafkaConsumer<string, string>.CalculateRemainingConsumeOneTimeout(
            timeout,
            drainStarted);

        await Assert.That(remaining).IsEqualTo(timeout);
    }

    [Test]
    public async Task ConsumeOneAsync_RuntimeSupportedLargeTimeout_UsesBufferedFastPath()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "one"))
        ]);
        await using var consumer = CreateInitializedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);

        var resultTask = consumer.ConsumeOneAsync(
            TimeSpan.FromMilliseconds((long)int.MaxValue + 1),
            CancellationToken.None);

        await Assert.That(resultTask.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await resultTask).IsNotNull();
    }

    [Test]
    public async Task ConsumeOneAsync_WithPendingFetch_FlushesPositionWhenFetchExhausted()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);

        await using var consumer = CreateInitializedConsumer(fetch);
        var tp = new TopicPartition(Topic, Partition);
        var positions = GetPositions(consumer);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(positions.ContainsKey(tp)).IsFalse();

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(positions.ContainsKey(tp)).IsFalse();

        await Assert.That(TryConsumeOneFromPendingFetches(consumer, out _)).IsFalse();
        await Assert.That(positions[tp]).IsEqualTo(22L);
    }

    [Test]
    public async Task ExplicitCommitStaging_InAutoMode_FlushesActivePositionWithoutPendingQueue()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);

        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);
        var positions = GetPositions(consumer);
        var pendingFetches = GetPendingFetches(consumer);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        var activeFetch = pendingFetches.Peek();
        pendingFetches.Clear();
        activeFetch.Dispose();

        consumer.StageExplicitCommitOffsetsForTesting();

        await Assert.That(positions[tp]).IsEqualTo(21L);
    }

    [Test]
    public async Task GetPosition_InAutoMode_ReadsActiveSnapshotWithoutPendingQueue()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);

        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, fetch);
        var tp = new TopicPartition(Topic, Partition);
        var pendingFetches = GetPendingFetches(consumer);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        var activeFetch = pendingFetches.Peek();
        pendingFetches.Clear();
        activeFetch.Dispose();

        await Assert.That(consumer.GetPosition(tp)).IsEqualTo(21L);
    }

    [Test]
    public async Task ConsumeOneAsync_WithPrefetchBuffer_ReturnsRecord()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(30, CreateRecord(0, "a", "one"))
        ]);

        await using var consumer = CreateInitializedConsumer(queuedMinMessages: 2);
        SetPrefetchStarted(consumer);
        AssignTestPartition(consumer);
        SetPrefetchedBytes(consumer, KafkaConsumer<string, string>.EstimatePendingFetchBytes(fetch));
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(fetch)).IsTrue();

        var resultTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        await Assert.That(resultTask.IsCompletedSuccessfully).IsTrue();
        var result = await resultTask;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(30L);
        await Assert.That(result.Value.Value).IsEqualTo("one");
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
    }

    [Test]
    public async Task Seek_WithPendingFetches_RemovesOnlySeekedPartition()
    {
        var seekedPartition = new TopicPartition(Topic, Partition);
        var retainedPartition = new TopicPartition(Topic, 1);
        var seekedFetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20, CreateRecord(0, "a", "seeked"))
        ]);
        var retainedFetch = PendingFetchData.Create(Topic, retainedPartition.Partition,
        [
            CreateBatch(30, CreateRecord(0, "b", "retained"))
        ]);

        await using var consumer = CreateInitializedConsumer();
        consumer.Assign(seekedPartition, retainedPartition);
        var fetchPositions = GetFetchPositions(consumer);
        fetchPositions[seekedPartition] = 0;
        fetchPositions[retainedPartition] = 0;

        var pendingFetches = GetPendingFetches(consumer);
        pendingFetches.Enqueue(seekedFetch);
        pendingFetches.Enqueue(retainedFetch);
        var pendingEofEvents = GetPendingEofEvents(consumer);
        pendingEofEvents.Enqueue((seekedPartition, 20L));
        pendingEofEvents.Enqueue((retainedPartition, 30L));

        consumer.Seek(new TopicPartitionOffset(Topic, Partition, 20));

        await Assert.That(pendingFetches.Count).IsEqualTo(1);
        await Assert.That(pendingFetches.Peek().TopicPartition).IsEqualTo(retainedPartition);
        await Assert.That(pendingEofEvents.ToArray())
            .IsEquivalentTo([(retainedPartition, 30L)]);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Partition).IsEqualTo(retainedPartition.Partition);
        await Assert.That(result.Value.Value).IsEqualTo("retained");
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task ConsumeOneAsync_EmitsFetchMetricsAsDeltas()
    {
        var topic = $"metrics-test-{Guid.NewGuid():N}";
        var messagesReceived = new List<long>();
        var bytesReceived = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                (instrument.Name == "messaging.client.consumed.messages" ||
                 instrument.Name == "dekaf.consumer.consumed.bytes"))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (AccumulatorTestHelpers.GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                return;

            if (instrument.Name == "messaging.client.consumed.messages")
                messagesReceived.Add(measurement);
            else if (instrument.Name == "dekaf.consumer.consumed.bytes")
                bytesReceived.Add(measurement);
        });
        listener.Start();

        var fetch = PendingFetchData.Create(topic, Partition,
        [
            CreateBatch(40,
                CreateRecord(0, "a", "one"),
                CreateRecord(1, "b", "two"))
        ]);

        await using var consumer = CreateInitializedConsumer(fetch);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None)).IsNotNull();
        await Assert.That(messagesReceived).IsEmpty();

        await Assert.That(TryConsumeOneFromPendingFetches(consumer, out _)).IsFalse();

        await Assert.That(messagesReceived).IsEquivalentTo([2L]);
        await Assert.That(bytesReceived.Sum()).IsEqualTo(8L);
    }

    [Test]
    public async Task ConsumeOneAsync_WhenTimeoutExpires_ReturnsNull()
    {
        await using var consumer = CreateInitializedConsumer();

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConsumeOneAsync_TimeoutDoesNotCancelPrefetchLoop()
    {
        await using var consumer = CreateInitializedConsumer(queuedMinMessages: 2, fetchMaxWaitMs: 1);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        await Assert.That(result).IsNull();
        var prefetchCts = GetPrefetchCts(consumer);
        await Assert.That(prefetchCts).IsNotNull();
        await Assert.That(prefetchCts!.IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task ConsumeOneAsync_WithEmptyPrefetchBuffer_DoesNotBlockCaller()
    {
        await using var consumer = CreateInitializedConsumer(queuedMinMessages: 2, fetchMaxWaitMs: 1_000);
        AssignTestPartition(consumer);
        SetPrefetchStarted(consumer);

        using var cts = new CancellationTokenSource();

        var consumeTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumeTask.IsCompleted).IsFalse();

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumeTask);
    }

    [Test]
    public async Task ConsumeOneAsync_WithPendingEof_ReturnsPartitionEof()
    {
        await using var consumer = CreateInitializedConsumer(queuedMinMessages: 2, fetchMaxWaitMs: 1);
        AssignTestPartition(consumer);
        SetPrefetchStarted(consumer);
        GetPendingEofEvents(consumer).Enqueue((new TopicPartition(Topic, Partition), 55L));

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.IsPartitionEof).IsTrue();
        await Assert.That(result.Value.Topic).IsEqualTo(Topic);
        await Assert.That(result.Value.Partition).IsEqualTo(Partition);
        await Assert.That(result.Value.Offset).IsEqualTo(55L);
    }

    [Test]
    public async Task ConsumeOneAsync_TruncatedFetch_LogsAndContinues()
    {
        var loggerFactory = new CapturingLoggerFactory();
        var faultingFetch = PendingFetchData.Create(Topic, Partition,
        [
            new RecordBatch
            {
                BaseOffset = 0,
                BaseTimestamp = 1700000000000L,
                Attributes = 0,
                Records = new ThrowingRecordList([], faultAtIndex: 0)
            }
        ]);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(100, CreateRecord(0, "k", "v"))
        ]);

        await using var consumer = CreateInitializedConsumer(loggerFactory, faultingFetch, goodFetch);

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(100L);
        await Assert.That(result.Value.Value).IsEqualTo("v");

        var parsingErrorLog = loggerFactory.GetLogger()
            .Entries
            .First(entry => entry.LogLevel == LogLevel.Error && entry.Message.Contains("Record parsing error"));
        await Assert.That(parsingErrorLog.Message).Contains(Topic);
        await Assert.That(parsingErrorLog.Message).Contains(Partition.ToString());
        await Assert.That(parsingErrorLog.Exception).IsNotNull();
    }

    [Test]
    public async Task ConsumeOneAsync_DeserializerInvalidDataException_ProvidesRecordContext()
    {
        var record = CreateRecord(
            0,
            "key",
            "value",
            [
                new Header("trace-id", Encoding.UTF8.GetBytes("abc")),
                new Header("nullable", (byte[]?)null)
            ]);
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(0, record)
        ]);
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            valueDeserializer: new InvalidDataThrowingDeserializer());

        var exception = (await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None))
            .Throws<RecordDeserializationException>())!;

        await Assert.That(exception.Origin).IsEqualTo(DeserializationExceptionOrigin.Value);
        await Assert.That(exception.TopicPartition).IsEqualTo(new TopicPartition(Topic, Partition));
        await Assert.That(exception.Offset).IsEqualTo(0L);
        await Assert.That(exception.KeyData).IsEquivalentTo(Encoding.UTF8.GetBytes("key"));
        await Assert.That(exception.ValueData).IsEquivalentTo(Encoding.UTF8.GetBytes("value"));
        await Assert.That(exception.TimestampMs).IsEqualTo(1_700_000_000_000L);
        await Assert.That(exception.Headers[0].Key).IsEqualTo("trace-id");
        await Assert.That(Encoding.UTF8.GetString(exception.Headers[0].Value.Span)).IsEqualTo("abc");
        await Assert.That(exception.Headers[1].IsValueNull).IsTrue();
        await Assert.That(exception.InnerException).IsTypeOf<InvalidDataException>();
        await Assert.That(exception.InnerException!.Message).Contains("user deserializer");
    }

    [Test]
    public async Task ConsumeOneAsync_KeyDeserializerFailure_ReportsKeyOrigin()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(7, CreateRecord(1, "bad-key", "value"))
        ]);
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer: new InvalidDataThrowingDeserializer());

        var exception = (await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None))
            .Throws<RecordDeserializationException>())!;

        await Assert.That(exception.Origin).IsEqualTo(DeserializationExceptionOrigin.Key);
        await Assert.That(exception.Component).IsEqualTo(SerializationComponent.Key);
        await Assert.That(exception.Offset).IsEqualTo(8L);
        await Assert.That(exception.KeyData).IsEquivalentTo(Encoding.UTF8.GetBytes("bad-key"));
        await Assert.That(exception.ValueData).IsEquivalentTo(Encoding.UTF8.GetBytes("value"));
    }

    [Test]
    public async Task ConsumeOneAsync_AsyncDeserializerFailure_ProvidesRecordContext()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(10, CreateRecord(2, "key", "value"))
        ]);
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            asyncValueDeserializer: new InvalidDataThrowingAsyncDeserializer());

        var exception = (await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None))
            .Throws<RecordDeserializationException>())!;

        await Assert.That(exception.Origin).IsEqualTo(DeserializationExceptionOrigin.Value);
        await Assert.That(exception.Offset).IsEqualTo(12L);
        await Assert.That(exception.ValueData).IsEquivalentTo(Encoding.UTF8.GetBytes("value"));
        await Assert.That(exception.InnerException).IsTypeOf<InvalidDataException>();
    }

    [Test]
    public async Task ConsumeOneAsync_AsyncDeserializerCancellation_IsNotWrapped()
    {
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(0, CreateRecord(0, "key", "value"))
        ]);
        await using var consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            asyncValueDeserializer: new CancellationThrowingAsyncDeserializer());

        await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ConsumeOneAsync_KeyDeserializerSeeks_DefersDisposalThroughValueDeserializer()
    {
        var keyBytes = Encoding.UTF8.GetBytes("original-key");
        var valueBytes = Encoding.UTF8.GetBytes("original-value");
        var memoryOwner = new TrackingPooledMemory(() => valueBytes.AsSpan().Clear());
        var fetch = PendingFetchData.Create(
            Topic,
            Partition,
            [CreateBatch(0, CreateRecord(0, keyBytes, valueBytes))],
            memoryOwner: memoryOwner);
        KafkaConsumer<string, string>? consumer = null;
        var keyDeserializer = new CallbackStringDeserializer(
            () => consumer!.Seek(new TopicPartitionOffset(Topic, Partition, 7)));
        var disposeCountDuringValueDeserialization = -1;
        string? observedValue = null;
        var valueDeserializer = new CallbackStringDeserializer(() =>
        {
            disposeCountDuringValueDeserialization = memoryOwner.DisposeCount;
            observedValue = Encoding.UTF8.GetString(valueBytes);
        });
        consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            keyDeserializer,
            valueDeserializer);

        await using (consumer)
        {
            var consumed = TryConsumeOneFromPendingFetches(consumer, out _);

            await Assert.That(consumed).IsFalse();
            await Assert.That(observedValue).IsEqualTo("original-value");
            await Assert.That(disposeCountDuringValueDeserialization).IsEqualTo(0);
            await Assert.That(memoryOwner.DisposeCount).IsEqualTo(1);
            await Assert.That(consumer.GetPosition(new TopicPartition(Topic, Partition))).IsEqualTo(7);
        }
    }

    [Test]
    public async Task ConsumeOneAsync_AsyncDeserializerSeeksThenThrows_PreservesRequestedOffset()
    {
        var valueBytes = Encoding.UTF8.GetBytes("original-value");
        var memoryOwner = new TrackingPooledMemory(() => valueBytes.AsSpan().Clear());
        var fetch = PendingFetchData.Create(
            Topic,
            Partition,
            [CreateBatch(0, CreateRecord(0, Encoding.UTF8.GetBytes("key"), valueBytes))],
            memoryOwner: memoryOwner);
        KafkaConsumer<string, string>? consumer = null;
        var disposeCountAfterYield = -1;
        string? observedValue = null;
        var valueDeserializer = new CallbackAsyncStringDeserializer(async (data, _, _) =>
        {
            consumer!.Seek(new TopicPartitionOffset(Topic, Partition, 7));
            await Task.Yield();
            disposeCountAfterYield = memoryOwner.DisposeCount;
            observedValue = Encoding.UTF8.GetString(data.Span);
            throw new InvalidDataException("Expected async deserializer failure.");
        });
        consumer = CreateInitializedConsumerWithDeserializers(
            fetch,
            asyncValueDeserializer: valueDeserializer);

        await using (consumer)
        {
            await Assert.That(async () =>
                    await ConsumeOneFromPendingFetchesAsync(consumer))
                .Throws<RecordDeserializationException>();

            await Assert.That(observedValue).IsEqualTo("original-value");
            await Assert.That(disposeCountAfterYield).IsEqualTo(0);
            await Assert.That(memoryOwner.DisposeCount).IsEqualTo(1);
            await Assert.That(consumer.GetPosition(new TopicPartition(Topic, Partition))).IsEqualTo(7);
        }
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(queuedMinMessages: 1, fetchMaxWaitMs: 200, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        OffsetCommitMode offsetCommitMode,
        params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(null, queuedMinMessages: 1, fetchMaxWaitMs: 200, offsetCommitMode, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        int queuedMinMessages,
        params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(queuedMinMessages, fetchMaxWaitMs: 200, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        int queuedMinMessages,
        int fetchMaxWaitMs,
        params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(null, queuedMinMessages, fetchMaxWaitMs, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        ILoggerFactory? loggerFactory,
        params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(loggerFactory, queuedMinMessages: 1, fetchMaxWaitMs: 200, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        ILoggerFactory? loggerFactory,
        int queuedMinMessages,
        int fetchMaxWaitMs,
        params PendingFetchData[] fetches)
    {
        return CreateInitializedConsumer(loggerFactory, queuedMinMessages, fetchMaxWaitMs, OffsetCommitMode.Manual, fetches);
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumer(
        ILoggerFactory? loggerFactory,
        int queuedMinMessages,
        int fetchMaxWaitMs,
        OffsetCommitMode offsetCommitMode,
        params PendingFetchData[] fetches)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = offsetCommitMode,
                QueuedMinMessages = queuedMinMessages,
                FetchMaxWaitMs = fetchMaxWaitMs
            },
            Serializers.String,
            Serializers.String,
            loggerFactory);

        SetInitialized(consumer);

        if (fetches.Length == 0)
            return consumer;

        var partitions = fetches
            .Select(static fetch => fetch.TopicPartition)
            .Distinct()
            .ToArray();
        consumer.Assign(partitions);
        var fetchPositions = GetFetchPositions(consumer);
        foreach (var partition in partitions)
            fetchPositions[partition] = 0;
        var pendingFetches = GetPendingFetches(consumer);
        foreach (var fetch in fetches)
            pendingFetches.Enqueue(fetch);

        return consumer;
    }

    private static KafkaConsumer<string, string> CreateInitializedConsumerWithDeserializers(
        PendingFetchData fetch,
        IDeserializer<string>? keyDeserializer = null,
        IDeserializer<string>? valueDeserializer = null,
        IAsyncDeserializer<string>? asyncValueDeserializer = null)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200
            },
            keyDeserializer ?? Serializers.String,
            valueDeserializer ?? Serializers.String,
            asyncValueDeserializer: asyncValueDeserializer);

        SetInitialized(consumer);
        consumer.Assign(fetch.TopicPartition);
        GetFetchPositions(consumer)[fetch.TopicPartition] = 0;
        GetPendingFetches(consumer).Enqueue(fetch);
        return consumer;
    }

    [Test]
    public async Task ConsumeOneAsync_EmptyPendingFetchWithQueuedEof_ReturnsEofSynchronously()
    {
        var fetch = PendingFetchData.Create(Topic, Partition, [CreateBatch(20)]);
        await using var consumer = CreateInitializedConsumer(fetch);
        MarkManualAssignmentCurrent(consumer);
        GetPendingEofEvents(consumer).Enqueue((new TopicPartition(Topic, Partition), 20L));

        var resultTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(resultTask.IsCompletedSuccessfully).IsTrue();
        var result = await resultTask;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.IsPartitionEof).IsTrue();
        await Assert.That(result.Value.Offset).IsEqualTo(20L);
    }

    private static KafkaConsumer<string, string> CreateInitializedGroupedConsumer(
        PendingFetchData fetch,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "consume-one-fast-path-tests",
                OffsetCommitMode = offsetCommitMode,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200
            },
            Serializers.String,
            Serializers.String);

        SetInitialized(consumer);
        AssignTestPartition(consumer);
        GetPendingFetches(consumer).Enqueue(fetch);
        return consumer;
    }

    private sealed class InvalidDataThrowingDeserializer : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidDataException("user deserializer failed");
    }

    private sealed class InvalidDataThrowingAsyncDeserializer : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<string>(new InvalidDataException("async user deserializer failed"));
    }

    private sealed class CancellationThrowingAsyncDeserializer : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<string>(new OperationCanceledException(cancellationToken));
    }

    private sealed class CallbackStringDeserializer(Action callback) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            callback();
            return Encoding.UTF8.GetString(data.Span);
        }
    }

    private sealed class GatedPreparedStringDeserializer
        : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _prepared;

        public GatedPreparedStringDeserializer(bool initiallyPrepared = false)
        {
            if (!initiallyPrepared)
                return;

            _prepared = 1;
            _release.TrySetResult();
        }

        public TaskCompletionSource PreparationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PrepareCount { get; private set; }
        public int PrepareCallCount { get; private set; }
        public int DeserializeCount { get; private set; }

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out string value)
        {
            if (Volatile.Read(ref _prepared) == 0)
            {
                value = default!;
                return false;
            }

            value = Deserialize(data, context);
            return true;
        }

        public async ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCallCount++;
            if (Volatile.Read(ref _prepared) != 0)
                return;

            PrepareCount++;
            PreparationStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _prepared, 1);
        }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (Volatile.Read(ref _prepared) == 0)
                throw new InvalidOperationException("Deserializer was used before preparation.");

            DeserializeCount++;
            return Encoding.UTF8.GetString(data.Span);
        }

        public void ReleasePreparation() => _release.TrySetResult();
    }

    private sealed class PreparationNotRequiredStringDeserializer
        : IDeserializer<string>,
          IAsyncDeserializerPreparer<string>,
          IAsyncDeserializerPreparationRequirement
    {
        public bool RequiresPreparation => false;
        public int PrepareCallCount { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            Encoding.UTF8.GetString(data.Span);

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out string value)
        {
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCallCount++;
            throw new InvalidOperationException("Preparation must not be called.");
        }
    }

    private static ActivityListener CreateConsumeActivityListener(
        string topic,
        ConcurrentQueue<Activity> started,
        ConcurrentQueue<Activity> stopped)
    {
        var operationName = DekafDiagnostics.PollSpanName(topic);
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == DekafDiagnostics.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == operationName)
                    started.Enqueue(activity);
            },
            ActivityStopped = activity =>
            {
                if (activity.OperationName == operationName)
                    stopped.Enqueue(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class PreparedOnlyStringDeserializer
        : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private int _prepared;

        public int PrepareCount { get; private set; }
        public int TryDeserializeCount { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("Prepared deserializer must use TryDeserialize.");

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out string value)
        {
            TryDeserializeCount++;
            if (Volatile.Read(ref _prepared) == 0)
            {
                value = default!;
                return false;
            }

            value = Encoding.UTF8.GetString(data.Span);
            return true;
        }

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            Volatile.Write(ref _prepared, 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InvalidatedPreparedStringDeserializer
        : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private int _generation;

        public int PrepareCount { get; private set; }
        public int TryDeserializeCount { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("Prepared deserializer must use TryDeserialize.");

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out string value)
        {
            TryDeserializeCount++;
            if (Volatile.Read(ref _generation) < 2)
            {
                value = default!;
                return false;
            }

            value = Encoding.UTF8.GetString(data.Span);
            return true;
        }

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            Interlocked.Increment(ref _generation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CallbackAsyncStringDeserializer(
        Func<ReadOnlyMemory<byte>, SerializationContext, CancellationToken, ValueTask<string>> callback)
        : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
            => callback(data, context, cancellationToken);
    }

    private sealed class TrackingPooledMemory(Action onDispose) : IPooledMemory
    {
        public int DisposeCount { get; private set; }
        public ReadOnlyMemory<byte> Memory => ReadOnlyMemory<byte>.Empty;

        public void Dispose()
        {
            DisposeCount++;
            onDispose();
        }
    }

    private static void AssignTestPartition(KafkaConsumer<string, string> consumer)
    {
        var tp = new TopicPartition(Topic, Partition);
        consumer.Assign(tp);
        GetFetchPositions(consumer)[tp] = 0;
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found.");

        initializedField.SetValue(consumer, true);
    }

    private static void MarkManualAssignmentCurrent(KafkaConsumer<string, string> consumer)
    {
        var consumerType = typeof(KafkaConsumer<string, string>);
        var assignmentVersion = consumerType.GetField(
            "_assignmentEnsureVersion",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_assignmentEnsureVersion field not found.");
        var acknowledgedVersion = consumerType.GetField(
            "_lastManualAssignmentEnsureVersion",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_lastManualAssignmentEnsureVersion field not found.");
        acknowledgedVersion.SetValue(consumer, assignmentVersion.GetValue(consumer));
    }

    private static bool IsTopicFilterRefreshDue(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "IsTopicFilterRefreshDue",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("IsTopicFilterRefreshDue method not found.");
        return (bool)method.Invoke(consumer, null)!;
    }

    private static void SetPrivateField<T>(
        KafkaConsumer<string, string> consumer,
        string fieldName,
        T value)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        field.SetValue(consumer, value);
    }

    private static T GetPrivateField<T>(KafkaConsumer<string, string> consumer, string fieldName)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        return (T)field.GetValue(consumer)!;
    }

    private static ConsumerCoordinator GetCoordinator(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinator",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinator field not found.");
        return (ConsumerCoordinator)field.GetValue(consumer)!;
    }

    private static long GetCoordinatorPollVersion(ConsumerCoordinator coordinator)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            "_pollVersion",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pollVersion field not found.");
        return (long)field.GetValue(coordinator)!;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_fetchPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found.");

        return (ConcurrentDictionary<TopicPartition, long>)field.GetValue(consumer)!;
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found.");

        return (Queue<PendingFetchData>)field.GetValue(consumer)!;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetPositions(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_positions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_positions field not found.");

        return (ConcurrentDictionary<TopicPartition, long>)field.GetValue(consumer)!;
    }

    private static bool TryConsumeOneFromPendingFetches(
        KafkaConsumer<string, string> consumer,
        out ConsumeResult<string, string> result)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("TryConsumeOneFromPendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TryConsumeOneFromPendingFetches method not found.");

        object?[] args = [null];
        var consumed = (bool)method.Invoke(consumer, args)!;
        result = args[0] is ConsumeResult<string, string> consumeResult
            ? consumeResult
            : default;
        return consumed;
    }

    private static ValueTask<ConsumeResult<string, string>?> ConsumeOneFromPendingFetchesAsync(
        KafkaConsumer<string, string> consumer,
        object? preparedKey = null)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("ConsumeOneFromPendingFetchesAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ConsumeOneFromPendingFetchesAsync method not found.");

        return (ValueTask<ConsumeResult<string, string>?>)method.Invoke(
            consumer,
            [preparedKey, CancellationToken.None])!;
    }

    private static bool TryConsumeOneFromPendingFetchesWithPreparation(
        KafkaConsumer<string, string> consumer,
        out bool requiresAsyncPreparation,
        out object? preparedKey)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod(
                "TryConsumeOneFromPendingFetchesWithPreparation",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "TryConsumeOneFromPendingFetchesWithPreparation method not found.");

        object?[] args = [null, false, null];
        var consumed = (bool)method.Invoke(consumer, args)!;
        requiresAsyncPreparation = (bool)args[1]!;
        preparedKey = args[2];
        return consumed;
    }

    private static MpscFetchBuffer GetPrefetchBuffer(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchBuffer", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchBuffer field not found.");

        return (MpscFetchBuffer)field.GetValue(consumer)!;
    }

    private static ConcurrentQueue<(TopicPartition Partition, long Offset)> GetPendingEofEvents(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingEofEvents", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingEofEvents field not found.");

        return (ConcurrentQueue<(TopicPartition Partition, long Offset)>)field.GetValue(consumer)!;
    }

    private static CancellationTokenSource? GetPrefetchCts(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchCts", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchCts field not found.");

        return (CancellationTokenSource?)field.GetValue(consumer);
    }

    private static void SetPrefetchStarted(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchTask", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchTask field not found.");

        field.SetValue(consumer, Task.CompletedTask);
    }

    private static long GetPrefetchedBytes(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchedBytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        return (long)field.GetValue(consumer)!;
    }

    private static void SetPrefetchedBytes(KafkaConsumer<string, string> consumer, long bytes)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_prefetchedBytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        field.SetValue(consumer, bytes);
    }

    private static RecordBatch CreateBatch(long baseOffset, params Record[] records)
    {
        return new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = records
        };
    }

    private static Record CreateRecord(int offsetDelta, string key, string value, Header[]? headers = null)
        => CreateRecord(
            offsetDelta,
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes(value),
            headers);

    private static Record CreateRecord(
        int offsetDelta,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> value,
        Header[]? headers = null)
    {
        return new Record
        {
            OffsetDelta = offsetDelta,
            TimestampDelta = 0,
            Key = key,
            IsKeyNull = false,
            Value = value,
            IsValueNull = false,
            Headers = headers,
            HeaderCount = headers?.Length ?? 0
        };
    }

    private sealed class ThrowingRecordList : IReadOnlyList<Record>
    {
        private readonly Record[] _goodRecords;
        private readonly int _faultAtIndex;

        public ThrowingRecordList(Record[] goodRecords, int faultAtIndex)
        {
            _goodRecords = goodRecords;
            _faultAtIndex = faultAtIndex;
        }

        public int Count => _faultAtIndex + 1;

        public Record this[int index]
        {
            get
            {
                if (index >= _faultAtIndex)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Simulated truncated record at index {index}");
                return _goodRecords[index];
            }
        }

        public IEnumerator<Record> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
                yield return this[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private CapturingLogger? _logger;

        public ILogger CreateLogger(string categoryName)
        {
            _logger ??= new CapturingLogger();
            return _logger;
        }

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        public CapturingLogger GetLogger() =>
            _logger ?? throw new InvalidOperationException("CreateLogger was never called.");
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
}
