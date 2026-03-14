---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-03-14 10:43 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 415,225 | 395.99 | 373,984,174 | 0 |
| Confluent | 404,761 | 386.01 | 364,444,625 | 26330231 |

:::note
Dekaf and Confluent.Kafka have similar producer performance.
:::

## Consumer Performance (15 min, 1000B)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 401,239 | 382.65 | 361,115,941 | 0 |
| Confluent | 343,286 | 327.38 | 308,958,699 | 0 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 79080 | 452 | 8 | 1151.53 GB |
| Confluent | producer-idempotent | 20865 | 89 | 9 | 485.24 GB |
| Dekaf | consumer | 47759 | 3741 | 10 | 743.16 GB |
| Dekaf | producer-idempotent | 91 | 11 | 4 | 2.06 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
