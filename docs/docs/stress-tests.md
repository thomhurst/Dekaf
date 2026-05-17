---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-05-17 03:34 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,558 | 383.91 | 362,437,736 | 35465417 |
| Dekaf | 402,491 | 383.85 | 363,411,330 | 0 |
| Dekaf (3conn) | 402,332 | 383.69 | 363,175,641 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 266,415 | 254.07 | 241,441,150 | 0 |
| Dekaf (3conn) | 259,614 | 247.59 | 234,619,915 | 0 |
| Confluent | 205,947 | 196.41 | 185,451,098 | 61309083 |

:::tip
**Dekaf is 1.29x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,634 | 383.98 | 362,499,766 | 27995749 |
| Dekaf | 402,551 | 383.90 | 363,365,952 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 134,173 | 127.96 | 120,845,280 | 79173700 |
| Dekaf | 133,185 | 127.02 | 120,975,980 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,660 | 384.01 | 362,404,236 | 32812765 |
| Dekaf | 402,534 | 383.89 | 363,380,567 | 0 |
| Dekaf (3conn) | 402,505 | 383.86 | 363,289,732 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 133,420 | 127.24 | 120,080,952 | 0 |
| Dekaf | 133,388 | 127.21 | 121,167,966 | 0 |
| Dekaf (3conn) | 132,887 | 126.73 | 120,712,258 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 108,724 | 103.69 | 97,850,484 | 0 |
| Dekaf | 97,359 | 92.85 | 87,623,000 | 0 |

:::note
Confluent.Kafka is 1.12x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 75399 | 14 | 14 | 705.88 GB |
| Confluent | Producer (Fire-and-Forget) | 104284 | 6 | 6 | 496.01 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 67623 | 42 | 35 | 321.42 GB |
| Confluent | producer-acks-all | 101864 | 4 | 4 | 484.18 GB |
| Confluent | producer-acks-all, 3 Brokers | 60726 | 54 | 44 | 275.74 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 103378 | 4 | 4 | 491.94 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 17706 | 10 | 8 | 144.05 GB |
| Dekaf | Consumer | 32944 | 16 | 5 | 163.59 GB |
| Dekaf | Producer (Fire-and-Forget) | 218 | 19 | 17 | 3.75 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 64 | 22 | 21 | 26.15 GB |
| Dekaf | producer-acks-all | 177 | 18 | 17 | 2.65 GB |
| Dekaf | producer-acks-all, 3 Brokers | 52 | 17 | 15 | 21.42 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 157 | 17 | 16 | 3.30 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 53 | 17 | 15 | 18.50 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 66 | 14 | 13 | 6.92 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 45 | 18 | 17 | 17.36 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 71 | 15 | 14 | 5.89 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 46 | 18 | 16 | 19.13 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
