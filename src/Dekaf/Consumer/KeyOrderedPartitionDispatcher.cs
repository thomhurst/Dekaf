using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Dekaf.Internal;

namespace Dekaf.Consumer;

// Only RunAsync owns queues, key membership and commit progress. Handler callbacks
// publish completed workers to an intrusive stack and wake that single coordinator.
internal sealed partial class KeyOrderedPartitionDispatcher<TKey, TValue>
{
    private readonly PartitionProcessorContext<TKey, TValue> _context;
    private readonly Func<IReadOnlyList<ConsumeResult<TKey, TValue>>, CancellationToken, ValueTask> _processor;
    private PendingRecord[] _records;
    private readonly int _maxBufferedRecords;
    private readonly object _lanes;
    private readonly bool _hasBinaryKeyComparer;
    private object? _freeLanes;
    private readonly Queue<KeyLane> _readyLanes;
    private readonly Stack<Worker> _freeWorkers;
    private readonly int _maxWorkers;
    private readonly int _batchSize;
    private readonly bool _automaticCompletion;
    // Completion callbacks hold no dispatcher lock and may resume its sole reader
    // inline, avoiding another thread-pool hop after an asynchronous user handler.
    private readonly AsyncAutoResetSignal _signal = new(inlineContinuations: true);
    private readonly Action _inputReadyCallback;
    private ConfiguredValueTaskAwaitable<bool>.ConfiguredValueTaskAwaiter _inputAwaiter;
    private Worker? _completedWorkers;
    private AutomaticPartitionProgress? _progress;
    private CancellationToken _processingToken;
    private ExceptionDispatchInfo? _failure;
    private int _inputReady;
    private bool _inputPending;
    private bool _inputCompleted;
    private int _head = -1;
    private int _tail = -1;
    private int _freeRecord;
    private long _lastReadOffset = -1;
    private int _lastReadEpoch = -1;
    private int _used;
    private int _activeWorkers;
    private int _laneCount;

    internal KeyOrderedPartitionDispatcher(
        PartitionProcessorContext<TKey, TValue> context,
        int maxBatchSize,
        int maxConcurrentHandlers,
        int maxBufferedRecords,
        Func<IReadOnlyList<ConsumeResult<TKey, TValue>>, CancellationToken, ValueTask> processor,
        IEqualityComparer<TKey>? keyComparer = null,
        bool automaticCompletion = false)
    {
        _context = context;
        _processor = processor;
        _maxBufferedRecords = maxBufferedRecords;
        var initialCapacity = Math.Min(maxBufferedRecords, 128);
        _records = new PendingRecord[initialCapacity];
        for (var index = 0; index < _records.Length; index++)
            _records[index].Next = index + 1;
        _records[_records.Length - 1].Next = -1;
        _maxWorkers = Math.Min(maxConcurrentHandlers, maxBufferedRecords);
        // A batch size is an upper bound. Divide reusable storage across workers so
        // multiplying the two user limits cannot allocate quadratic record storage.
        _batchSize = Math.Min(maxBatchSize, Math.Max(1, maxBufferedRecords / _maxWorkers));
        if (UseCompactKeys)
        {
            var comparer = keyComparer is not null && (_maxWorkers != 1 || _batchSize != 1)
                ? new CustomUncachedPartitionMessageKeyComparer<TKey>(keyComparer) : null;
            _lanes = new Dictionary<PartitionMessageKey<TKey>.Uncached, KeyLane>(initialCapacity, comparer);
        }
        else
        {
            IEqualityComparer<PartitionMessageKey<TKey>>? comparer = null;
            if (_maxWorkers != 1 || _batchSize != 1)
            {
                comparer = keyComparer is null
                    ? PartitionMessageKeyComparer<TKey>.Default
                    : new CustomPartitionMessageKeyComparer<TKey>(keyComparer);
            }
            _hasBinaryKeyComparer = comparer is BinaryPartitionMessageKeyComparer<TKey>;
            _lanes = new Dictionary<PartitionMessageKey<TKey>, KeyLane>(initialCapacity, comparer);
            _freeLanes = new Stack<KeyLane>(initialCapacity);
        }
        _readyLanes = new Queue<KeyLane>(initialCapacity);
        _freeWorkers = new Stack<Worker>(Math.Min(_maxWorkers, initialCapacity));
        _automaticCompletion = automaticCompletion;
        _inputReadyCallback = InputReady;
    }

    internal int LaneCount => Volatile.Read(ref _laneCount);

    public async ValueTask RunAsync(CancellationToken cancellationToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingToken = stopping.Token;
        using var registration = _processingToken.UnsafeRegister(
            static state => ((AsyncAutoResetSignal)state!).Signal(), _signal);
        if (_automaticCompletion)
            _progress = _context.EnableAutomaticCompletion();

        try
        {
            while (true)
            {
                DrainCompletions();
                _failure?.Throw();
                _processingToken.ThrowIfCancellationRequested();
                FinishInputWait();

                if (!_inputPending && !_inputCompleted && _used < _maxBufferedRecords)
                {
                    while (_used < _maxBufferedRecords && _context.TryReadMessage(out var record))
                    {
                        Enqueue(record);
                        // A continuously replenished input must not starve completed
                        // handlers or delay observing their failures until it empties.
                        if (Volatile.Read(ref _completedWorkers) is not null)
                        {
                            DrainCompletions();
                            _failure?.Throw();
                        }
                        _processingToken.ThrowIfCancellationRequested();
                        DispatchReadyLanes();
                        _failure?.Throw();
                    }

                    if (_used < _maxBufferedRecords)
                    {
                        BeginInputWait();
                        if (!_inputPending)
                            continue;
                    }
                }

                DispatchReadyLanes();
                _failure?.Throw();
                if (_inputCompleted && _used == 0)
                    return;

                await _signal.WaitAsync(Timeout.Infinite).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _failure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            try
            {
                if (_activeWorkers != 0 || _inputPending)
                    await stopping.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            // A cancelled handler still owns its batch until its actual completion.
            // Observe every registered callback before disposing the wake signal.
            while (_activeWorkers != 0 || _inputPending)
            {
                DrainCompletions();
                try
                {
                    FinishInputWait();
                }
                catch (Exception exception)
                {
                    _failure ??= ExceptionDispatchInfo.Capture(exception);
                }

                if (_activeWorkers != 0 || _inputPending)
                    await _signal.WaitAsync(Timeout.Infinite).ConfigureAwait(false);
            }

            for (var index = 0; index < _records.Length; index++)
            {
                if (_records[index].OwnsStorage)
                    _records[index].Record.ReleaseStorage();
                _records[index] = default;
            }

            ReleaseLanes();
            if (_hasBinaryKeyComparer)
                Unsafe.As<BinaryPartitionMessageKeyComparer<TKey>>(StandardLanes.Comparer).ReleaseCollisionState();
            if (_failure is not null)
            {
                // Failed removal or rebuilding can displace lanes. Their retained
                // keys remain owned until every in-flight worker has finished.
                ReleaseFailureLaneKeys();
            }
            Volatile.Write(ref _laneCount, 0);
            registration.Dispose();
            _signal.Dispose();
        }

        _failure?.Throw();
    }

    private void Enqueue(ConsumeResult<TKey, TValue> record)
    {
        if (_freeRecord < 0)
        {
            // Grow only when the active working set needs it. Large configured
            // limits must not eagerly reserve arrays for records never dispatched.
            var previousLength = _records.Length;
            try
            {
                Array.Resize(ref _records, (int)Math.Min((long)previousLength * 2, _maxBufferedRecords));
            }
            catch
            {
                record.ReleaseStorage();
                throw;
            }
            for (var slot = previousLength; slot < _records.Length; slot++)
                _records[slot].Next = slot + 1;
            _records[_records.Length - 1].Next = -1;
            _freeRecord = previousLength;
        }
        // Ownership transfers from the partition queue immediately, including if
        // a user key comparer throws while obtaining dictionary membership.
        var index = _freeRecord;
        _freeRecord = _records[index].Next;
        _used++;
        _records[index] = new PendingRecord
        {
            Record = record,
            OwnsStorage = true,
            Next = -1,
            PreviousPending = _tail,
            NextPending = -1,
            PreviousOffset = _lastReadOffset,
            PreviousEpoch = _lastReadEpoch
        };
        if (_tail >= 0)
            _records[_tail].NextPending = index;
        else
            _head = index;
        _tail = index;
        if (!record.IsPartitionEof)
        {
            _lastReadOffset = record.Offset;
            _lastReadEpoch = record.LeaderEpoch ?? -1;
        }
        // One sequential record handler needs no key comparison. Other shapes
        // preserve wire-null identity and cache binary hashes through lane removal.
        var key = _maxWorkers == 1 && _batchSize == 1
            ? default
            : PartitionMessageKey<TKey>.From(record.Key, record.IsKeyNull);
        var binaryKeyComparer = _hasBinaryKeyComparer
            ? Unsafe.As<BinaryPartitionMessageKeyComparer<TKey>>(StandardLanes.Comparer) : null;
        KeyLane? lane = null;
        var sampledHash = false;
        if (binaryKeyComparer is not null)
        {
            // An existing sampled lane already determines the key's membership.
            // Only unmatched keys need the full hash after collision promotion.
            if (binaryKeyComparer.TryGetSampledKey(key, out var sampledKey))
                StandardLanes.TryGetValue(sampledKey, out lane);
            if (lane is null && key.HasValue)
                key = key.WithBinaryHashCode(binaryKeyComparer.ComputeHashCode(key, out sampledHash));
        }
#if NETSTANDARD2_0
        if (lane is null && !TryGetLane(key, out lane))
        {
            lane = RentLane();
            lane.Key = key;
            lane.SampledHash = sampledHash;
            if (sampledHash) binaryKeyComparer!.AddSampledLane();
            lane.StorageOrNext = record.RetainStorage();
            try
            {
                AddLane(key, lane);
            }
            catch
            {
                lane.ReleaseKey();
                throw;
            }
            Volatile.Write(ref _laneCount, StorageCount);
        }
#else
        // The coordinator owns membership. Publish the lane before retaining its
        // storage, so shutdown owns cleanup even if initialization fails.
        if (lane is null)
        {
            ref var entry = ref GetLaneEntry(key);
            lane = entry;
            if (lane is null)
            {
                lane = RentLane();
                entry = lane;
                lane.Key = key;
                lane.SampledHash = sampledHash;
                if (sampledHash) binaryKeyComparer!.AddSampledLane();
                lane.StorageOrNext = record.RetainStorage();
                Volatile.Write(ref _laneCount, StorageCount);
            }
        }
#endif

        if (lane.Head < 0)
            lane.Head = index;
        else
            _records[lane.Tail].Next = index;
        lane.Tail = index;
        if (!lane.Scheduled)
        {
            lane.Scheduled = true;
            _readyLanes.Enqueue(lane);
        }
        if (binaryKeyComparer is { NeedsFullHashing: true })
            StrengthenBinaryHashing(binaryKeyComparer, lane);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StrengthenBinaryHashing(BinaryPartitionMessageKeyComparer<TKey> comparer, KeyLane triggeringLane)
    {
        // Strengthen at most the eight compared keys and the triggering lane.
        // Unrelated lanes stay in the same table under their cached sample hashes.
        var dictionary = StandardLanes;
        var keys = comparer.TakeCollisionKeys(out var count);
        comparer.EnableFullHashing();
        try
        {
            for (var index = 0; index < count; index++)
            {
                if (dictionary.TryGetValue(keys[index], out var lane) && lane.SampledHash)
                    StrengthenLane(lane, comparer);
            }
            if (triggeringLane.SampledHash) StrengthenLane(triggeringLane, comparer);
        }
        finally
        {
            Array.Clear(keys, 0, count);
        }
    }

    private void StrengthenLane(KeyLane lane, BinaryPartitionMessageKeyComparer<TKey> comparer)
    {
        var dictionary = StandardLanes;
        // Preserve ownership before removing a key; hashing or reinsertion can fail
        // for invalidated backing memory or a mutated key. Failure cleanup joins workers.
        var key = lane.Key.WithBinaryHashCode(comparer.ComputeHashCode(lane.Key));
#if NETSTANDARD2_0
        if (!dictionary.TryGetValue(lane.Key, out var found) || !ReferenceEquals(found, lane) || !dictionary.Remove(lane.Key))
            throw new InvalidOperationException("A partition key changed its hash code or equality while being processed.");
#else
        if (!dictionary.Remove(lane.Key, out var removed) || !ReferenceEquals(removed, lane))
        {
            // Mutation can displace another active lane. Retain its storage until
            // shutdown has observed every worker, just as completion removal does.
            if (removed is not null) ReturnLane(removed);
            throw new InvalidOperationException("A partition key changed its hash code or equality while being processed.");
        }
#endif
        lane.Key = key;
        lane.SampledHash = false;
        comparer.RemoveSampledLane();
        try
        {
            dictionary.Add(key, lane);
        }
        catch (Exception error)
        {
            ReturnLane(lane);
            if (error is ArgumentException)
                throw new InvalidOperationException(
                    "A partition key changed its hash code or equality while being processed.", error);
            throw;
        }
    }

    private void DispatchReadyLanes()
    {
        while (_failure is null && _activeWorkers < _maxWorkers && _readyLanes.TryDequeue(out var lane))
        {
            _processingToken.ThrowIfCancellationRequested();
            var worker = _freeWorkers.Count != 0
                ? _freeWorkers.Pop()
                : new Worker(this, _batchSize);
            worker.Lane = lane;
            while (worker.Batch.Count < _batchSize && lane.Head >= 0)
            {
                var index = lane.Head;
                lane.Head = _records[index].Next;
                worker.Add(_records[index].Record, index);
            }

            _activeWorkers++;
            bool completed;
            try
            {
                var processing = _processor(worker.Batch, _processingToken);
                worker.Awaiter = processing.ConfigureAwait(false).GetAwaiter();
                completed = worker.Awaiter.IsCompleted;
            }
            catch (Exception exception)
            {
                _failure ??= ExceptionDispatchInfo.Capture(exception);
                // A synchronous throw never registered a completion callback.
                ReleaseWorker(worker, succeeded: false);
                continue;
            }
            if (completed)
                CompleteWorker(worker);
            else
                worker.Register();
        }
    }

    private void CompleteWorker(Worker worker)
    {
        var succeeded = false;
        try
        {
            worker.Awaiter.GetResult();
            succeeded = true;
        }
        catch (Exception exception)
        {
            _failure ??= ExceptionDispatchInfo.Capture(exception);
        }
        try
        {
            ReleaseWorker(worker, succeeded);
        }
        catch (Exception exception)
        {
            // A key comparer can throw during lane removal. Retain the failure
            // without losing other workers in the detached completion chain.
            _failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }

    private void ReleaseWorker(Worker worker, bool succeeded)
    {
        var frontierChanged = false;
        for (var index = 0; index < worker.Batch.Count; index++)
        {
            var slot = worker.Indices[index];
            ref var pending = ref _records[slot];
            var record = pending.Record;
            pending.Record = default;
            pending.OwnsStorage = false;
            record.ReleaseStorage();
            if (!succeeded)
                continue;
            if (!record.IsPartitionEof)
                _progress?.MarkProcessed(record.Offset);

            // Unlink a completed record in constant time. The next unfinished
            // record remembers its delivered predecessor, including compacted
            // offset gaps. Completed payloads never occupy the pending budget.
            if (pending.PreviousPending >= 0)
                _records[pending.PreviousPending].NextPending = pending.NextPending;
            else
            {
                _head = pending.NextPending;
                frontierChanged = true;
            }
            if (pending.NextPending >= 0)
                _records[pending.NextPending].PreviousPending = pending.PreviousPending;
            else
                _tail = pending.PreviousPending;

            pending = new PendingRecord { Next = _freeRecord };
            _freeRecord = slot;
            _used--;
        }
        worker.Batch.Clear();
        worker.Awaiter = default;
        _activeWorkers--;
        _freeWorkers.Push(worker);
        var lane = worker.Lane!;
        worker.Lane = null;
        if (lane.Head >= 0)
        {
            _readyLanes.Enqueue(lane);
        }
        else
        {
            // A mutable user key can make its dictionary entry unreachable.
            // Keep the lane owned until shutdown instead of pooling an alias.
#if NETSTANDARD2_0
            if (!TryGetLane(lane.Key, out var found) || !ReferenceEquals(found, lane) || !RemoveLane(lane.Key))
                throw new InvalidOperationException("A partition key changed its hash code or equality while being processed.");
#else
            if (!RemoveLane(lane.Key, out var removed) || !ReferenceEquals(removed, lane))
            {
                // A mutated key may remove a different active lane. Keep its storage
                // owned until every handler finishes, even though membership is gone.
                if (removed is not null)
                {
                    if (UseCompactKeys)
                        worker.Lane = removed;
                    else
                        ReturnLane(removed);
                }
                throw new InvalidOperationException("A partition key changed its hash code or equality while being processed.");
            }
#endif
            if (lane.SampledHash)
                Unsafe.As<BinaryPartitionMessageKeyComparer<TKey>>(StandardLanes.Comparer).RemoveSampledLane();
            lane.ReleaseKey();
            lane.Scheduled = false;
            lane.Tail = -1;
            ReturnLane(lane);
            Volatile.Write(ref _laneCount, StorageCount);
        }

        if (frontierChanged)
            PublishCommit();
    }

    private void PublishCommit()
    {
        var offset = _head >= 0 ? _records[_head].PreviousOffset : _lastReadOffset;
        var epoch = _head >= 0 ? _records[_head].PreviousEpoch : _lastReadEpoch;
        if (offset >= 0)
            _progress?.Publish(offset + 1, epoch);
    }

    private void BeginInputWait()
    {
        _inputAwaiter = _context.WaitToReadMessageAsync(_processingToken).ConfigureAwait(false).GetAwaiter();
        if (_inputAwaiter.IsCompleted)
        {
            _inputCompleted = !_inputAwaiter.GetResult();
            _inputAwaiter = default;
            return;
        }
        Volatile.Write(ref _inputReady, 0);
        _inputPending = true;
        _inputAwaiter.UnsafeOnCompleted(_inputReadyCallback);
    }

    private void InputReady()
    {
        Volatile.Write(ref _inputReady, 1);
        _signal.Signal();
    }

    private void FinishInputWait()
    {
        if (!_inputPending || Volatile.Read(ref _inputReady) == 0)
            return;
        _inputPending = false;
        try
        {
            _inputCompleted = !_inputAwaiter.GetResult();
        }
        finally
        {
            _inputAwaiter = default;
        }
    }

    private void PublishCompletion(Worker worker)
    {
        Worker? head;
        do
        {
            head = Volatile.Read(ref _completedWorkers);
            worker.NextCompleted = head;
        } while (Interlocked.CompareExchange(ref _completedWorkers, worker, head) != head);
        _signal.Signal();
    }

    private void DrainCompletions()
    {
        var worker = Interlocked.Exchange(ref _completedWorkers, null);
        while (worker is not null)
        {
            var next = worker.NextCompleted;
            worker.NextCompleted = null;
            CompleteWorker(worker);
            worker = next;
        }
    }

    private struct PendingRecord
    {
        internal ConsumeResult<TKey, TValue> Record;
        internal long PreviousOffset;
        internal int PreviousEpoch;
        internal int Next;
        internal int PreviousPending;
        internal int NextPending;
        internal bool OwnsStorage;
    }

    private sealed class KeyLane
    {
        internal PartitionMessageKey<TKey> Key;
        // Active lanes retain PendingFetchData. A released compact lane reuses
        // this reference for its free-pool link, without enlarging every lane.
        internal object? StorageOrNext;
        internal int Head = -1;
        internal int Tail = -1;
        internal bool Scheduled;
        internal bool SampledHash;

        internal void ReleaseKey()
        {
            Unsafe.As<PendingFetchData>(StorageOrNext)?.ReleaseAfterProcessing();
            StorageOrNext = null;
            Key = default;
            SampledHash = false;
        }
    }

    private sealed class Worker
    {
        private readonly KeyOrderedPartitionDispatcher<TKey, TValue> _dispatcher;
        internal readonly PartitionRecordBatch<TKey, TValue> Batch;
        internal int[] Indices;
        private readonly Action _callback;
        internal ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter Awaiter;
        internal KeyLane? Lane;
        internal Worker? NextCompleted;

        internal Worker(KeyOrderedPartitionDispatcher<TKey, TValue> dispatcher, int batchSize)
        {
            _dispatcher = dispatcher;
            Batch = new PartitionRecordBatch<TKey, TValue>(batchSize);
            Indices = new int[Math.Min(batchSize, 16)];
            _callback = Complete;
        }

        internal void Register()
        {
            // Self marks a registered worker whose callback has not claimed it.
            // Publication replaces this sentinel with the normal completion link.
            Volatile.Write(ref NextCompleted, this);
            try
            {
                Awaiter.UnsafeOnCompleted(_callback);
            }
            catch (Exception exception)
            {
                _dispatcher._failure ??= ExceptionDispatchInfo.Capture(exception);
                // Registration may invoke its callback and then throw. Observe an
                // already-published completion once; otherwise suppress late callbacks
                // and release the worker whose awaiter could not be registered.
                if (Interlocked.CompareExchange(ref NextCompleted, null, this) == this)
                    _dispatcher.ReleaseWorker(this, succeeded: false);
            }
        }

        private void Complete()
        {
            if (Interlocked.CompareExchange(ref NextCompleted, null, this) == this)
                _dispatcher.PublishCompletion(this);
        }

        internal void Add(ConsumeResult<TKey, TValue> record, int index)
        {
            if (Batch.Count == Indices.Length)
                Array.Resize(ref Indices, (int)Math.Min((long)Indices.Length * 2, _dispatcher._batchSize));
            Indices[Batch.Count] = index;
            Batch.Add(record);
        }
    }
}
