namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded Reservoir-backed pool for <see cref="PendingAppend"/> instances.
/// </summary>
internal sealed class PendingAppendPool(int maxPoolSize) : ObjectPool<PendingAppend>(maxPoolSize)
{
    protected override PendingAppend Create() => new();

    protected override void Reset(PendingAppend item) { }
}
