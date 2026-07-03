---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 22:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,146.84 μs** |   **742.419 μs** |  **40.694 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,354.41 μs |   443.065 μs |  24.286 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,376.58 μs** |   **282.406 μs** |  **15.480 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,327.50 μs |   366.764 μs |  20.104 μs |  0.32 |    0.00 |  19.5313 |  3.9063 |  339.55 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,236.73 μs** |   **168.640 μs** |   **9.244 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,486.47 μs |   896.553 μs |  49.143 μs |  0.24 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,526.23 μs** | **2,737.140 μs** | **150.032 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,579.45 μs | 7,688.666 μs | 421.442 μs |  0.53 |    0.03 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.09 μs** |   **161.559 μs** |   **8.856 μs** |  **1.00** |    **0.08** |   **2.4414** |       **-** |   **41.98 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.48 μs |    83.352 μs |   4.569 μs |  0.42 |    0.04 |        - |       - |   10.45 KB |        0.25 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,468.57 μs** |   **343.008 μs** |  **18.801 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    633.86 μs | 1,512.380 μs |  82.899 μs |  0.43 |    0.05 |   1.9531 |       - |  131.27 KB |        0.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    199.13 μs |   152.408 μs |   8.354 μs |     ? |       ? |   0.9766 |       - |  102.93 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,008.41 μs | 2,764.205 μs | 151.515 μs |     ? |       ? |   7.8125 |       - | 1005.04 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,442.31 μs** |    **37.308 μs** |   **2.045 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,226.44 μs |    84.851 μs |   4.651 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,428.13 μs** |   **221.396 μs** |  **12.135 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,126.13 μs |   195.636 μs |  10.723 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,421.68 μs** |     **6.276 μs** |   **0.344 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,120.79 μs |    75.241 μs |   4.124 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,423.88 μs** |    **25.765 μs** |   **1.412 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.91 μs |    17.370 μs |   0.952 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.237 ms** |  **59.403 ms** | **3.2561 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.684 ms |  15.990 ms | 0.8765 ms | 0.006 | 592.93 KB |        7.95 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.450 ms** |  **15.454 ms** | **0.8471 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.538 ms |  39.261 ms | 2.1520 ms | 0.005 | 776.21 KB |        3.10 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.382 ms** |   **5.638 ms** | **0.3090 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.823 ms | 127.029 ms | 6.9629 ms | 0.006 | 995.17 KB |        1.65 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.491 ms** |   **6.535 ms** | **0.3582 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.669 ms |  43.021 ms | 2.3581 ms | 0.005 | 2762.1 KB |        1.17 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.304 ms** |  **52.888 ms** | **2.8990 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.417 ms |   5.140 ms | 0.2817 ms | 0.002 | 184.38 KB |       76.62 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.942 ms** |  **17.440 ms** | **0.9560 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.652 ms |  36.558 ms | 2.0039 ms | 0.002 | 186.47 KB |       44.78 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.874 ms** |   **5.333 ms** | **0.2923 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.455 ms |  13.217 ms | 0.7245 ms | 0.002 | 184.69 KB |       76.75 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.949 ms** |  **14.295 ms** | **0.7836 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.525 ms |  27.739 ms | 1.5205 ms | 0.002 | 187.12 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.924 μs |  0.9261 μs | 0.0508 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.671 μs |  2.1661 μs | 0.1187 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.184 μs | 19.7336 μs | 1.0817 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.780 μs |  0.3649 μs | 0.0200 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.934 μs |  0.2107 μs | 0.0115 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.824 μs | 68.7141 μs | 3.7665 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.248 μs | 14.2391 μs | 0.7805 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.483 μs | 17.8779 μs | 0.9799 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.716 μs |  3.1173 μs | 0.1709 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.899 μs |  4.7474 μs | 0.2602 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,352.7 ns | 1,910.8 ns | 104.74 ns |  0.30 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,215.3 ns | 1,004.8 ns |  55.08 ns |  0.27 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,430.3 ns | 4,628.3 ns | 253.69 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,499.5 ns |   364.9 ns |  20.00 ns |  0.55 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    738.3 ns | 1,010.9 ns |  55.41 ns |  0.16 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,600.5 ns | 7,908.8 ns | 433.51 ns |  8.73 |    0.70 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,565.0 ns | 8,021.8 ns | 439.70 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,157.3 ns | 7,021.6 ns | 384.88 ns |  0.92 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.36 μs |   8.443 μs |  0.463 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   543.74 μs | 671.374 μs | 36.800 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.27 μs |   6.148 μs |  0.337 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,672.02 μs | 224.173 μs | 12.288 μs |    1280 B |


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