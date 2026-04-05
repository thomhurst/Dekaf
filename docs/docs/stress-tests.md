---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-04-05 03:34 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,563 | 383.91 | 362,449,922 | 27883053 |
| Dekaf | 402,122 | 383.49 | 362,147,052 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 198,660 | 189.46 | 178,865,464 | 54901834 |
| Dekaf | 145,447 | 138.71 | 131,607,797 | 0 |

:::note
Confluent.Kafka is 1.37x faster for producer (fire-and-forget), 3 brokers throughput.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,511 | 383.86 | 362,300,374 | 31870657 |
| Dekaf | 402,151 | 383.52 | 362,175,466 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 133,232 | 127.06 | 120,208,660 | 0 |
| Confluent | 132,989 | 126.83 | 119,736,649 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 105,850 | 100.95 | 95,273,126 | 0 |
| Dekaf | 97,314 | 92.81 | 87,582,000 | 0 |

:::note
Confluent.Kafka is 1.09x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 72672 | 0 | 0 | 679.15 GB |
| Confluent | Producer (Fire-and-Forget) | 102461 | 1 | 1 | 485.69 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 60734 | 67 | 58 | 287.29 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 104180 | 4 | 4 | 492.45 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 24127 | 14 | 13 | 57.66 GB |
| Dekaf | Consumer | 58770 | 37 | 9 | 161.88 GB |
| Dekaf | Producer (Fire-and-Forget) | 398 | 38 | 37 | 15.11 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 197 | 40 | 39 | 24.46 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 1455 | 1420 | 1419 | 681.38 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 108 | 22 | 21 | 7.02 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
