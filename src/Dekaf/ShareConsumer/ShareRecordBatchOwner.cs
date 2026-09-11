using System.Buffers;
using System.Runtime.CompilerServices;
using Dekaf.Protocol.Records;

namespace Dekaf.ShareConsumer;

/// <summary>Owns parsed payload storage for one poll and any renewed records from that batch.</summary>
internal sealed class ShareRecordBatchOwner
{
    internal const uint MaximumGeneration = int.MaxValue;
    // One reference belongs to the parser, one to the poll scope. Renewals add
    // references only when requested; ordinary delivery needs no per-record counter.
    private int _references;
    // A replay snapshot retains each owner once, even when records are interleaved.
    internal bool RenewalPinned;
    private RecordBatch? _batch;
    private byte[]? _payload;
    private ArrayPool<byte>? _payloadPool;
    private readonly Pool _pool;

    private ShareRecordBatchOwner(TopicPartition partition, Pool pool)
    {
        Topic = partition.Topic;
        Partition = partition.Partition;
        _pool = pool;
    }

    // Metadata never changes: results retained past their payload lifetime still expose it.
    internal string Topic { get; }
    internal int Partition { get; }
    internal uint Generation { get; private set; }

    internal void Retain()
    {
        // IKafkaShareConsumer requires serialized access to its operations.
        if (_references == 0)
            throw new InvalidOperationException("Cannot renew a record after its borrowed payload lifetime has ended.");
        _references++;
    }

    internal void Release()
    {
        if (--_references != 0)
            return;
        var batch = _batch;
        _batch = null;
        batch?.DisposeAndReturnUnownedConsumerBatch();
        var payload = _payload;
        var payloadPool = _payloadPool;
        _payload = null;
        _payloadPool = null;
        if (payload is not null)
            payloadPool!.Return(payload);
        // Never wrap the generation: an arbitrarily old result must not become valid
        // again. Only exhaustion of the full generation retires an owner.
        if (Generation != MaximumGeneration)
            _pool.Return(this);
        else
            _pool.RetireOwner();
    }

    internal void CompleteParsing()
    {
        var batch = _batch!;
        _payload = batch.DetachRecordData(out _payloadPool);
        _batch = null;
        batch.DisposeAndReturnUnownedConsumerBatch();
        Release();
    }

    // Consumer operations already serialize owner references and pool access.
    internal sealed class Pool(TopicPartition partition)
    {
        private ShareRecordBatchOwner?[] _available = new ShareRecordBatchOwner?[128];
        private int _availableCount;
        private int _ownerCount;
        private long _misses;

        internal int MaxPoolSize => _available.Length;
        internal long Misses => _misses;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ShareRecordBatchOwner Rent(RecordBatch batch)
        {
            ShareRecordBatchOwner owner;
            if (_availableCount == 0)
            {
                owner = Create();
            }
            else
            {
                var index = --_availableCount;
                owner = _available[index]!;
                _available[index] = null;
            }
            owner.Generation++;
            owner._references = 2;
            owner._batch = batch;
            return owner;
        }

        private ShareRecordBatchOwner Create()
        {
            // Serialized consumer access means this count includes both pooled owners
            // and owners retained by a poll or renewal. Grow only on a pool miss;
            // ordinary rents/returns add no counter or capacity check per batch.
            if (_ownerCount == _available.Length)
            {
                // A miss means every slot is empty; no retained references need copying.
                _available = new ShareRecordBatchOwner?[
                    _available.Length <= int.MaxValue / 2 ? _available.Length * 2 : int.MaxValue];
            }
            var owner = new ShareRecordBatchOwner(partition, this);
            _ownerCount++;
            _misses++;
            return owner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Return(ShareRecordBatchOwner owner) => _available[_availableCount++] = owner;

        internal void RetireOwner() => _ownerCount--;
    }
}
