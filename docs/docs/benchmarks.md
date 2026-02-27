---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-27 19:30 UTC

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
  ProducerBenchmarks.Confluent_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_FireAndForget: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Dekaf_ProduceSingle: Job-VDSHUR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.170 s** | **0.0073 s** | **0.0011 s** |  **1.00** |    **0.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.167 s** | **0.0056 s** | **0.0009 s** |  **1.00** |    **0.00** |  **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.168 s** | **0.0059 s** | **0.0015 s** |  **1.00** |    **0.00** |  **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.168 s** | **0.0047 s** | **0.0012 s** |  **1.00** |    **0.00** | **2424896 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.162 s** | **0.0051 s** | **0.0008 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.161 s** | **0.0041 s** | **0.0011 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.162 s** | **0.0020 s** | **0.0005 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |      NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                      |            |              |             |         |          |          |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.160 s** | **0.0039 s** | **0.0010 s** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |      NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_ConsumeAll: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=100, MessageSize=1000]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=100]
  ConsumerBenchmarks.Dekaf_PollSingle: Job-VTGNCZ(InvocationCount=1, IterationCount=5, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=2) [MessageCount=1000, MessageSize=1000]


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 35.194 μs | 16.5521 μs | 9.8499 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.081 μs |  0.2662 μs | 0.1584 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.849 μs |  0.2186 μs | 0.1301 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.433 μs | 12.2684 μs | 7.3007 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 15.193 μs |  9.8749 μs | 5.8764 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 24.895 μs |  9.2132 μs | 5.4826 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.810 μs |  0.1864 μs | 0.1110 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.351 μs |  0.0589 μs | 0.0308 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,247.4 ns |  20.53 ns |  12.22 ns |  0.33 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,700.9 ns | 153.62 ns | 101.61 ns |  0.44 |    0.03 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,816.2 ns | 169.24 ns | 100.71 ns |  0.47 |    0.03 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,813.9 ns |  51.98 ns |  30.93 ns |  0.73 |    0.01 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    645.2 ns |  37.04 ns |  22.04 ns |  0.17 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,021.2 ns | 592.82 ns | 392.11 ns |  8.88 |    0.18 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,831.6 ns | 113.85 ns |  67.75 ns |  1.00 |    0.02 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,404.9 ns |  84.73 ns |  56.04 ns |  0.89 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.215 μs |  1.4873 μs |  0.9837 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   545.766 μs | 28.0401 μs | 18.5468 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.758 μs |  0.3728 μs |  0.2466 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,615.658 μs | 52.9051 μs | 34.9934 μs |         - |


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