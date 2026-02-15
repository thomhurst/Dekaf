# Adaptive Micro-Linger for Awaited Produces

**Issue:** [#265](https://github.com/thomhurst/Dekaf/issues/265)
**Date:** 2026-02-15

## Problem

`ShouldFlush()` returns `true` immediately when a batch contains any awaited produces (`_completionSourceCount > 0`). Under high throughput with mixed `Send()`/`ProduceAsync()` workloads, this produces many small batches when waiting a fraction of a millisecond would let co-temporal messages accumulate into fuller batches.

## Design

### Approach: Micro-Linger with Existing Timer

Instead of flushing immediately, awaited batches wait a small fraction of the configured `LingerMs`. This piggybacks on the existing 1ms `PeriodicTimer` tick — no new timers or threads.

- `LingerMs = 0` (default): immediate flush, identical to current behavior
- `LingerMs > 0`: awaited batches wait `min(1ms, LingerMs / 10)` before flushing

### Formula

```
awaitedLingerMs = min(1.0, lingerMs / 10.0)
```

| LingerMs | Awaited Linger | Fire-and-Forget Linger |
|----------|---------------|----------------------|
| 0        | 0 (immediate) | 0 (immediate)        |
| 5        | 0.5ms         | 5ms                  |
| 10       | 1ms           | 10ms                 |
| 20       | 1ms (capped)  | 20ms                 |
| 100      | 1ms (capped)  | 100ms                |

### Properties

- **Backwards compatible**: `LingerMs = 0` (the default) preserves exact current behavior
- **No new configuration**: reuses existing `LingerMs` setting
- **No new infrastructure**: existing 1ms timer provides ~1ms resolution, which is sufficient
- **Minimal code change**: ~5 lines of logic in `ShouldFlush()`, one comment update

## Implementation

### Change 1: `ShouldFlush()` in `PartitionBatch`

**File:** `src/Dekaf/Producer/RecordAccumulator.cs` (line ~3884)

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool ShouldFlush(DateTimeOffset now, int lingerMs)
{
    if (Volatile.Read(ref _recordCount) == 0)
        return false;

    var elapsedMs = (now - _createdAt).TotalMilliseconds;

    // Awaited produces: use micro-linger instead of immediate flush.
    // When LingerMs > 0, wait min(1ms, LingerMs/10) to let co-temporal messages batch.
    // When LingerMs == 0, flush immediately (preserves current default behavior).
    if (Volatile.Read(ref _completionSourceCount) > 0)
        return lingerMs == 0 || elapsedMs >= Math.Min(1.0, lingerMs / 10.0);

    return elapsedMs >= lingerMs;
}
```

### Change 2: Comment update in `ExpireLingerAsync()`

**File:** `src/Dekaf/Producer/RecordAccumulator.cs` (line ~2063)

Update the comment that says awaited produces "need immediate flushing" to reflect the new micro-linger behavior.

## Testing

### Unit Tests

Test the `ShouldFlush()` decision matrix:

| LingerMs | Completion Sources | Age (ms) | Expected |
|----------|--------------------|----------|----------|
| 0        | yes                | 0        | true     |
| 5        | yes                | 0        | false    |
| 5        | yes                | 0.5      | true     |
| 20       | yes                | 0.9      | false    |
| 20       | yes                | 1.0      | true     |
| 5        | no                 | 4        | false    |
| 5        | no                 | 5        | true     |

### Integration Test

Produce several `ProduceAsync` messages in rapid succession with `LingerMs = 10`. Verify they arrive in fewer batches than messages sent.

### Benchmark

Compare batch count and throughput before/after with mixed `Send()`/`ProduceAsync()` workload at `LingerMs = 5`.
