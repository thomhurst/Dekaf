# Dekaf Stress Test Results

**Run Date:** 2026-02-03 10:41:23 UTC
**Machine:** tom-longhurst-Intel-Z690 (20 processors)
**Total Duration:** 7.0 minutes

## Producer Throughput (7 minutes, 1000B messages)

| Client    | Messages/sec | MB/sec | Errors | Ratio |
|-----------|--------------|--------|--------|-------|
| Dekaf     |       81,788 |  78.00 |      0 | 1.00x |

## Latency Percentiles

| Client    | p50    | p95    | p99    | Max    |
|-----------|--------|--------|--------|--------|
| Dekaf     | 0.50ms | 0.50ms | 0.50ms | 39ms |

## GC Statistics

| Client    | Scenario | Gen0 | Gen1 | Gen2 | Allocated |
|-----------|----------|------|------|------|-----------|
| Dekaf     | producer | 4466 | 1309 |   43 | 267.46 MB |

