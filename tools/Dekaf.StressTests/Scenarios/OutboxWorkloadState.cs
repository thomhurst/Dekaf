using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

/// <summary>Bounded message identities shared by one database writer and one Kafka reader.</summary>
internal sealed class OutboxWorkloadState
{
    internal const int WindowSize = 4096;
    private readonly long[] _completedSequences = new long[WindowSize];
    private readonly long[] _readOffsets;
    private readonly int _messageSize;
    private readonly long _runIdentity;
    private ThroughputTracker? _throughput;
    private Exception? _failure;
    private long _reserved;
    private long _committed;
    private long _completed;
    private long _duplicates;

    public long Reserved => Volatile.Read(ref _reserved);
    public long Committed => Volatile.Read(ref _committed);
    public long Completed => Volatile.Read(ref _completed);
    public long Duplicates => Volatile.Read(ref _duplicates);
    public LatencyTracker Latency { get; } = new();

    public OutboxWorkloadState(int messageSize, int partitions, long runIdentity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(messageSize, 24);
        ArgumentOutOfRangeException.ThrowIfLessThan(partitions, 1);
        _messageSize = messageSize;
        _runIdentity = runIdentity;
        _readOffsets = new long[partitions];
        for (var i = 0; i < WindowSize; i++)
            _completedSequences[i] = i - WindowSize;
    }

    public void BeginCycle(ThroughputTracker throughput)
    {
        ThrowIfFailed();
        if (Reserved != Committed || Completed != Committed)
            throw new InvalidOperationException("The previous outbox phase has not fully drained.");
        Latency.Reset();
        _throughput = throughput;
    }

    // Reservation precedes SaveChanges: a post-commit notification can deliver the row
    // before SaveChanges returns to the writer. Failed saves fail the entire workload.
    public bool TryReserve(out long sequence)
    {
        sequence = _reserved;
        if (sequence - Completed >= WindowSize
            || Volatile.Read(ref _completedSequences[sequence % WindowSize]) != sequence - WindowSize)
            return false;
        Volatile.Write(ref _reserved, sequence + 1);
        return true;
    }

    public void RecordCommitted(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var committed = _committed + count;
        if (committed > Reserved)
            throw new InvalidOperationException("Committed rows exceed reserved message identities.");
        Volatile.Write(ref _committed, committed);
    }

    public byte[] CreatePayload(long sequence)
    {
        var payload = new byte[_messageSize];
        BinaryPrimitives.WriteInt64LittleEndian(payload, sequence);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), Stopwatch.GetTimestamp());
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(16), _runIdentity);
        return payload;
    }

    public bool Process(byte[]? payload, int partition, long offset)
    {
        if (payload is null || payload.Length != _messageSize
            || BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(16)) != _runIdentity)
            throw new InvalidOperationException("The outbox record does not belong to this workload.");
        var sequence = BinaryPrimitives.ReadInt64LittleEndian(payload);
        var timestamp = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(8));
        var elapsed = Stopwatch.GetTimestamp() - timestamp;
        if (sequence < 0 || sequence >= Reserved || timestamp <= 0 || elapsed < 0)
            throw new InvalidOperationException("The outbox record has an invalid identity or timestamp.");
        if ((uint)partition >= (uint)_readOffsets.Length || offset != _readOffsets[partition])
            throw new InvalidOperationException("The Kafka reader skipped or repeated a partition offset.");
        Volatile.Write(ref _readOffsets[partition], offset + 1);

        ref var slot = ref _completedSequences[sequence % WindowSize];
        var previous = Interlocked.CompareExchange(ref slot, sequence, sequence - WindowSize);
        if (previous >= sequence)
        {
            Interlocked.Increment(ref _duplicates);
            return false;
        }
        if (previous != sequence - WindowSize)
            throw new InvalidOperationException("An outbox identity skipped an unfinished slot generation.");
        Latency.RecordTicks(elapsed);
        _throughput!.RecordMessage(payload.Length);
        Interlocked.Increment(ref _completed);
        return true;
    }

    public bool HasReadThrough(long[] endOffsets)
    {
        if (endOffsets.Length != _readOffsets.Length)
            throw new ArgumentException("Partition counts do not match.", nameof(endOffsets));
        for (var i = 0; i < endOffsets.Length; i++)
            if (Volatile.Read(ref _readOffsets[i]) < endOffsets[i])
                return false;
        return true;
    }

    public void RecordFailure(Exception exception) => Interlocked.CompareExchange(ref _failure, exception, null);

    public void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } failure)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
