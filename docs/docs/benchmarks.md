---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 07:31 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,183.45 μs** |   **958.54 μs** |  **52.541 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,411.68 μs | 2,126.87 μs | 116.581 μs |  0.23 |    0.02 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,374.41 μs** | **2,098.63 μs** | **115.033 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,358.73 μs |   602.32 μs |  33.015 μs |  0.32 |    0.01 |  15.6250 |       - |  339.88 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,258.91 μs** |   **760.53 μs** |  **41.687 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,214.04 μs | 1,179.61 μs |  64.658 μs |  0.19 |    0.01 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,696.56 μs** | **3,638.10 μs** | **199.417 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,484.89 μs | 5,296.69 μs | 290.329 μs |  0.51 |    0.02 |  15.6250 |       - |  369.46 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.21 μs** |    **71.49 μs** |   **3.919 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     84.28 μs |   388.72 μs |  21.307 μs |  0.60 |    0.13 |   0.4883 |       - |   14.86 KB |        0.36 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,443.38 μs** |   **200.00 μs** |  **10.963 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **418.01 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    782.58 μs | 3,397.15 μs | 186.209 μs |  0.54 |    0.11 |   3.9063 |       - |  115.05 KB |        0.28 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    304.43 μs |   229.20 μs |  12.563 μs |     ? |       ? |   6.8359 |  5.8594 |  203.52 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,222.16 μs | 5,885.72 μs | 322.616 μs |     ? |       ? |  62.5000 | 54.6875 | 1844.22 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,450.73 μs** |   **345.96 μs** |  **18.963 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,116.92 μs |    10.79 μs |   0.591 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,442.93 μs** |    **61.32 μs** |   **3.361 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,128.05 μs |   197.12 μs |  10.805 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.95 μs** |    **65.11 μs** |   **3.569 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.15 μs |    68.98 μs |   3.781 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,438.03 μs** |    **97.44 μs** |   **5.341 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,389.39 μs |   365.42 μs |  20.030 μs |  0.26 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.962 ms** | **50.883 ms** | **2.7891 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.313 ms | 47.461 ms | 2.6015 ms | 0.005 |  415.75 KB |        5.57 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.260 ms** | **32.693 ms** | **1.7920 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.894 ms | 49.998 ms | 2.7406 ms | 0.004 |  590.07 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.923 ms** | **12.366 ms** | **0.6778 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.441 ms | 26.269 ms | 1.4399 ms | 0.005 |  811.16 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.275 ms** |  **3.241 ms** | **0.1776 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.296 ms | 34.272 ms | 1.8786 ms | 0.005 | 2649.45 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.996 ms** | **35.461 ms** | **1.9437 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.607 ms | 37.244 ms | 2.0415 ms | 0.002 |  190.39 KB |       79.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.129 ms** |  **9.576 ms** | **0.5249 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.442 ms |  8.090 ms | 0.4435 ms | 0.002 |   183.7 KB |       44.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.730 ms** | **14.241 ms** | **0.7806 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.166 ms | 12.620 ms | 0.6917 ms | 0.002 |  253.64 KB |      105.41 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.491 ms** | **26.884 ms** | **1.4736 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.365 ms |  7.852 ms | 0.4304 ms | 0.002 |  183.93 KB |       44.01 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.474 μs | 2.0849 μs | 0.1143 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.754 μs | 4.2277 μs | 0.2317 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.152 μs | 3.0871 μs | 0.1692 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.680 μs | 2.2119 μs | 0.1212 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.967 μs | 0.9757 μs | 0.0535 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.433 μs | 4.4954 μs | 0.2464 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.564 μs | 6.8461 μs | 0.3753 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.585 μs | 3.4343 μs | 0.1882 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.540 μs | 6.0717 μs | 0.3328 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,057.3 ns | 2,374.0 ns | 130.13 ns |  0.47 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,360.5 ns |   221.2 ns |  12.12 ns |  0.31 |    0.00 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,462.0 ns | 1,264.0 ns |  69.28 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,604.0 ns | 3,811.6 ns | 208.93 ns |  0.60 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    827.3 ns |   270.8 ns |  14.84 ns |  0.19 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,923.3 ns | 4,433.7 ns | 243.02 ns |  8.94 |    0.09 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,354.0 ns |   813.2 ns |  44.58 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,979.2 ns | 2,381.0 ns | 130.51 ns |  0.91 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error         | StdDev     | Allocated |
|------------------------ |-------------:|--------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.134 μs |     0.8227 μs |  0.0451 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   547.378 μs |   400.3585 μs | 21.9450 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.227 μs |    15.9330 μs |  0.8733 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,702.628 μs | 1,280.2379 μs | 70.1742 μs |    1280 B |


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