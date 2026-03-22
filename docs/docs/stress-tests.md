---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-03-22 15:32 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 484,477 | 462.03 | 436,245,160 | 0 |
| Dekaf | 420,714 | 401.22 | 378,895,243 | 0 |
| Confluent | 409,736 | 390.76 | 368,828,051 | 23463792 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 348,113 | 331.99 | 314,048,123 | 0 |
| Dekaf | 264,414 | 252.16 | 238,242,092 | 0 |
| Confluent | 249,817 | 238.24 | 224,914,141 | 39220767 |

:::tip
**Dekaf is 1.06x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 423,735 | 404.11 | 381,563,485 | 0 |
| Dekaf | 421,943 | 402.40 | 379,969,652 | 0 |
| Confluent | 401,292 | 382.70 | 361,245,980 | 14909377 |

:::tip
**Dekaf is 1.05x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent) throughput!
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 133,937 | 127.73 | 120,828,839 | 0 |
| Confluent | 131,589 | 125.49 | 118,452,533 | 0 |
| Dekaf | 131,479 | 125.39 | 118,600,816 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 402,578 | 383.93 | 362,319,796 | 0 |
| Confluent | 168,806 | 160.99 | 151,925,893 | 0 |

:::tip
**Dekaf is 2.38x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 85528 | 4 | 4 | 793.84 GB |
| Confluent | producer | 165169 | 0 | 0 | 485.34 GB |
| Confluent | producer | 71433 | 11 | 11 | 340.98 GB |
| Confluent | producer-idempotent | 16408 | 8 | 7 | 142.10 GB |
| Confluent | producer-idempotent | 151464 | 0 | 0 | 460.62 GB |
| Dekaf | consumer | 74365 | 1 | 1 | 744.93 GB |
| Dekaf | producer | 62 | 6 | 5 | 706.26 MB |
| Dekaf | producer | 34 | 7 | 6 | 2.27 GB |
| Dekaf | producer-idempotent | 53 | 5 | 4 | 1.01 GB |
| Dekaf | producer-idempotent | 27 | 7 | 6 | 6.25 GB |
| Dekaf (3conn) | producer | 71 | 4 | 4 | 406.64 MB |
| Dekaf (3conn) | producer | 49 | 3 | 3 | 1.43 GB |
| Dekaf (3conn) | producer-idempotent | 55 | 3 | 3 | 1.05 GB |
| Dekaf (3conn) | producer-idempotent | 30 | 4 | 3 | 731.83 MB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
