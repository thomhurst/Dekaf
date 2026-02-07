---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-02-07 17:11 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 489,107 | 466.45 | 440,414,121 | 0 |
| Confluent | 424,193 | 404.54 | 381,837,647 | 39377825 |

:::tip
**Dekaf is 1.15x faster** than Confluent.Kafka for producer throughput!
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 397,830 | 379.40 | 358,047,348 | 0 |
| Dekaf | 376,566 | 359.12 | 338,910,174 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 86883 | 521 | 8 | 1264.65 GB |
| Confluent | producer | 34209 | 89 | 9 | 529.92 GB |
| Dekaf | consumer | 42021 | 909 | 8 | 653.83 GB |
| Dekaf | producer | 52 | 16 | 4 | 1.07 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
