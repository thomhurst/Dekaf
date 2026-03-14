---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-03-14 01:36 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 428,670 | 408.81 | 385,863,176 | 41387715 |
| Dekaf | 418,580 | 399.19 | 376,931,918 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer performance.
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 400,157 | 381.62 | 360,142,365 | 0 |
| Confluent | 399,731 | 381.21 | 359,758,881 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 59326 | 355 | 8 | 1292.14 GB |
| Confluent | producer | 23134 | 89 | 9 | 538.42 GB |
| Dekaf | consumer | 47635 | 3678 | 11 | 741.16 GB |
| Dekaf | producer | 90 | 12 | 4 | 2.06 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
