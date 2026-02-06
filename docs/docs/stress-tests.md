---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered.
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Results

Stress test results will appear here automatically after the next workflow run.

To run stress tests locally:

```bash
dotnet run --project tools/Dekaf.StressTests --configuration Release -- \
  --duration 15 \
  --message-size 1000 \
  --scenario all \
  --client all
```

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
