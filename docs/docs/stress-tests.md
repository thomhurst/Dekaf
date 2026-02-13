---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-02-13 22:10 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 411,329 | 392.27 | 370,272,677 | 34431510 |
| Dekaf | 402,501 | 383.85 | 362,486,886 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer performance.
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 386,972 | 369.04 | 348,274,880 | 0 |
| Dekaf | 367,651 | 350.62 | 330,886,917 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 85322 | 516 | 8 | 1237.87 GB |
| Confluent | producer | 32717 | 89 | 9 | 507.02 GB |
| Dekaf | consumer | 41048 | 628 | 8 | 638.56 GB |
| Dekaf | producer | 65 | 11 | 3 | 1.63 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
