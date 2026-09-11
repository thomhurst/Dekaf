using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dekaf.Consumer;

internal sealed class CompletedOffsetNode
{
    internal OffsetCompletionBatch? Owner;
    internal long Start;
    internal long End;
    internal int Epoch;
    internal int Height;
    internal CompletedOffsetNode? Left;
    internal CompletedOffsetNode? Right;
}

/// <summary>
/// Reserves nodes in bounded chunks before records enter the partition queue. Records retain this stable
/// identity independently of their borrowed fetch buffers and channel residency.
/// The owning lane serializes completion and publication-end bookkeeping.
/// </summary>
internal sealed class OffsetCompletionBatch
{
    private const int NodeInitializationChunkSize = 1024;
    private object _owner;
    internal CompletedOffsetRanges LaneIdentity
    {
        get
        {
            // Foreign-record validation runs before taking the lane lock. Keep one
            // reference while concurrent completion can detach this batch from a pair.
            var owner = _owner;
            return owner is CompletedOffsetRanges lane ? lane : ((OffsetCompletionPair)owner).LaneIdentity;
        }
    }
    internal readonly int Capacity;
    internal CompletedOffsetNode[]? Nodes;
    private int _publishedRecords;
    private uint _remaining = uint.MaxValue;
    private int _ranges;
    internal int ReservationIndex = -1;
    private bool _publishing = true;
    private bool _abandoned;

    internal OffsetCompletionBatch(int capacity, CompletedOffsetRanges owner)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, PackedProcessingEpoch.IndexCapacity);
        Capacity = capacity;
        _owner = owner;
        Nodes = ArrayPool<CompletedOffsetNode>.Shared.Rent(capacity);
    }

    // Failed channel writes reuse the unpublished slot. All storage is reserved before
    // publication. The requested capacity bounds every independently pooled array.
    internal int PrepareRecord(long previous)
    {
        var index = _publishedRecords;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Capacity);
        var node = Nodes![index];
        if (node is null)
        {
            InitializeNextChunk(index);
            node = Nodes[index];
        }
        // A record owns this slot until completion. Negative height marks a
        // pending record; completed nodes have zero or a positive AVL height.
        node.Start = previous;
        node.Height = -1;
        return index;
    }

    internal void PublishRecord() => _publishedRecords++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeNextChunk(int start)
    {
        var nodes = Nodes!;
        var end = start + Math.Min(NodeInitializationChunkSize, nodes.Length - start);
        // Slabs retain their initialized prefix across pool returns. Publication
        // reaches the first uninitialized slot only at a chunk boundary.
        for (var index = start; index < end; index++)
            nodes[index] ??= new CompletedOffsetNode();
    }

    internal CompletedOffsetNode GetCompletionNode(int index) => Nodes![index];

    internal CompletedOffsetNode Allocate(CompletedOffsetNode node, long offset, int epoch)
    {
        node.Left = null;
        node.Right = null;
        node.Owner = this;
        node.End = offset;
        node.Epoch = epoch;
        node.Height = 1;
        return node;
    }
    // The remaining sentinel prevents return while the writer can still publish.
    // Removing unused reservations here also handles filters, cancellation, and short batches.
    internal void Finish(int published)
    {
        _publishing = false;
        // The sentinel exceeds the largest supported reservation (PackedProcessingEpoch.IndexCapacity).
        // Completion can subtract published records before the writer finishes.
        // Automatic startup can abandon this batch while the router still counts writes;
        // those writes belong to automatic progress and must not restore manual ownership.
        _remaining = _abandoned ? 0 : _remaining - (uint.MaxValue - (uint)published);
        TryReturn();
        if (Nodes is not null)
            LaneIdentity.FinishReservation(this);
    }

    internal void Abandon()
    {
        _abandoned = true;
        if (!_publishing)
            _remaining = 0;
        TryReturn();
    }

    internal void AddRange() => _ranges++;

    internal void RemoveRange(CompletedOffsetNode node)
    {
        node.Owner = null;
        node.Right = null;
        node.Left = null;
        node.Height = 0;
        _ranges--;
        TryReturn();
    }

    internal void Complete()
    {
        if (--_remaining == 0)
            TryReturn();
    }

    private void TryReturn()
    {
        if (_remaining != 0 || _ranges != 0 || Nodes is null)
            return;
        if (_owner is OffsetCompletionPair pair)
        {
            _owner = pair.LaneIdentity;
            pair.Release(this);
        }
        else
        {
            ((CompletedOffsetRanges)_owner).RemoveReservation(this);
        }
        ReturnStorage();
    }

    internal void Join(OffsetCompletionPair pair) => _owner = pair;

    internal void Reclaim(CompletedOffsetRanges lane)
    {
        _owner = lane;
        ReturnStorage();
    }

    private void ReturnStorage()
    {
        var nodes = Nodes;
        Nodes = null;
        // A finalizer can also see an abandoned, unreachable lane whose tree was
        // never retired. Its live range links must not enter the shared node pool.
        if (nodes is not null && _ranges == 0)
            ArrayPool<CompletedOffsetNode>.Shared.Return(nodes);
    }
}

/// <summary>
/// Shares finalization between two unfinished reservations. Ordinary completed
/// batches need neither a finalizer nor weak tracking. Retained records keep their
/// pair alive; the lane retains at most one finished pair independently of records.
/// </summary>
internal sealed class OffsetCompletionPair : IDisposable
{
    internal readonly CompletedOffsetRanges LaneIdentity;
    private OffsetCompletionBatch? _first;
    private OffsetCompletionBatch? _second;
    internal int ReservationIndex = -1;
    internal bool IsFull => _first is not null && _second is not null;

    internal OffsetCompletionPair(CompletedOffsetRanges lane) => LaneIdentity = lane;

    ~OffsetCompletionPair()
    {
        lock (LaneIdentity)
        {
            LaneIdentity.RemoveReservation(this);
            _first?.Reclaim(LaneIdentity);
            _second?.Reclaim(LaneIdentity);
        }
    }

    internal void Add(OffsetCompletionBatch batch)
    {
        if (_first is null)
            _first = batch;
        else
            _second = batch;
        batch.Join(this);
    }

    internal void Release(OffsetCompletionBatch batch)
    {
        if (ReferenceEquals(_first, batch))
            _first = null;
        else
            _second = null;
        if (_first is null && _second is null)
            Dispose();
    }

    public void Dispose()
    {
        LaneIdentity.RemoveReservation(this);
        var first = _first;
        var second = _second;
        _first = null;
        _second = null;
        // Pairs contain only finished batches. Retirement clears the AVL tree
        // before disposal, so each member can return its reserved nodes now.
        first?.Reclaim(LaneIdentity);
        second?.Reclaim(LaneIdentity);
        System.GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Merges completed delivery-order runs in an AVL tree. Nodes are reserved by each
/// batch, so first-time fragmentation never grows storage on the completing thread.
/// </summary>
internal sealed class CompletedOffsetRanges
{
    private const int RetiredFlag = int.MinValue;
    private const int FinishedReservationFlag = 1 << 30;
    private const int ReservationCountMask = FinishedReservationFlag - 1;
    private CompletedOffsetNode? _root;
    private object?[]? _reservations;
    private GCHandle[]? _weakReservations;
    // Keep lifecycle flags in the count word so weak tracking does not grow each lane.
    private int _reservationState;
    internal int Count { get; private set; }
    internal bool IsRetired => (_reservationState & RetiredFlag) != 0;
    private int ReservationCount => _reservationState & ReservationCountMask;

    internal void AddReservation(OffsetCompletionBatch batch)
    {
        if (IsRetired)
        {
            batch.Abandon();
            return;
        }
        if (_reservations is null)
            _reservations = ArrayPool<object?>.Shared.Rent(16);
        else if (ReservationCount == _reservations.Length)
            GrowReservations();
        var index = ReservationCount;
        batch.ReservationIndex = index;
        _reservations[index] = batch;
        if (_weakReservations is not null)
            _weakReservations[index] = default;
        _reservationState++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowReservations()
    {
        var previous = _reservations!;
        var previousWeak = _weakReservations;
        var capacity = checked(previous.Length * 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, ReservationCountMask);
        var next = ArrayPool<object?>.Shared.Rent(capacity);
        GCHandle[]? nextWeak;
        try
        {
            nextWeak = previousWeak is null ? null : ArrayPool<GCHandle>.Shared.Rent(next.Length);
        }
        catch
        {
            ArrayPool<object?>.Shared.Return(next, clearArray: true);
            throw;
        }
        Array.Copy(previous, next, ReservationCount);
        if (previousWeak is not null)
            Array.Copy(previousWeak, nextWeak!, ReservationCount);
        // Publish both arrays together. A failed rent must leave the old index valid.
        _reservations = next;
        _weakReservations = nextWeak;
        ArrayPool<object?>.Shared.Return(previous, clearArray: true);
        if (previousWeak is not null)
            ArrayPool<GCHandle>.Shared.Return(previousWeak, clearArray: true);
    }

    internal void RemoveReservation(OffsetCompletionBatch batch)
    {
        var index = batch.ReservationIndex;
        if (index < 0)
            return;
        RemoveReservation(index);
        batch.ReservationIndex = -1;
    }

    internal void RemoveReservation(OffsetCompletionPair pair)
    {
        var index = pair.ReservationIndex;
        if (index < 0)
            return;
        RemoveReservation(index);
        pair.ReservationIndex = -1;
    }

    private static void SetReservationIndex(object reservation, int index)
    {
        if (reservation is OffsetCompletionBatch batch)
            batch.ReservationIndex = index;
        else
            ((OffsetCompletionPair)reservation).ReservationIndex = index;
    }

    private void RemoveReservation(int index)
    {
        if (index == 0)
            _reservationState &= ~FinishedReservationFlag;
        if (_weakReservations is not null && _weakReservations[index].IsAllocated)
            _weakReservations[index].Free();
        var last = --_reservationState & ReservationCountMask;
        if (index != last)
        {
            var moved = GetReservation(last);
            _reservations![index] = _reservations[last];
            if (_weakReservations is not null)
                _weakReservations[index] = _weakReservations[last];
            SetReservationIndex(moved, index);
        }
        _reservations![last] = null;
        if (_weakReservations is not null)
            _weakReservations[last] = default;
    }

    // Keep at most two finished batches for prompt retirement and pool reuse.
    // Older pairs belong to retained records and actual completed ranges; weak
    // handles allow retirement to reclaim them without retaining discarded records.
    internal void FinishReservation(OffsetCompletionBatch batch)
    {
        if (IsRetired || batch.ReservationIndex < 0)
            return;
        if ((_reservationState & FinishedReservationFlag) != 0)
        {
            if (_reservations![0] is OffsetCompletionBatch previous)
            {
                var pair = new OffsetCompletionPair(this);
                RemoveReservation(batch);
                previous.ReservationIndex = -1;
                pair.Add(previous);
                pair.Add(batch);
                // Replace the retained batch in place. Removing and promoting it
                // would repeatedly move unrelated weak reservations through slot zero.
                pair.ReservationIndex = 0;
                _reservations[0] = pair;
                return;
            }
            var finishedPair = (OffsetCompletionPair)_reservations[0]!;
            if (!finishedPair.IsFull)
            {
                RemoveReservation(batch);
                finishedPair.Add(batch);
                return;
            }
            if (_weakReservations is null)
            {
                _weakReservations = ArrayPool<GCHandle>.Shared.Rent(_reservations!.Length);
                Array.Clear(_weakReservations);
            }
            // Track resurrection so retirement can still obtain a pair whose
            // finalizer is pending. Both paths remove its handle under this lock.
            _weakReservations[0] = GCHandle.Alloc(_reservations![0], GCHandleType.WeakTrackResurrection);
            _reservations[0] = null;
        }
        PromoteReservation(batch, batch.ReservationIndex);
    }

    private void PromoteReservation(object reservation, int index)
    {
        if (index != 0)
        {
            var first = GetReservation(0);
            _reservations![index] = _reservations[0];
            if (_weakReservations is not null)
            {
                _weakReservations[index] = _weakReservations[0];
                _weakReservations[0] = default;
            }
            SetReservationIndex(first, index);
            _reservations[0] = reservation;
            SetReservationIndex(reservation, 0);
        }
        _reservationState |= FinishedReservationFlag;
    }

    private object GetReservation(int index) =>
        _reservations![index] ?? _weakReservations![index].Target!;

    // Retire after the processor exits, or when its automatic coordinator takes
    // ownership of progress before processing records. Publishing batches retain
    // their arrays until Finish; completed manual ranges no longer advance.
    internal void Retire()
    {
        _reservationState |= RetiredFlag;
        Clear(_root);
        _root = null;
        while (ReservationCount != 0)
        {
            if (GetReservation(ReservationCount - 1) is OffsetCompletionBatch batch)
            {
                RemoveReservation(batch);
                batch.Abandon();
            }
            else
            {
                ((OffsetCompletionPair)GetReservation(ReservationCount - 1)).Dispose();
            }
        }
        if (_reservations is not null)
        {
            var reservations = _reservations;
            _reservations = null;
            ArrayPool<object?>.Shared.Return(reservations, clearArray: true);
        }
        if (_weakReservations is not null)
        {
            var reservations = _weakReservations;
            _weakReservations = null;
            ArrayPool<GCHandle>.Shared.Return(reservations, clearArray: true);
        }
    }

    private void Clear(CompletedOffsetNode? node)
    {
        if (node is null)
            return;
        Clear(node.Left);
        Clear(node.Right);
        Free(node);
    }

    internal bool Complete(OffsetCompletionBatch owner, CompletedOffsetNode reserved, long offset, int epoch,
        long committed, out long completed, out int completedEpoch)
    {
        var previous = reserved.Start;
        reserved.Height = 0;
        completed = offset;
        completedEpoch = epoch;
        if (_root is null && previous == committed)
        {
            owner.Complete();
            return true;
        }
        var left = Floor(previous);
        if (left is not null && left.End >= offset)
            return false;
        var right = Find(offset);
        var advances = previous == committed;
        if (right is not null)
        {
            completed = right.End;
            completedEpoch = right.Epoch;
            if (advances)
            {
                _root = Remove(_root!, offset);
            }
            else if (left is not null && left.End == previous)
            {
                left.End = completed;
                left.Epoch = completedEpoch;
                _root = Remove(_root!, offset);
            }
            else
            {
                // No delivered offset exists between this record and its predecessor.
                // Moving the start backward therefore cannot cross another tree key.
                right.Start = previous;
            }
        }
        else if (!advances)
        {
            if (left is not null && left.End == previous)
            {
                left.End = completed;
                left.Epoch = completedEpoch;
            }
            else
            {
                var node = owner.Allocate(reserved, completed, completedEpoch);
                owner.AddRange();
                Count++;
                _root = Insert(_root, node);
            }
        }
        owner.Complete();
        return advances;
    }

    private CompletedOffsetNode? Find(long start)
    {
        var node = _root;
        while (node is not null)
        {
            var key = node.Start;
            if (key == start)
                return node;
            node = start < key ? node.Left : node.Right;
        }
        return default;
    }

    private CompletedOffsetNode? Floor(long start)
    {
        var node = _root;
        CompletedOffsetNode? floor = default;
        while (node is not null)
        {
            if (node.Start > start)
                node = node.Left;
            else
            {
                floor = node;
                node = node.Right;
            }
        }
        return floor;
    }

    private void Free(CompletedOffsetNode cursor)
    {
        Count--;
        cursor.Owner!.RemoveRange(cursor);
    }

    private static CompletedOffsetNode Insert(CompletedOffsetNode? root, CompletedOffsetNode node)
    {
        if (root is null)
            return node;
        if (node.Start < root.Start)
            root.Left = Insert(root.Left, node);
        else
            root.Right = Insert(root.Right, node);
        return Balance(root);
    }

    private CompletedOffsetNode? Remove(CompletedOffsetNode root, long start)
    {
        if (start < root.Start)
            root.Left = Remove(root.Left!, start);
        else if (start > root.Start)
            root.Right = Remove(root.Right!, start);
        else
        {
            if (root.Left is null || root.Right is null)
            {
                var child = root.Left ?? root.Right;
                Free(root);
                return child;
            }
            var successor = root.Right;
            while (successor.Left is not null)
                successor = successor.Left;
            root.Start = successor.Start;
            root.End = successor.End;
            root.Epoch = successor.Epoch;
            root.Right = Remove(root.Right, successor.Start);
        }
        return Balance(root);
    }

    private static int Height(CompletedOffsetNode? node) => node is null ? 0 : node.Height;

    private static void UpdateHeight(CompletedOffsetNode node) =>
        node.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));

    private static CompletedOffsetNode Balance(CompletedOffsetNode node)
    {
        UpdateHeight(node);
        var left = node.Left;
        var right = node.Right;
        var balance = Height(left) - Height(right);
        if (balance > 1)
        {
            if (Height(left!.Left) < Height(left!.Right))
                node.Left = RotateLeft(left!);
            return RotateRight(node);
        }
        if (balance < -1)
        {
            if (Height(right!.Right) < Height(right!.Left))
                node.Right = RotateRight(right!);
            return RotateLeft(node);
        }
        return node;
    }

    private static CompletedOffsetNode RotateLeft(CompletedOffsetNode node)
    {
        var pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;
        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }

    private static CompletedOffsetNode RotateRight(CompletedOffsetNode node)
    {
        var pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;
        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }
}
