using System.Diagnostics;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Consumer;

/// <summary>
/// Tracks aggregate raw Fetch response memory across queued and in-flight requests.
/// Memory is reserved, not preallocated.
/// </summary>
internal sealed class FetchBufferMemoryPool : IResponseMemoryPool, IDisposable
{
    private readonly long _limitBytes;
    private readonly long _createdTimestamp = Stopwatch.GetTimestamp();
    private readonly SemaphoreSlim _memoryAvailable = new(0, 1);
    private long _usedBytes;
    private long _depletedStartTimestamp;
    private long _depletedTimestampTicks;
    private int _waiterCount;
    private int _disposed;

    public FetchBufferMemoryPool(long limitBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limitBytes, 1);
        _limitBytes = limitBytes;
    }

    public long LimitBytes => _limitBytes;
    public long UsedBytes => Interlocked.Read(ref _usedBytes);
    public long FreeBytes => Math.Max(0, _limitBytes - UsedBytes);

    public double DepletedPercent
    {
        get
        {
            var elapsed = Stopwatch.GetTimestamp() - _createdTimestamp;
            return elapsed <= 0
                ? 0
                : Math.Min(100, GetDepletedTimestampTicks() * 100d / elapsed);
        }
    }

    public double DepletedDurationSeconds =>
        GetDepletedTimestampTicks() / (double)Stopwatch.Frequency;

    public bool TryReserve(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 1);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        while (true)
        {
            var used = Interlocked.Read(ref _usedBytes);
            // One response larger than the configured pool may proceed only when it is
            // the sole reservation. Kafka permits the first record batch to cross fetch
            // limits so consumers can always make progress.
            if (used != 0 && bytes > _limitBytes - used)
            {
                return false;
            }

            var updated = used + bytes;
            if (updated < used)
                throw new OverflowException("Fetch buffer memory reservation overflowed");

            if (Interlocked.CompareExchange(ref _usedBytes, updated, used) == used)
                return true;
        }
    }

    public ValueTask<IResponseMemoryReservation> ReserveAsync(
        int bytes,
        CancellationToken cancellationToken)
    {
        if (TryReserve(bytes))
            return new ValueTask<IResponseMemoryReservation>(
                FetchBufferMemoryReservation.Create(this, bytes));

        return ReserveSlowAsync(bytes, cancellationToken);
    }

    internal async ValueTask<IResponseMemoryReservation> ReserveSlowAsync(
        int bytes,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _waiterCount);
        try
        {
            // Capacity may have been released after ReserveAsync's fast-path check but
            // before this waiter became visible to Release. Recheck before sleeping so
            // that release cannot be lost in that registration gap.
            if (TryReserve(bytes))
                return FetchBufferMemoryReservation.Create(this, bytes);

            BeginDepletion();
            while (true)
            {
                await _memoryAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (TryReserve(bytes))
                    return FetchBufferMemoryReservation.Create(this, bytes);

                // The awakened waiter may need more memory than another waiter. Pass the
                // edge-triggered signal to an already-queued waiter before waiting again.
                if (Volatile.Read(ref _waiterCount) > 1)
                    SignalMemoryAvailable();
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _waiterCount) == 0)
            {
                EndDepletion();
                if (Volatile.Read(ref _waiterCount) > 0)
                    BeginDepletion();
            }
            else if (Volatile.Read(ref _disposed) != 0 || FreeBytes > 0)
                SignalMemoryAvailable();
        }
    }

    internal void Release(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 1);

        var used = Interlocked.Add(ref _usedBytes, -bytes);
        if (used < 0)
        {
            Interlocked.Add(ref _usedBytes, bytes);
            throw new InvalidOperationException("Fetch buffer memory released more bytes than were reserved");
        }

        if (Volatile.Read(ref _waiterCount) > 0)
            SignalMemoryAvailable();
    }

    private void BeginDepletion()
    {
        if (Volatile.Read(ref _depletedStartTimestamp) != 0)
            return;

        _ = Interlocked.CompareExchange(
            ref _depletedStartTimestamp,
            Stopwatch.GetTimestamp(),
            0);
    }

    private void EndDepletion()
    {
        var started = Interlocked.Exchange(ref _depletedStartTimestamp, 0);
        if (started != 0)
            Interlocked.Add(ref _depletedTimestampTicks, Stopwatch.GetTimestamp() - started);
    }

    private long GetDepletedTimestampTicks()
    {
        var ticks = Interlocked.Read(ref _depletedTimestampTicks);
        var started = Volatile.Read(ref _depletedStartTimestamp);
        return started == 0 ? ticks : ticks + Stopwatch.GetTimestamp() - started;
    }

    private void SignalMemoryAvailable()
    {
        if (_memoryAvailable.CurrentCount != 0)
            return;

        try
        {
            _memoryAvailable.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another release won the edge-triggered signal race.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        EndDepletion();
        SignalMemoryAvailable();
    }
}

/// <summary>
/// Releases a fetch-buffer reservation with its raw response storage.
/// </summary>
internal sealed class FetchBufferMemoryReservation : IResponseMemoryReservation
{
    private static readonly FetchBufferMemoryReservationPool s_pool = new();

    private FetchBufferMemoryPool? _pool;
    private long _reservedBytes;
    private int _disposed;

    private FetchBufferMemoryReservation()
    {
    }

    internal static FetchBufferMemoryReservation Create(
        FetchBufferMemoryPool pool,
        long reservedBytes)
    {
        var owner = s_pool.Rent();
        owner._pool = pool;
        owner._reservedBytes = reservedBytes;
        owner._disposed = 0;
        return owner;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _pool?.Release(_reservedBytes);
        s_pool.Return(this);
    }

    private sealed class FetchBufferMemoryReservationPool()
        : ObjectPool<FetchBufferMemoryReservation>(maxPoolSize: 128)
    {
        protected override FetchBufferMemoryReservation Create() => new();

        protected override void Reset(FetchBufferMemoryReservation item)
        {
            item._pool = null;
            item._reservedBytes = 0;
            item._disposed = 1;
        }
    }
}
