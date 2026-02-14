---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-02-14 00:56 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 410,407 | 391.39 | 369,442,636 | 33242753 |
| Dekaf | 402,787 | 384.13 | 362,798,702 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer performance.
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 390,201 | 372.12 | 351,180,810 | 0 |
| Dekaf | 351,610 | 335.32 | 316,448,890 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 85606 | 515 | 8 | 1242.05 GB |
| Confluent | producer | 32513 | 89 | 9 | 503.86 GB |
| Dekaf | consumer | 39269 | 673 | 8 | 610.86 GB |
| Dekaf | producer | 53 | 12 | 4 | 1.82 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
