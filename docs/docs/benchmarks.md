---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-14 13:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----:|------:|------:|--------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |   NA |    NA |     ? |       ? |           ? |
|                         |               |             |           |      |       |       |         |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-BKUREU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.180 s** | **0.0051 s** | **0.0013 s** |  **1.00** |    **0.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.178 s** | **0.0045 s** | **0.0012 s** |  **1.00** |    **0.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.177 s** | **0.0039 s** | **0.0006 s** |  **1.00** |    **0.00** |  **616432 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.177 s** | **0.0056 s** | **0.0015 s** |  **1.00** |    **0.00** | **2425024 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.167 s** | **0.0062 s** | **0.0016 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.166 s** | **0.0030 s** | **0.0008 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.165 s** | **0.0028 s** | **0.0007 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.166 s** | **0.0037 s** | **0.0006 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-PYWHLC(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.468 μs |  8.8978 μs | 5.2949 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.239 μs |  0.2707 μs | 0.1611 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.834 μs |  0.2994 μs | 0.1566 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.133 μs | 12.1617 μs | 7.2372 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 15.004 μs |  9.9792 μs | 5.9384 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.106 μs |  9.1562 μs | 5.4487 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.411 μs |  0.8332 μs | 0.4958 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.448 μs |  0.2907 μs | 0.1520 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,325.8 ns |    45.49 ns |  27.07 ns |  0.31 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,554.6 ns |   330.18 ns | 196.48 ns |  0.36 |    0.05 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,768.3 ns |   320.91 ns | 212.26 ns |  0.41 |    0.06 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,679.8 ns |   115.41 ns |  68.68 ns |  0.86 |    0.08 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    625.8 ns |    50.27 ns |  33.25 ns |  0.15 |    0.02 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,986.9 ns | 1,046.88 ns | 622.98 ns |  7.70 |    0.72 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,325.9 ns |   692.19 ns | 457.84 ns |  1.01 |    0.14 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,815.8 ns |   738.40 ns | 488.41 ns |  0.89 |    0.14 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Allocated |
|------------------------ |-------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.335 μs |   1.6050 μs |   1.0616 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   834.098 μs | 303.4943 μs | 200.7427 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.162 μs |   0.9563 μs |   0.5691 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,976.928 μs |  46.8728 μs |  31.0035 μs |         - |


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