---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-06-28 03:40 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 402,640 | 383.99 | 363,516,056 | 0 |
| Confluent | 402,574 | 383.92 | 362,433,428 | 36989623 |
| Dekaf | 402,333 | 383.69 | 363,276,463 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 287,053 | 273.75 | 259,340,802 | 0 |
| Dekaf | 216,663 | 206.63 | 195,557,144 | 0 |
| Confluent | 204,616 | 195.14 | 184,286,676 | 56648116 |

:::tip
**Dekaf is 1.06x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,705 | 384.05 | 362,472,150 | 34539425 |
| Dekaf | 402,448 | 383.80 | 363,256,665 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 134,240 | 128.02 | 120,869,932 | 79528780 |
| Dekaf | 132,832 | 126.68 | 120,681,518 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,501 | 383.85 | 362,285,057 | 30153649 |
| Dekaf (3conn) | 402,297 | 383.66 | 363,012,413 | 0 |
| Dekaf | 265,434 | 253.14 | 240,001,906 | 0 |

:::note
Confluent.Kafka is 1.52x faster for producer (fire-and-forget, idempotent) throughput.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 133,917 | 127.71 | 121,683,855 | 0 |
| Dekaf (3conn) | 133,811 | 127.61 | 121,535,007 | 0 |
| Confluent | 133,018 | 126.86 | 119,716,821 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 106,244 | 101.32 | 95,618,406 | 0 |
| Dekaf | 96,720 | 92.24 | 87,047,000 | 0 |

:::note
Confluent.Kafka is 1.10x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 74493 | 73 | 58 | 694.65 GB |
| Confluent | Producer (Fire-and-Forget) | 104168 | 1 | 1 | 502.29 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 68389 | 5 | 5 | 324.48 GB |
| Confluent | producer-acks-all | 105287 | 1 | 1 | 497.87 GB |
| Confluent | producer-acks-all, 3 Brokers | 60558 | 11 | 11 | 285.32 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 103438 | 1 | 1 | 489.64 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 20225 | 10 | 8 | 143.62 GB |
| Dekaf | Consumer | 34896 | 15 | 4 | 162.65 GB |
| Dekaf | Producer (Fire-and-Forget) | 218 | 19 | 17 | 3.97 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 65 | 19 | 16 | 23.99 GB |
| Dekaf | producer-acks-all | 165 | 17 | 15 | 2.69 GB |
| Dekaf | producer-acks-all, 3 Brokers | 54 | 18 | 16 | 18.09 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 124 | 19 | 18 | 2.73 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 53 | 17 | 15 | 16.15 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 67 | 15 | 13 | 6.31 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 57 | 17 | 16 | 18.35 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 80 | 16 | 15 | 5.84 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 40 | 18 | 16 | 15.46 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
