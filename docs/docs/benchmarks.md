---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet.
**Ratio &lt; 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Results

Benchmark results will appear here automatically after the next benchmark workflow run.

To run benchmarks locally:

```bash
dotnet run --project tools/Dekaf.Benchmarks --configuration Release -- --filter "*"
```

---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to baseline (Confluent.Kafka)
  - `< 1.0` = Dekaf is faster
  - `> 1.0` = Confluent is faster
  - `1.0` = Same performance
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*
