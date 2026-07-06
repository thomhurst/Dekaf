using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel]
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
    public async Task CommitAsync_InAutoMode_FlushesActivePositionWithoutPendingQueue()
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

        await consumer.CommitAsync(CancellationToken.None);

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
    public async Task ConsumeOneAsync_EmitsFetchMetricsAsDeltas()
    {
        var messagesReceived = new List<long>();
        var bytesReceived = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                (instrument.Name == "messaging.client.consumed.messages" ||
                 instrument.Name == "messaging.client.consumed.bytes"))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "messaging.client.consumed.messages")
                messagesReceived.Add(measurement);
            else if (instrument.Name == "messaging.client.consumed.bytes")
                bytesReceived.Add(measurement);
        });
        listener.Start();

        var fetch = PendingFetchData.Create(Topic, Partition,
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

        AssignTestPartition(consumer);
        var pendingFetches = GetPendingFetches(consumer);
        foreach (var fetch in fetches)
            pendingFetches.Enqueue(fetch);

        return consumer;
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

    private static Record CreateRecord(int offsetDelta, string key, string value)
    {
        return new Record
        {
            OffsetDelta = offsetDelta,
            TimestampDelta = 0,
            Key = Encoding.UTF8.GetBytes(key),
            IsKeyNull = false,
            Value = Encoding.UTF8.GetBytes(value),
            IsValueNull = false,
            Headers = null,
            HeaderCount = 0
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
