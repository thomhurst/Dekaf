---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-13 22:20 UTC

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
  ProducerBenchmarks.Confluent_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-SCGMIK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.179 s** | **0.0101 s** | **0.0016 s** |  **1.00** |    **0.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.177 s** | **0.0064 s** | **0.0010 s** |  **1.00** |    **0.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.178 s** | **0.0043 s** | **0.0011 s** |  **1.00** |    **0.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.178 s** | **0.0045 s** | **0.0007 s** |  **1.00** |    **0.00** | **2425024 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.166 s** | **0.0089 s** | **0.0014 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.166 s** | **0.0067 s** | **0.0010 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.166 s** | **0.0031 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.166 s** | **0.0075 s** | **0.0020 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-MQWIXM(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 21.489 μs | 11.3435 μs | 6.7503 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.392 μs |  0.2532 μs | 0.1507 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.983 μs |  0.4989 μs | 0.2969 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.458 μs | 12.2534 μs | 7.2918 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 23.003 μs | 11.6892 μs | 6.9560 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.181 μs |  9.7028 μs | 5.7740 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.684 μs |  0.3883 μs | 0.2311 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  5.529 μs |  0.2401 μs | 0.1588 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,370.4 ns |    79.89 ns |    47.54 ns |  0.34 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,582.9 ns |   174.66 ns |    91.35 ns |  0.39 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  2,020.5 ns |   153.49 ns |   101.52 ns |  0.50 |    0.03 |         - |          NA |
| &#39;Deserialize String&#39;                 |  4,218.8 ns | 2,063.88 ns | 1,365.13 ns |  1.04 |    0.32 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    687.7 ns |    73.73 ns |    43.87 ns |  0.17 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,039.2 ns |   624.05 ns |   412.77 ns |  8.64 |    0.17 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,057.0 ns |   113.43 ns |    67.50 ns |  1.00 |    0.02 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,609.2 ns |   581.82 ns |   384.84 ns |  0.89 |    0.09 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.536 μs |  1.8026 μs |  1.0727 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   546.837 μs | 28.2925 μs | 18.7137 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.936 μs |  0.3088 μs |  0.2043 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,621.919 μs | 36.1150 μs | 23.8878 μs |         - |


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