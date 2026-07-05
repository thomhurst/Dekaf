using System.Collections.Concurrent;

namespace Dekaf.Networking;

internal sealed class CancelledCorrelationIdTracker
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<int, byte> _ids = new();
    private readonly ConcurrentQueue<int> _order = new();

    public CancelledCorrelationIdTracker(int capacity)
    {
        CompatibilityThrowHelpers.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public bool TryAdd(int correlationId)
    {
        if (!_ids.TryAdd(correlationId, 0))
            return false;

        _order.Enqueue(correlationId);
        Trim();
        return true;
    }

    public bool TryRemove(int correlationId)
        => _ids.TryRemove(correlationId, out _);

    internal int QueuedCount => _order.Count;

    public void Clear()
    {
        _ids.Clear();

        while (_order.TryDequeue(out _))
        {
        }
    }

    private void Trim()
    {
        while (_order.Count > _capacity && _order.TryDequeue(out var oldestCorrelationId))
        {
            _ids.TryRemove(oldestCorrelationId, out _);
        }
    }
}
