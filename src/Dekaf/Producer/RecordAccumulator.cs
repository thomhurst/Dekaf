using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

/// <summary>
/// Accumulates records into batches for efficient sending.
/// </summary>
public sealed class RecordAccumulator : IAsyncDisposable
{
    private readonly ProducerOptions _options;
    private readonly ConcurrentDictionary<TopicPartition, PartitionBatch> _batches = new();
    private readonly Channel<ReadyBatch> _readyBatches;
    private volatile bool _disposed;
    private volatile bool _closed;

    public RecordAccumulator(ProducerOptions options)
    {
        _options = options;
        _readyBatches = Channel.CreateBounded<ReadyBatch>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Appends a record to the appropriate batch.
    /// </summary>
    public async ValueTask<RecordAppendResult> AppendAsync(
        TopicPartition topicPartition,
        long timestamp,
        byte[]? key,
        byte[]? value,
        IReadOnlyList<RecordHeader>? headers,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RecordAccumulator));

        var batch = _batches.GetOrAdd(topicPartition, tp => new PartitionBatch(tp, _options));

        var result = batch.TryAppend(timestamp, key, value, headers);

        if (!result.Success)
        {
            // Batch is full, need to flush and create new batch
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
            }

            // Create new batch and retry
            batch = new PartitionBatch(topicPartition, _options);
            _batches[topicPartition] = batch;
            result = batch.TryAppend(timestamp, key, value, headers);
        }

        return result;
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
/// </summary>
internal sealed class PartitionBatch
{
    private readonly TopicPartition _topicPartition;
    private readonly ProducerOptions _options;
    private readonly ArrayBufferWriter<byte> _buffer;
    private readonly List<Record> _records = [];
    private readonly List<TaskCompletionSource<RecordMetadata>> _completionSources = [];
    private readonly object _lock = new();

    private long _baseTimestamp;
    private int _offsetDelta;
    private DateTimeOffset _createdAt;

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _buffer = new ArrayBufferWriter<byte>(options.BatchSize);
        _createdAt = DateTimeOffset.UtcNow;
    }

    public int RecordCount => _records.Count;
    public int EstimatedSize => _buffer.WrittenCount;

    public RecordAppendResult TryAppend(
        long timestamp,
        byte[]? key,
        byte[]? value,
        IReadOnlyList<RecordHeader>? headers)
    {
        lock (_lock)
        {
            if (_records.Count == 0)
            {
                _baseTimestamp = timestamp;
            }

            var timestampDelta = (int)(timestamp - _baseTimestamp);
            var record = new Record
            {
                TimestampDelta = timestampDelta,
                OffsetDelta = _offsetDelta,
                Key = key,
                Value = value,
                Headers = headers
            };

            // Estimate size
            var estimatedSize = EstimateRecordSize(key, value, headers);
            if (_buffer.WrittenCount + estimatedSize > _options.BatchSize && _records.Count > 0)
            {
                return new RecordAppendResult(false, null);
            }

            _records.Add(record);

            var tcs = new TaskCompletionSource<RecordMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completionSources.Add(tcs);

            _offsetDelta++;

            return new RecordAppendResult(true, tcs.Task);
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
                Records = _records.ToList()
            };

            return new ReadyBatch(
                _topicPartition,
                batch,
                _completionSources.ToList());
        }
    }

    private static int EstimateRecordSize(byte[]? key, byte[]? value, IReadOnlyList<RecordHeader>? headers)
    {
        var size = 20; // Base overhead
        size += key?.Length ?? 0;
        size += value?.Length ?? 0;

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                size += header.Key.Length + (header.Value?.Length ?? 0) + 10;
            }
        }

        return size;
    }
}

/// <summary>
/// Result of appending a record.
/// </summary>
public readonly record struct RecordAppendResult(bool Success, Task<RecordMetadata>? Future);

/// <summary>
/// A batch ready to be sent.
/// </summary>
public sealed class ReadyBatch
{
    public TopicPartition TopicPartition { get; }
    public RecordBatch RecordBatch { get; }
    public IReadOnlyList<TaskCompletionSource<RecordMetadata>> CompletionSources { get; }

    public ReadyBatch(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        IReadOnlyList<TaskCompletionSource<RecordMetadata>> completionSources)
    {
        TopicPartition = topicPartition;
        RecordBatch = recordBatch;
        CompletionSources = completionSources;
    }

    public void Complete(long baseOffset, DateTimeOffset timestamp)
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

    public void Fail(Exception exception)
    {
        foreach (var tcs in CompletionSources)
        {
            tcs.TrySetException(exception);
        }
    }
}
