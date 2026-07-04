---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 13:07 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,034.56 μs** |    **437.55 μs** |    **23.984 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,415.23 μs |  2,677.85 μs |   146.782 μs |  0.23 |    0.02 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,235.48 μs** |  **1,284.44 μs** |    **70.405 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,222.47 μs |    191.27 μs |    10.484 μs |  0.31 |    0.00 | 11.7188 |  3.9063 |  339.65 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,534.89 μs** |    **329.48 μs** |    **18.060 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,352.99 μs |  3,346.29 μs |   183.422 μs |  0.21 |    0.02 |       - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,784.05 μs** |  **1,098.71 μs** |    **60.224 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,582.80 μs |  1,631.66 μs |    89.437 μs |  0.52 |    0.01 |  7.8125 |       - |  361.61 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **124.71 μs** |     **25.88 μs** |     **1.419 μs** |  **1.00** |    **0.01** |  **1.7090** |       **-** |   **42.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    65.64 μs |    334.33 μs |    18.326 μs |  0.53 |    0.13 |       - |       - |     7.1 KB |        0.17 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,237.88 μs** |    **270.71 μs** |    **14.839 μs** |  **1.00** |    **0.01** | **15.6250** |       **-** |   **421.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   699.77 μs |    323.51 μs |    17.732 μs |  0.57 |    0.01 |       - |       - |   41.14 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   170.57 μs |    122.76 μs |     6.729 μs |     ? |       ? |  0.4883 |       - |   36.75 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,786.44 μs |  2,754.55 μs |   150.986 μs |     ? |       ? |       - |       - |  921.59 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,385.47 μs** |     **87.54 μs** |     **4.798 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,209.25 μs |    248.12 μs |    13.600 μs |  0.22 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,395.52 μs** |     **71.36 μs** |     **3.912 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,231.28 μs |     85.94 μs |     4.711 μs |  0.23 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **6,127.44 μs** | **23,674.12 μs** | **1,297.658 μs** |  **1.03** |    **0.25** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,241.48 μs |    397.66 μs |    21.797 μs |  0.21 |    0.03 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,376.72 μs** |     **26.97 μs** |     **1.478 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,251.87 μs |    473.40 μs |    25.949 μs |  0.23 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.569 ms** | **17.139 ms** | **0.9394 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.955 ms | 33.169 ms | 1.8181 ms | 0.005 |  592.95 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.909 ms** | **74.409 ms** | **4.0786 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.558 ms | 29.339 ms | 1.6082 ms | 0.004 |  778.16 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.369 ms** | **53.274 ms** | **2.9201 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.979 ms | 58.696 ms | 3.2173 ms | 0.005 |  995.17 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.587 ms** | **18.060 ms** | **0.9899 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.102 ms | 38.455 ms | 2.1078 ms | 0.005 | 2764.58 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.255 ms** | **12.556 ms** | **0.6882 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.967 ms | 19.440 ms | 1.0656 ms | 0.002 |  189.15 KB |       78.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.947 ms** | **14.732 ms** | **0.8075 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.200 ms |  6.858 ms | 0.3759 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.064 ms** | **11.159 ms** | **0.6116 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.166 ms |  5.715 ms | 0.3133 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.026 ms** | **49.153 ms** | **2.6943 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.274 ms | 19.281 ms | 1.0569 ms | 0.002 |  187.15 KB |       44.78 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.848 μs |  0.3649 μs | 0.0200 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.458 μs |  3.3595 μs | 0.1841 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.216 μs |  3.1068 μs | 0.1703 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.808 μs |  4.0850 μs | 0.2239 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.826 μs |  2.0721 μs | 0.1136 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.347 μs | 60.5365 μs | 3.3182 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.896 μs |  5.6009 μs | 0.3070 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.127 μs | 30.6136 μs | 1.6780 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.545 μs |  2.7732 μs | 0.1520 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.600 μs |  7.2989 μs | 0.4001 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,709.3 ns |   1,473.2 ns |    80.75 ns |  0.40 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,372.5 ns |     182.4 ns |    10.00 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,562.3 ns |   6,648.8 ns |   364.45 ns |  0.37 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,169.5 ns |  12,451.4 ns |   682.50 ns |  0.75 |    0.14 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    724.0 ns |     111.0 ns |     6.08 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 52,995.0 ns | 127,856.6 ns | 7,008.25 ns | 12.54 |    1.53 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,231.5 ns |   3,758.7 ns |   206.03 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,053.7 ns |   1,753.0 ns |    96.09 ns |  0.96 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev   | Allocated |
|------------------------ |------------:|-----------:|---------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.19 μs |   5.148 μs | 0.282 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.56 μs | 116.287 μs | 6.374 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.31 μs |   3.577 μs | 0.196 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,644.86 μs | 136.505 μs | 7.482 μs |    1280 B |


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