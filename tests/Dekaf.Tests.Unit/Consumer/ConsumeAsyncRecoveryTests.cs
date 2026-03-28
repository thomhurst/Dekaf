using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the ConsumeAsync error recovery logic introduced in PR #640.
/// Verifies that a faulted PendingFetchData does not terminate the iterator,
/// positions are retained correctly, OperationCanceledException propagates,
/// and LogRecordParsingError is invoked with correct context.
///
/// These tests use reflection to inject PendingFetchData directly into the
/// consumer's _pendingFetches queue, bypassing the network layer. This is
/// necessary because KafkaConsumer has many internal dependencies that make
/// pure unit testing of ConsumeAsync impractical without this approach.
/// </summary>
public sealed class ConsumeAsyncRecoveryTests
{
    private const string Topic = "test-topic";
    private const int Partition = 0;

    /// <summary>
    /// Creates a KafkaConsumer with minimal configuration, sets internal state
    /// via reflection so ConsumeAsync can iterate pre-loaded pending fetches
    /// without hitting the network.
    /// </summary>
    private static KafkaConsumer<string, string> CreateConsumerWithPendingFetches(
        ILoggerFactory? loggerFactory,
        string topic,
        int partition,
        params PendingFetchData[] fetches)
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1 // Disable prefetching
        };

        var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            loggerFactory);

        // Use Assign to set the assignment (no coordinator needed)
        consumer.Assign(new TopicPartition(topic, partition));

        // Set _initialized = true via reflection (normally set by InitializeAsync)
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found — was it renamed?");
        initializedField.SetValue(consumer, true);

        // Pre-populate _fetchPositions so EnsureAssignmentAsync skips network calls
        var fetchPositionsField = typeof(KafkaConsumer<string, string>)
            .GetField("_fetchPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found — was it renamed?");
        var fetchPositions = (ConcurrentDictionary<TopicPartition, long>)fetchPositionsField.GetValue(consumer)!;
        fetchPositions[new TopicPartition(topic, partition)] = 0;

        // Inject PendingFetchData into _pendingFetches queue
        var pendingFetchesField = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found — was it renamed?");
        var pendingFetches = (Queue<PendingFetchData>)pendingFetchesField.GetValue(consumer)!;

        foreach (var fetch in fetches)
            pendingFetches.Enqueue(fetch);

        return consumer;
    }

    private static KafkaConsumer<string, string> CreateConsumerWithPendingFetches(
        ILoggerFactory? loggerFactory,
        params PendingFetchData[] fetches)
        => CreateConsumerWithPendingFetches(loggerFactory, Topic, Partition, fetches);

    /// <summary>
    /// Creates a valid RecordBatch with the specified records.
    /// </summary>
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

    /// <summary>
    /// Creates a valid Record with a simple string key and value.
    /// </summary>
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

    /// <summary>
    /// Creates a PendingFetchData with a batch that throws at a specific record index,
    /// simulating a corrupted fetch that fails mid-iteration.
    /// </summary>
    private static PendingFetchData CreateFaultingFetch(
        long baseOffset,
        int goodRecordCount,
        int faultAtIndex)
    {
        var goodRecords = new Record[goodRecordCount];
        for (int i = 0; i < goodRecordCount; i++)
        {
            goodRecords[i] = CreateRecord(i, $"key-{i}", $"value-{i}");
        }
        var batch = new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = new ThrowingRecordList(goodRecords, faultAtIndex)
        };

        return PendingFetchData.Create(Topic, Partition, [batch]);
    }

    /// <summary>
    /// Creates a PendingFetchData whose MoveNext succeeds but then record access throws
    /// OperationCanceledException, simulating cancellation during record parsing.
    /// </summary>
    private static PendingFetchData CreateCancellationFaultingFetch(long baseOffset)
    {
        var batch = new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = new CancellationThrowingRecordList()
        };

        return PendingFetchData.Create(Topic, Partition, [batch]);
    }

    #region Test 1: Consumer continues after a faulted fetch

    [Test]
    public async Task ConsumeAsync_FaultedFetch_ContinuesWithNextPendingFetch()
    {
        // Arrange: first fetch faults immediately (at record 0), second fetch has 2 valid records
        var faultingFetch = CreateFaultingFetch(baseOffset: 0, goodRecordCount: 0, faultAtIndex: 0);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(100, CreateRecord(0, "k1", "v1"), CreateRecord(1, "k2", "v2"))]);

        await using var consumer = CreateConsumerWithPendingFetches(null, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();

        // Act: iterate ConsumeAsync, collect results, cancel after getting records
        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);
            if (results.Count >= 2)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert: the faulted fetch was skipped, both records from the good fetch were yielded
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Key).IsEqualTo("k1");
        await Assert.That(results[1].Key).IsEqualTo("k2");
    }

    [Test]
    public async Task ConsumeAsync_FaultedFetchAfterSomeRecords_ContinuesWithNextFetch()
    {
        // Arrange: first fetch yields 2 records then faults at record 2, second fetch has 1 record
        var faultingFetch = CreateFaultingFetch(baseOffset: 0, goodRecordCount: 2, faultAtIndex: 2);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(100, CreateRecord(0, "k-after", "v-after"))]);

        await using var consumer = CreateConsumerWithPendingFetches(null, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();

        // Act
        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);
            if (results.Count >= 3)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert: 2 from faulted fetch + 1 from good fetch
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Key).IsEqualTo("key-0");
        await Assert.That(results[1].Key).IsEqualTo("key-1");
        await Assert.That(results[2].Key).IsEqualTo("k-after");
    }

    #endregion

    #region Test 1b: Consumer recovers from malformed varint (MalformedProtocolDataException)

    [Test]
    public async Task ConsumeAsync_MalformedProtocolDataException_ContinuesWithNextFetch()
    {
        // Arrange: first fetch throws MalformedProtocolDataException (malformed varint),
        // second fetch has valid records. The consumer should recover.
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = new MalformedProtocolDataThrowingRecordList()
        };
        var faultingFetch = PendingFetchData.Create(Topic, Partition, [batch]);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(100, CreateRecord(0, "k1", "v1"), CreateRecord(1, "k2", "v2"))]);

        await using var consumer = CreateConsumerWithPendingFetches(null, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();

        // Act
        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);
            if (results.Count >= 2)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert: the faulted fetch was skipped, both records from the good fetch were yielded
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Key).IsEqualTo("k1");
        await Assert.That(results[1].Key).IsEqualTo("k2");
    }

    #endregion

    #region Test 2: _positions retains last good offset

    [Test]
    public async Task ConsumeAsync_FaultedFetchAtRecordN_PositionIsNPlusOne()
    {
        // Arrange: fetch with baseOffset 10, 3 good records (offsets 10, 11, 12), faults at record 3
        var faultingFetch = CreateFaultingFetch(baseOffset: 10, goodRecordCount: 3, faultAtIndex: 3);
        // Need a second fetch to keep the loop alive so we can read position
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(100, CreateRecord(0, "k", "v"))]);

        await using var consumer = CreateConsumerWithPendingFetches(null, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);
            // After getting record from the good fetch, we know fault recovery happened
            if (results.Count >= 4)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert: position should be last good offset + 1 from the faulted fetch
        // Records at offsets 10, 11, 12 were consumed, then fault at record 3.
        // The good fetch then yields offset 100, updating position to 101.
        // But we want to verify the position was correctly set during the faulted fetch.
        // After all records: position should reflect the latest consumed (100 + 1 = 101)
        var tp = new TopicPartition(Topic, Partition);
        var position = consumer.GetPosition(tp);
        await Assert.That(position).IsEqualTo(101L);

        // Also verify the 3 records from the faulted fetch were yielded
        await Assert.That(results[0].Offset).IsEqualTo(10L);
        await Assert.That(results[1].Offset).IsEqualTo(11L);
        await Assert.That(results[2].Offset).IsEqualTo(12L);
    }

    [Test]
    public async Task ConsumeAsync_FaultedFetchAtFirstRecord_PositionNotUpdated()
    {
        // Arrange: fault at the very first record — no position update should occur from this fetch
        var faultingFetch = CreateFaultingFetch(baseOffset: 0, goodRecordCount: 0, faultAtIndex: 0);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(50, CreateRecord(0, "k", "v"))]);

        await using var consumer = CreateConsumerWithPendingFetches(null, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);
            if (results.Count >= 1)
            {
                cts.Cancel();
                break;
            }
        }

        // Assert: position should be from the good fetch (50 + 1 = 51)
        var tp = new TopicPartition(Topic, Partition);
        var position = consumer.GetPosition(tp);
        await Assert.That(position).IsEqualTo(51L);
    }

    [Test]
    public async Task GetPosition_AfterPartialBatchConsumed_ReflectsLastYieldedOffset()
    {
        // Arrange: a single fetch with 5 records at offsets 20..24.
        // We consume 3 of the 5 and then check GetPosition mid-batch.
        var fetch = PendingFetchData.Create(Topic, Partition,
        [
            CreateBatch(20,
                CreateRecord(0, "a", "1"),
                CreateRecord(1, "b", "2"),
                CreateRecord(2, "c", "3"),
                CreateRecord(3, "d", "4"),
                CreateRecord(4, "e", "5"))
        ]);

        await using var consumer = CreateConsumerWithPendingFetches(null, fetch);

        using var cts = new CancellationTokenSource();
        var results = new List<ConsumeResult<string, string>>();
        var tp = new TopicPartition(Topic, Partition);

        // Act: consume exactly 3 records, checking position after each yield
        long? positionAfterFirst = null;
        long? positionAfterSecond = null;
        long? positionAfterThird = null;

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            results.Add(result);

            switch (results.Count)
            {
                case 1:
                    positionAfterFirst = consumer.GetPosition(tp);
                    break;
                case 2:
                    positionAfterSecond = consumer.GetPosition(tp);
                    break;
                case 3:
                    positionAfterThird = consumer.GetPosition(tp);
                    cts.Cancel();
                    break;
            }

            if (cts.IsCancellationRequested)
                break;
        }

        // Assert: each position = last yielded offset + 1
        // After offset 20 -> position 21, after 21 -> 22, after 22 -> 23
        await Assert.That(positionAfterFirst).IsEqualTo(21L);
        await Assert.That(positionAfterSecond).IsEqualTo(22L);
        await Assert.That(positionAfterThird).IsEqualTo(23L);

        // Verify the records themselves
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Offset).IsEqualTo(20L);
        await Assert.That(results[1].Offset).IsEqualTo(21L);
        await Assert.That(results[2].Offset).IsEqualTo(22L);
    }

    #endregion

    #region Test 3: OperationCanceledException is not swallowed

    [Test]
    public async Task ConsumeAsync_OperationCanceledException_PropagatesOut()
    {
        // Arrange: a fetch that throws OperationCanceledException during record parsing
        var cancelFetch = CreateCancellationFaultingFetch(baseOffset: 0);

        await using var consumer = CreateConsumerWithPendingFetches(null, cancelFetch);

        // Act & Assert: OperationCanceledException must propagate, not be caught and logged
        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync())
            {
                // Should not reach here — the exception should propagate
            }
        }).Throws<OperationCanceledException>();
    }

    #endregion

    #region Test 4: LogRecordParsingError is invoked with correct topic/partition

    [Test]
    public async Task ConsumeAsync_FaultedFetch_LogsErrorWithCorrectTopicPartition()
    {
        // Arrange: use a capturing logger to verify LogRecordParsingError is called
        var loggerFactory = new CapturingLoggerFactory();
        var faultingFetch = CreateFaultingFetch(baseOffset: 0, goodRecordCount: 0, faultAtIndex: 0);
        var goodFetch = PendingFetchData.Create(Topic, Partition,
            [CreateBatch(100, CreateRecord(0, "k", "v"))]);

        await using var consumer = CreateConsumerWithPendingFetches(loggerFactory, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();

        await foreach (var _ in consumer.ConsumeAsync(cts.Token))
        {
            cts.Cancel();
            break;
        }

        // Assert: error log entry was written with correct topic and partition
        var logger = loggerFactory.GetLogger();
        var errorLogs = logger.Entries.Where(e => e.LogLevel == LogLevel.Error).ToList();
        await Assert.That(errorLogs.Count).IsGreaterThanOrEqualTo(1);

        var parsingErrorLog = errorLogs.First(e => e.Message.Contains("Record parsing error"));
        await Assert.That(parsingErrorLog.Message).Contains(Topic);
        await Assert.That(parsingErrorLog.Message).Contains(Partition.ToString());
        await Assert.That(parsingErrorLog.Exception).IsNotNull();
    }

    [Test]
    public async Task ConsumeAsync_FaultedFetchOnPartition5_LogsCorrectPartition()
    {
        // Arrange: use partition 5 to verify the partition index is logged correctly
        const int partitionIndex = 5;
        var loggerFactory = new CapturingLoggerFactory();

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1700000000000L,
            Attributes = 0,
            Records = new ThrowingRecordList([], 0)
        };
        var faultingFetch = PendingFetchData.Create("orders", partitionIndex, [batch]);
        var goodFetch = PendingFetchData.Create("orders", partitionIndex,
            [CreateBatch(100, CreateRecord(0, "k", "v"))]);

        await using var consumer = CreateConsumerWithPendingFetches(
            loggerFactory, "orders", partitionIndex, faultingFetch, goodFetch);

        using var cts = new CancellationTokenSource();

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            cts.Cancel();
            break;
        }

        // Assert: log references "orders" and "5"
        var logger = loggerFactory.GetLogger();
        var errorLog = logger.Entries.First(e => e.LogLevel == LogLevel.Error && e.Message.Contains("Record parsing error"));
        await Assert.That(errorLog.Message).Contains("orders");
        await Assert.That(errorLog.Message).Contains("5");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// An IReadOnlyList&lt;Record&gt; that throws ArgumentOutOfRangeException when
    /// the item at the specified faultIndex is accessed. Simulates a truncated
    /// record batch where LazyRecordList reduces count after parsing failure.
    /// </summary>
    private sealed class ThrowingRecordList : IReadOnlyList<Record>
    {
        private readonly Record[] _goodRecords;
        private readonly int _faultAtIndex;

        public ThrowingRecordList(Record[] goodRecords, int faultAtIndex)
        {
            _goodRecords = goodRecords;
            _faultAtIndex = faultAtIndex;
        }

        // Report count as including the faulting index so MoveNext advances to it
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
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// An IReadOnlyList&lt;Record&gt; that throws MalformedProtocolDataException on first access.
    /// Simulates a malformed varint in a record batch that causes
    /// "Malformed variable-length integer" during parsing.
    /// </summary>
    private sealed class MalformedProtocolDataThrowingRecordList : IReadOnlyList<Record>
    {
        public int Count => 1;

        public Record this[int index] =>
            throw new MalformedProtocolDataException("Malformed variable-length integer");

        public IEnumerator<Record> GetEnumerator()
        {
            yield return this[0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// An IReadOnlyList&lt;Record&gt; that throws OperationCanceledException on first access.
    /// Used to verify that OperationCanceledException propagates through the catch block.
    /// </summary>
    private sealed class CancellationThrowingRecordList : IReadOnlyList<Record>
    {
        public int Count => 1;

        public Record this[int index] =>
            throw new OperationCanceledException("Simulated cancellation during record access");

        public IEnumerator<Record> GetEnumerator()
        {
            yield return this[0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A simple capturing logger factory and logger for verifying log output.
    /// </summary>
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

        public CapturingLogger GetLogger() => _logger ?? throw new InvalidOperationException("No logger was created — CreateLogger was never called");
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    #endregion
}
