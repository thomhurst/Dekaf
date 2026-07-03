---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 01:30 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 402,650 | 384.00 | 363,528,986 | 0 |
| Confluent | 402,641 | 383.99 | 362,482,932 | 34865627 |
| Dekaf | 402,554 | 383.91 | 363,308,502 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 256,054 | 244.19 | 231,746,499 | 0 |
| Dekaf (3conn) | 254,185 | 242.41 | 231,103,396 | 0 |
| Confluent | 202,682 | 193.29 | 182,555,867 | 56562813 |

:::tip
**Dekaf is 1.26x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 402,579 | 383.93 | 363,439,764 | 0 |
| Confluent | 402,555 | 383.91 | 362,417,571 | 27538522 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 134,041 | 127.83 | 120,700,874 | 77092463 |
| Dekaf | 132,835 | 126.68 | 120,640,659 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 402,784 | 384.12 | 363,572,995 | 0 |
| Dekaf | 402,379 | 383.74 | 363,201,949 | 0 |
| Confluent | 402,375 | 383.73 | 362,170,371 | 29637011 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 133,523 | 127.34 | 121,310,526 | 0 |
| Confluent | 133,349 | 127.17 | 120,038,711 | 0 |
| Dekaf (3conn) | 69,607 | 66.38 | 62,654,440 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 98,391 | 93.83 | 88,557,485 | 0 |
| Dekaf | 97,955 | 93.42 | 88,159,000 | 0 |

:::note
Dekaf and Confluent.Kafka have similar consumer performance.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 72592 | 88 | 68 | 675.95 GB |
| Confluent | Producer (Fire-and-Forget) | 105026 | 1 | 1 | 498.48 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 68322 | 18 | 18 | 318.97 GB |
| Confluent | producer-acks-all | 101891 | 1 | 1 | 485.02 GB |
| Confluent | producer-acks-all, 3 Brokers | 61473 | 12 | 12 | 282.58 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 102709 | 1 | 1 | 488.56 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 23071 | 43 | 39 | 144.00 GB |
| Dekaf | Consumer | 35446 | 23 | 9 | 164.57 GB |
| Dekaf | Producer (Fire-and-Forget) | 144 | 20 | 17 | 3.16 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 63 | 22 | 21 | 23.98 GB |
| Dekaf | producer-acks-all | 162 | 20 | 18 | 3.51 GB |
| Dekaf | producer-acks-all, 3 Brokers | 57 | 17 | 15 | 16.84 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 137 | 25 | 23 | 16.48 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 47 | 18 | 16 | 17.18 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 36 | 12 | 11 | 6.07 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 43 | 16 | 15 | 10.25 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 51 | 15 | 14 | 6.00 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 27 | 19 | 17 | 30.35 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
