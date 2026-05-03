---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-05-03 03:38 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,462 | 383.82 | 362,301,038 | 27871713 |
| Dekaf | 402,396 | 383.75 | 363,259,505 | 0 |
| Dekaf (3conn) | 402,370 | 383.73 | 363,145,014 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 227,295 | 216.77 | 206,532,643 | 0 |
| Dekaf | 214,930 | 204.97 | 195,410,430 | 0 |
| Confluent | 200,961 | 191.65 | 180,968,461 | 58771929 |

:::tip
**Dekaf is 1.07x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 402,606 | 383.95 | 362,414,177 | 29604012 |
| Dekaf | 402,243 | 383.61 | 363,188,442 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 133,822 | 127.62 | 120,472,853 | 78168618 |
| Dekaf | 132,399 | 126.27 | 119,610,486 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf (3conn) | 402,545 | 383.90 | 363,401,001 | 0 |
| Dekaf | 402,423 | 383.78 | 363,290,190 | 0 |
| Confluent | 402,394 | 383.75 | 362,222,373 | 23738683 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Dekaf | 133,366 | 127.19 | 121,110,243 | 0 |
| Confluent | 132,714 | 126.57 | 119,444,267 | 17097 |
| Dekaf (3conn) | 132,655 | 126.51 | 120,519,325 | 0 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent), 3 brokers performance.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors |
|--------|--------------|--------|-------|--------|
| Confluent | 104,989 | 100.12 | 94,489,282 | 0 |
| Dekaf | 97,392 | 92.88 | 87,652,000 | 0 |

:::note
Confluent.Kafka is 1.08x faster for consumer throughput.
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 72498 | 32 | 20 | 675.95 GB |
| Confluent | Producer (Fire-and-Forget) | 99233 | 1 | 1 | 485.49 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 67374 | 12 | 12 | 322.73 GB |
| Confluent | producer-acks-all | 102360 | 6 | 6 | 484.65 GB |
| Confluent | producer-acks-all, 3 Brokers | 61188 | 49 | 37 | 282.49 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 99746 | 8 | 8 | 472.32 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 20272 | 19 | 16 | 143.32 GB |
| Dekaf | Consumer | 27225 | 14 | 5 | 163.87 GB |
| Dekaf | Producer (Fire-and-Forget) | 199 | 25 | 19 | 3.65 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 51 | 17 | 16 | 20.56 GB |
| Dekaf | producer-acks-all | 214 | 18 | 15 | 3.22 GB |
| Dekaf | producer-acks-all, 3 Brokers | 51 | 18 | 16 | 20.42 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 243 | 42 | 40 | 10.55 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 57 | 13 | 12 | 17.84 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 87 | 17 | 15 | 4.12 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 43 | 12 | 11 | 9.30 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 89 | 18 | 16 | 4.13 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 40 | 16 | 14 | 16.03 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
