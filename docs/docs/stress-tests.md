---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-03-30 01:08 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,729 | 384.07 | 362,512,862 | 29662814 |
| Dekaf | 402,108 | 383.48 | 362,144,784 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 196,135 | 187.05 | 176,665,451 | 52675978 |
| Dekaf | 161,119 | 153.65 | 145,796,186 | 0 |

:::note
Confluent.Kafka is 1.22x faster for producer (fire-and-forget), 3 brokers throughput.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 402,596 | 383.95 | 362,501,134 | 0 |
| Confluent | 402,335 | 383.70 | 362,148,629 | 27531626 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 132,642 | 126.50 | 119,378,498 | 0 |
| Dekaf | 132,285 | 126.16 | 119,327,800 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 102,230 | 97.49 | 92,006,289 | 0 |
| Dekaf | 96,947 | 92.46 | 87,252,000 | 0 |

:::note
Confluent.Kafka is 1.05x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | consumer | 72326 | 10 | 10 | 674.76 GB |
| Confluent | producer | 102289 | 1 | 1 | 489.00 GB |
| Confluent | producer | 64855 | 2 | 2 | 308.08 GB |
| Confluent | producer-idempotent | 101133 | 1 | 1 | 484.69 GB |
| Confluent | producer-idempotent | 15621 | 15 | 12 | 143.21 GB |
| Dekaf | consumer | 34880 | 27 | 9 | 163.57 GB |
| Dekaf | producer | 111 | 35 | 16 | 1.67 GB |
| Dekaf | producer | 173 | 65 | 32 | 18.80 GB |
| Dekaf | producer-idempotent | 589 | 546 | 544 | 226.03 GB |
| Dekaf | producer-idempotent | 114 | 29 | 16 | 9.80 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
