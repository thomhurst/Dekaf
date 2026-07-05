---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 19:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,141.07 μs** |   **111.74 μs** |   **6.125 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,254.49 μs | 1,424.72 μs |  78.094 μs |  0.20 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,409.19 μs** | **1,196.40 μs** |  **65.579 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,325.91 μs |   410.61 μs |  22.507 μs |  0.31 |    0.00 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,310.95 μs** |   **411.07 μs** |  **22.532 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,248.43 μs | 1,911.29 μs | 104.764 μs |  0.20 |    0.01 |        - |       - |   36.26 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,420.94 μs** | **1,656.88 μs** |  **90.819 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,108.88 μs |   711.15 μs |  38.981 μs |  0.49 |    0.00 |  15.6250 |       - |  361.65 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.78 μs** |    **30.56 μs** |   **1.675 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.18 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.87 μs |    18.26 μs |   1.001 μs |  0.51 |    0.01 |        - |       - |    7.34 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,449.18 μs** |   **761.18 μs** |  **41.723 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **425.64 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    603.37 μs |   329.32 μs |  18.051 μs |  0.42 |    0.02 |        - |       - |   70.97 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    196.03 μs |   192.69 μs |  10.562 μs |     ? |       ? |   0.9766 |       - |   100.7 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,960.81 μs | 1,799.06 μs |  98.612 μs |     ? |       ? |   7.8125 |       - |  984.78 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.45 μs** |    **37.79 μs** |   **2.071 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,267.35 μs |   283.00 μs |  15.512 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.98 μs** |    **47.02 μs** |   **2.578 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,335.40 μs |   507.94 μs |  27.842 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,428.41 μs** |    **64.36 μs** |   **3.528 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,380.42 μs |   345.45 μs |  18.935 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.99 μs** |    **13.77 μs** |   **0.755 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,393.28 μs |   568.83 μs |  31.179 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.271 ms** |  **3.441 ms** | **0.1886 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.283 ms |  8.146 ms | 0.4465 ms | 0.005 |  599.03 KB |        8.03 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.425 ms** | **14.180 ms** | **0.7772 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.731 ms | 13.299 ms | 0.7290 ms | 0.004 |  778.63 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.633 ms** | **26.982 ms** | **1.4790 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.253 ms | 82.583 ms | 4.5266 ms | 0.006 | 1080.64 KB |        1.80 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.426 ms** | **24.557 ms** | **1.3460 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.797 ms | 32.027 ms | 1.7555 ms | 0.005 | 2765.16 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.214 ms** | **31.902 ms** | **1.7487 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.488 ms |  5.047 ms | 0.2766 ms | 0.002 |  195.71 KB |       81.33 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.618 ms** |  **9.492 ms** | **0.5203 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.853 ms | 40.237 ms | 2.2056 ms | 0.002 |  186.36 KB |       44.75 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.805 ms** | **28.128 ms** | **1.5418 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.737 ms |  6.911 ms | 0.3788 ms | 0.002 |  267.17 KB |      111.03 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.393 ms** | **32.433 ms** | **1.7778 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.375 ms |  4.418 ms | 0.2421 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.814 μs |  1.3914 μs | 0.0763 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.004 μs |  0.3649 μs | 0.0200 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.890 μs |  0.7389 μs | 0.0405 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.173 μs | 11.6550 μs | 0.6388 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.244 μs |  7.7957 μs | 0.4273 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.301 μs |  2.5301 μs | 0.1387 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.583 μs |  5.6659 μs | 0.3106 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.006 μs | 30.2962 μs | 1.6606 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.531 μs |  0.6907 μs | 0.0379 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.683 μs |  2.9298 μs | 0.1606 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,311.7 ns | 1,281.0 ns | 70.22 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,350.8 ns |   557.4 ns | 30.55 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,259.2 ns | 1,214.7 ns | 66.58 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,441.3 ns | 1,044.9 ns | 57.27 ns |  0.58 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    808.7 ns | 1,004.8 ns | 55.08 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,215.3 ns |   459.1 ns | 25.17 ns |  9.38 |    0.05 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,181.3 ns |   459.1 ns | 25.17 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,038.0 ns |   482.7 ns | 26.46 ns |  0.97 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.41 μs |   5.267 μs |  0.289 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.37 μs | 184.275 μs | 10.101 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.41 μs |   8.711 μs |  0.477 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,671.60 μs | 634.199 μs | 34.763 μs |    1280 B |


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