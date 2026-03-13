---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-03-13 21:58 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 413,336 | 394.19 | 372,053,909 | 35964464 |
| Dekaf | 401,930 | 383.31 | 361,930,326 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer performance.
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 397,096 | 378.70 | 357,386,236 | 0 |
| Dekaf | 322,865 | 307.91 | 290,578,839 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 86789 | 533 | 8 | 1261.85 GB |
| Confluent | producer | 33033 | 89 | 9 | 511.96 GB |
| Dekaf | consumer | 53627 | 27310 | 1417 | 796.54 GB |
| Dekaf | producer | 16975 | 16975 | 2122 | 231.81 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
