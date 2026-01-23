using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

/// <summary>
/// Represents memory rented from ArrayPool that must be returned when no longer needed.
/// </summary>
public readonly struct PooledMemory
{
    private readonly byte[]? _array;
    private readonly int _length;
    private readonly bool _isNull;

    /// <summary>
    /// Creates a null PooledMemory instance.
    /// </summary>
    public static PooledMemory Null => new(null, 0, isNull: true);

    /// <summary>
    /// Creates a PooledMemory from a rented array.
    /// </summary>
    public PooledMemory(byte[]? array, int length, bool isNull = false)
    {
        _array = array;
        _length = length;
        _isNull = isNull;
    }

    /// <summary>
    /// Whether this represents a null key/value.
    /// </summary>
    public bool IsNull => _isNull;

    /// <summary>
    /// The length of the valid data in the array.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the underlying array (may be larger than Length).
    /// </summary>
    public byte[]? Array => _array;

    /// <summary>
    /// Gets the data as ReadOnlyMemory.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _array is null ? ReadOnlyMemory<byte>.Empty : _array.AsMemory(0, _length);

    /// <summary>
    /// Gets the data as ReadOnlySpan.
    /// </summary>
    public ReadOnlySpan<byte> Span => _array is null ? ReadOnlySpan<byte>.Empty : _array.AsSpan(0, _length);

    /// <summary>
    /// Returns the array to the shared pool.
    /// </summary>
    public void Return()
    {
        if (_array is not null)
        {
            ArrayPool<byte>.Shared.Return(_array);
        }
    }
}

/// <summary>
/// Accumulates records into batches for efficient sending.
/// Enforces memory limits to provide backpressure when the producer is overwhelmed.
/// Uses async signaling for efficient waiting (no polling).
/// </summary>
public sealed class RecordAccumulator : IAsyncDisposable
{
    private readonly ProducerOptions _options;
    private readonly ConcurrentDictionary<TopicPartition, PartitionBatch> _batches = new();
    private readonly Channel<ReadyBatch> _readyBatches;
    private readonly long _maxMemory;
    private long _usedMemory;
    private volatile bool _disposed;
    private volatile bool _closed;

    // Async signaling for memory backpressure - waiters await this TCS
    // When memory is released, we complete it and swap in a fresh one
    private TaskCompletionSource _memoryReleasedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordAccumulator(ProducerOptions options)
    {
        _options = options;
        _maxMemory = options.BufferMemory;

        _readyBatches = Channel.CreateBounded<ReadyBatch>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Current memory usage in bytes.
    /// </summary>
    public long UsedMemory => Interlocked.Read(ref _usedMemory);

    /// <summary>
    /// Appends a record to the appropriate batch.
    /// Key and value data are pooled - the batch will return them to the pool when complete.
    /// The completion source will be completed when the batch is sent.
    /// Blocks if the buffer memory limit is exceeded (backpressure).
    /// </summary>
    public async ValueTask<RecordAppendResult> AppendAsync(
        TopicPartition topicPartition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        TaskCompletionSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RecordAccumulator));

        // Calculate memory required for this record
        var recordMemory = EstimateRecordMemory(key.Length, value.Length, headers);

        // Wait for memory to be available (backpressure)
        await WaitForMemoryAsync(recordMemory, cancellationToken).ConfigureAwait(false);

        var batch = _batches.GetOrAdd(topicPartition, tp => new PartitionBatch(tp, _options, this));

        var result = batch.TryAppend(timestamp, key, value, headers, completion, recordMemory);

        if (!result.Success)
        {
            // Batch is full, need to flush and create new batch
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
            }

            // Create new batch and retry
            batch = new PartitionBatch(topicPartition, _options, this);
            _batches[topicPartition] = batch;
            result = batch.TryAppend(timestamp, key, value, headers, completion, recordMemory);
        }

        return result;
    }

    /// <summary>
    /// Wait until there's enough memory available using async signaling.
    /// Uses efficient wait/signal pattern - no polling or delays.
    /// Waiters are woken immediately when memory is released.
    /// </summary>
    private async ValueTask WaitForMemoryAsync(int requiredBytes, CancellationToken cancellationToken)
    {
        // Fast path: enough memory available (most common case)
        if (Interlocked.Read(ref _usedMemory) + requiredBytes <= _maxMemory)
        {
            return;
        }

        // Slow path: wait for memory to be released
        // This loop handles spurious wakeups and race conditions
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Capture the current signal before checking memory
            // This ensures we don't miss a signal between check and wait
            var signal = Volatile.Read(ref _memoryReleasedSignal);

            // Check if memory is now available
            if (Interlocked.Read(ref _usedMemory) + requiredBytes <= _maxMemory)
            {
                return;
            }

            // Wait for signal that memory was released
            // ReleaseMemory() completes this TCS when batches finish
            await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Allocates memory from the buffer pool. Called by PartitionBatch.
    /// </summary>
    internal void AllocateMemory(int bytes)
    {
        Interlocked.Add(ref _usedMemory, bytes);
    }

    /// <summary>
    /// Releases memory back to the buffer pool. Called when batches complete.
    /// Signals any waiting producers that memory is now available.
    /// </summary>
    internal void ReleaseMemory(int bytes)
    {
        Interlocked.Add(ref _usedMemory, -bytes);

        // Signal all waiters by completing the current TCS and swapping in a fresh one
        // This is lock-free and wakes all waiters efficiently
        var oldSignal = Interlocked.Exchange(
            ref _memoryReleasedSignal,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        oldSignal.TrySetResult();
    }

    private static int EstimateRecordMemory(int keyLength, int valueLength, IReadOnlyList<RecordHeader>? headers)
    {
        var size = 64; // Base overhead for record struct, list entries, etc.
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                size += header.Key.Length + (header.IsValueNull ? 0 : header.Value.Length) + 32;
            }
        }

        return size;
    }

    /// <summary>
    /// Gets batches that are ready to send.
    /// </summary>
    public IAsyncEnumerable<ReadyBatch> GetReadyBatchesAsync(CancellationToken cancellationToken)
    {
        return _readyBatches.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Checks for batches that have exceeded linger time.
    /// </summary>
    public async ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _batches)
        {
            var batch = kvp.Value;
            if (batch.ShouldFlush(now, _options.LingerMs))
            {
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
                    await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                    _batches.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Flushes all batches.
    /// </summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _batches)
        {
            var readyBatch = kvp.Value.Complete();
            if (readyBatch is not null)
            {
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
            }
        }
        _batches.Clear();
    }

    /// <summary>
    /// Flushes all pending batches and completes the ready channel for graceful shutdown.
    /// The sender loop will process remaining batches and exit when the channel is empty.
    /// </summary>
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _closed)
            return;

        _closed = true;

        // Flush all pending batches to the ready channel
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        // Complete the channel - sender will drain remaining batches and exit
        _readyBatches.Writer.Complete();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Fail all pending batches that haven't been sent yet
        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        foreach (var kvp in _batches)
        {
            var readyBatch = kvp.Value.Complete();
            readyBatch?.Fail(disposedException);
        }
        _batches.Clear();

        // Complete the channel if not already closed by CloseAsync
        if (!_closed)
        {
            _readyBatches.Writer.Complete();
        }

        // Drain any batches that were in the channel but not yet processed
        while (_readyBatches.Reader.TryRead(out var batch))
        {
            batch.Fail(disposedException);
        }
    }
}

/// <summary>
/// A batch of records for a single partition.
/// Tracks pooled arrays that are returned when the batch completes.
/// </summary>
internal sealed class PartitionBatch
{
    // Initial capacity based on typical batch sizes - avoids list resizing allocations
    private const int InitialRecordCapacity = 64;

    private readonly TopicPartition _topicPartition;
    private readonly ProducerOptions _options;
    private readonly RecordAccumulator? _accumulator;
    private readonly List<Record> _records = new(InitialRecordCapacity);
    private readonly List<TaskCompletionSource<RecordMetadata>> _completionSources = new(InitialRecordCapacity);
    private readonly List<byte[]> _pooledArrays = new(InitialRecordCapacity * 2); // 2 arrays per record (key + value)
    private readonly object _lock = new();

    private long _baseTimestamp;
    private int _offsetDelta;
    private int _estimatedSize;
    private int _trackedMemory; // Memory tracked for backpressure
    private DateTimeOffset _createdAt;

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options, RecordAccumulator? accumulator = null)
    {
        _topicPartition = topicPartition;
        _options = options;
        _accumulator = accumulator;
        _createdAt = DateTimeOffset.UtcNow;
    }

    public int RecordCount => _records.Count;
    public int EstimatedSize => _estimatedSize;

    public RecordAppendResult TryAppend(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        TaskCompletionSource<RecordMetadata> completion,
        int recordMemory = 0)
    {
        lock (_lock)
        {
            if (_records.Count == 0)
            {
                _baseTimestamp = timestamp;
            }

            // Estimate size
            var recordSize = EstimateRecordSize(key.Length, value.Length, headers);
            if (_estimatedSize + recordSize > _options.BatchSize && _records.Count > 0)
            {
                return new RecordAppendResult(false);
            }

            // Track pooled arrays for returning to pool later
            if (key.Array is not null)
            {
                _pooledArrays.Add(key.Array);
            }
            if (value.Array is not null)
            {
                _pooledArrays.Add(value.Array);
            }

            var timestampDelta = (int)(timestamp - _baseTimestamp);
            var record = new Record
            {
                TimestampDelta = timestampDelta,
                OffsetDelta = _offsetDelta,
                Key = key.Memory,
                IsKeyNull = key.IsNull,
                Value = value.Memory,
                IsValueNull = false,
                Headers = headers
            };

            _records.Add(record);
            _estimatedSize += recordSize;

            // Track memory for backpressure
            if (recordMemory > 0)
            {
                _trackedMemory += recordMemory;
                _accumulator?.AllocateMemory(recordMemory);
            }

            // Use the passed-in completion source - no allocation here
            _completionSources.Add(completion);

            _offsetDelta++;

            return new RecordAppendResult(true);
        }
    }

    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        lock (_lock)
        {
            if (_records.Count == 0)
                return false;

            return (now - _createdAt).TotalMilliseconds >= lingerMs;
        }
    }

    public ReadyBatch? Complete()
    {
        lock (_lock)
        {
            if (_records.Count == 0)
                return null;

            var batch = new RecordBatch
            {
                BaseOffset = 0,
                BaseTimestamp = _baseTimestamp,
                MaxTimestamp = _baseTimestamp + (_records.Count > 0 ? _records[^1].TimestampDelta : 0),
                LastOffsetDelta = _records.Count - 1,
                // Pass the list directly - PartitionBatch is discarded after Complete()
                // so no defensive copy needed
                Records = _records
            };

            // Pass list directly - PartitionBatch is discarded after Complete()
            // Also pass pooled arrays so they can be returned when the batch is done
            return new ReadyBatch(
                _topicPartition,
                batch,
                _completionSources,
                _pooledArrays,
                _trackedMemory,
                _accumulator);
        }
    }

    private static int EstimateRecordSize(int keyLength, int valueLength, IReadOnlyList<RecordHeader>? headers)
    {
        var size = 20; // Base overhead for varint lengths, timestamp delta, offset delta, etc.
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                size += header.Key.Length + (header.IsValueNull ? 0 : header.Value.Length) + 10;
            }
        }

        return size;
    }
}

/// <summary>
/// Result of appending a record.
/// </summary>
public readonly record struct RecordAppendResult(bool Success);

/// <summary>
/// A batch ready to be sent.
/// Returns pooled arrays to ArrayPool when complete.
/// Releases tracked memory for backpressure.
/// </summary>
public sealed class ReadyBatch
{
    public TopicPartition TopicPartition { get; }
    public RecordBatch RecordBatch { get; }
    public IReadOnlyList<TaskCompletionSource<RecordMetadata>> CompletionSources { get; }
    private readonly IReadOnlyList<byte[]> _pooledArrays;
    private readonly int _trackedMemory;
    private readonly RecordAccumulator? _accumulator;

    public ReadyBatch(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        IReadOnlyList<TaskCompletionSource<RecordMetadata>> completionSources,
        IReadOnlyList<byte[]> pooledArrays,
        int trackedMemory = 0,
        RecordAccumulator? accumulator = null)
    {
        TopicPartition = topicPartition;
        RecordBatch = recordBatch;
        CompletionSources = completionSources;
        _pooledArrays = pooledArrays;
        _trackedMemory = trackedMemory;
        _accumulator = accumulator;
    }

    public void Complete(long baseOffset, DateTimeOffset timestamp)
    {
        try
        {
            for (var i = 0; i < CompletionSources.Count; i++)
            {
                CompletionSources[i].TrySetResult(new RecordMetadata
                {
                    Topic = TopicPartition.Topic,
                    Partition = TopicPartition.Partition,
                    Offset = baseOffset + i,
                    Timestamp = timestamp
                });
            }
        }
        finally
        {
            Cleanup();
        }
    }

    public void Fail(Exception exception)
    {
        try
        {
            foreach (var tcs in CompletionSources)
            {
                tcs.TrySetException(exception);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        // Return pooled arrays
        foreach (var array in _pooledArrays)
        {
            ArrayPool<byte>.Shared.Return(array);
        }

        // Release tracked memory for backpressure
        if (_trackedMemory > 0)
        {
            _accumulator?.ReleaseMemory(_trackedMemory);
        }
    }
}
