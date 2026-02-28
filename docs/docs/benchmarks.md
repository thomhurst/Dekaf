---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-28 14:15 UTC

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
  ProducerBenchmarks.Confluent_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-QHQFNP(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.168 s** | **0.0037 s** | **0.0010 s** |  **1.00** |    **0.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.167 s** | **0.0054 s** | **0.0008 s** |  **1.00** |    **0.00** |  **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.167 s** | **0.0012 s** | **0.0002 s** |  **1.00** |    **0.00** |  **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.167 s** | **0.0035 s** | **0.0009 s** |  **1.00** |    **0.00** | **2423936 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.162 s** | **0.0018 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.161 s** | **0.0021 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.160 s** | **0.0026 s** | **0.0004 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.160 s** | **0.0048 s** | **0.0013 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-SNCSJJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Median    | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.540 μs |  9.0769 μs |  5.4015 μs | 22.066 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.073 μs |  1.5084 μs |  0.9977 μs | 13.249 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.966 μs |  0.1765 μs |  0.1167 μs | 10.986 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 37.376 μs | 15.2806 μs |  9.0932 μs | 40.716 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 25.572 μs | 19.7562 μs | 11.7566 μs | 30.857 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.073 μs |  9.4617 μs |  5.6305 μs | 26.841 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.838 μs |  0.2511 μs |  0.1313 μs | 16.842 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.421 μs |  0.1078 μs |  0.0713 μs |  4.443 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,261.9 ns | 150.19 ns |  99.34 ns |  0.31 |    0.02 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,612.7 ns | 296.82 ns | 176.63 ns |  0.39 |    0.04 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,445.8 ns | 117.74 ns |  70.07 ns |  0.35 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,900.9 ns | 227.30 ns | 118.88 ns |  0.71 |    0.03 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    649.0 ns |  80.65 ns |  53.34 ns |  0.16 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 33,649.7 ns | 634.71 ns | 419.82 ns |  8.23 |    0.20 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,092.0 ns | 158.26 ns |  94.18 ns |  1.00 |    0.03 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,394.5 ns | 119.30 ns |  62.40 ns |  0.83 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Allocated |
|------------------------ |-------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.649 μs |   0.5751 μs |   0.3804 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   850.018 μs | 282.1239 μs | 186.6075 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.386 μs |   1.6460 μs |   1.0887 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 3,404.548 μs | 162.6907 μs | 107.6098 μs |         - |


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