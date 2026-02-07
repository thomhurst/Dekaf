---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-02-07 15:01 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 496,381 | 473.39 | 446,951,070 | 0 |
| Confluent | 447,571 | 426.84 | 402,894,389 | 40039358 |

:::tip
**Dekaf is 1.11x faster** than Confluent.Kafka for producer throughput!
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 420,008 | 400.55 | 378,007,491 | 0 |
| Confluent | 390,547 | 372.45 | 351,492,347 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 85575 | 516 | 8 | 1244.19 GB |
| Confluent | producer | 23904 | 89 | 9 | 556.39 GB |
| Dekaf | consumer | 46858 | 1213 | 9 | 729.11 GB |
| Dekaf | producer | 53 | 17 | 4 | 1.10 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
