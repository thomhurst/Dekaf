---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-18 23:52 UTC

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
  ProducerBenchmarks.Confluent_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-DEJGZV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.167 s** | **0.0051 s** | **0.0008 s** |  **1.00** |    **0.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.165 s** | **0.0024 s** | **0.0006 s** |  **1.00** |    **0.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.165 s** | **0.0015 s** | **0.0002 s** |  **1.00** |    **0.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.166 s** | **0.0022 s** | **0.0006 s** |  **1.00** |    **0.00** | **2425024 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.160 s** | **0.0009 s** | **0.0002 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.161 s** | **0.0019 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.161 s** | **0.0030 s** | **0.0008 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.161 s** | **0.0020 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-TGBDZJ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.421 μs |  8.9640 μs | 5.3343 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 11.090 μs |  2.2211 μs | 1.4691 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.768 μs |  0.3094 μs | 0.1841 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 41.166 μs |  9.7474 μs | 5.0981 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 22.807 μs | 13.9761 μs | 8.3169 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.651 μs | 14.4451 μs | 8.5961 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.906 μs |  0.3456 μs | 0.2056 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.492 μs |  0.0954 μs | 0.0631 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,408.3 ns |  54.76 ns |  36.22 ns |  0.36 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,506.7 ns | 134.39 ns |  79.97 ns |  0.38 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,513.4 ns |  81.89 ns |  54.16 ns |  0.38 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,255.6 ns | 238.45 ns | 157.72 ns |  0.82 |    0.04 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    707.0 ns |  95.33 ns |  49.86 ns |  0.18 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 33,813.5 ns | 408.64 ns | 243.18 ns |  8.54 |    0.23 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,960.1 ns | 187.23 ns | 111.42 ns |  1.00 |    0.04 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,444.3 ns |  57.60 ns |  34.28 ns |  0.87 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev     | Allocated |
|------------------------ |-------------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.386 μs |  1.512 μs |  1.0001 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   540.047 μs | 18.018 μs | 10.7220 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.604 μs |  1.416 μs |  0.9367 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,620.444 μs | 25.332 μs | 13.2490 μs |         - |


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