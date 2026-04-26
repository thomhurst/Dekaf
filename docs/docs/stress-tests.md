---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-04-26 03:38 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 402,595 | 383.94 | 363,227,528 | 0 |
| Confluent | 402,576 | 383.93 | 362,364,136 | 27559429 |
| Dekaf | 402,209 | 383.58 | 363,071,556 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 220,006 | 209.81 | 199,409,714 | 0 |
| Confluent | 202,121 | 192.76 | 182,039,051 | 52974151 |

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,522 | 383.88 | 362,302,113 | 25872823 |
| Dekaf | 402,432 | 383.79 | 363,355,944 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 133,857 | 127.66 | 120,586,300 | 88134146 |
| Dekaf | 132,351 | 126.22 | 120,212,901 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,561 | 383.91 | 362,347,229 | 32417775 |
| Dekaf (3conn) | 402,515 | 383.87 | 363,374,780 | 0 |
| Dekaf | 402,225 | 383.59 | 363,042,913 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 132,855 | 126.70 | 120,637,653 | 0 |
| Dekaf | 132,497 | 126.36 | 120,338,986 | 0 |
| Confluent | 132,441 | 126.31 | 119,209,629 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 105,335 | 100.46 | 94,800,623 | 0 |
| Dekaf | 97,051 | 92.55 | 87,345,000 | 0 |

:::note
Confluent.Kafka is 1.09x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 73241 | 14 | 14 | 683.88 GB |
| Confluent | Producer (Fire-and-Forget) | 102759 | 1 | 1 | 484.99 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 64759 | 21 | 20 | 312.09 GB |
| Confluent | producer-acks-all | 96656 | 1 | 1 | 481.84 GB |
| Confluent | producer-acks-all, 3 Brokers | 91721 | 34 | 23 | 301.32 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 103156 | 30 | 13 | 486.88 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 19845 | 20 | 16 | 143.01 GB |
| Dekaf | Consumer | 34627 | 14 | 5 | 160.74 GB |
| Dekaf | Producer (Fire-and-Forget) | 207 | 23 | 16 | 3.70 GB |
| Dekaf | producer-acks-all | 191 | 18 | 17 | 3.16 GB |
| Dekaf | producer-acks-all, 3 Brokers | 57 | 17 | 15 | 18.31 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 215 | 24 | 21 | 4.56 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 62 | 18 | 16 | 17.89 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 61 | 16 | 15 | 6.91 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 44 | 15 | 13 | 14.05 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 71 | 15 | 14 | 4.57 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 40 | 13 | 12 | 16.05 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
