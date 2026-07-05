---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 14:29 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,251.04 μs** |    **666.22 μs** |  **36.518 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,407.25 μs |  1,089.73 μs |  59.732 μs |  0.23 |    0.01 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,467.86 μs** |    **502.09 μs** |  **27.521 μs** |  **1.00** |    **0.00** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,346.69 μs |    659.33 μs |  36.140 μs |  0.31 |    0.00 | 15.6250 |       - |  339.34 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,143.00 μs** |  **1,243.97 μs** |  **68.186 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,349.87 μs |  2,508.45 μs | 137.497 μs |  0.22 |    0.02 |       - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,190.43 μs** | **11,716.81 μs** | **642.238 μs** |  **1.00** |    **0.06** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,222.29 μs |  3,856.54 μs | 211.390 μs |  0.55 |    0.03 | 15.6250 |       - |  362.22 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **148.11 μs** |    **107.05 μs** |   **5.868 μs** |  **1.00** |    **0.05** |  **2.4414** |       **-** |   **41.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.47 μs |    118.53 μs |   6.497 μs |  0.47 |    0.04 |       - |       - |    7.73 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,479.34 μs** |    **803.26 μs** |  **44.029 μs** |  **1.00** |    **0.04** | **23.4375** |       **-** |     **421 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    639.41 μs |    678.29 μs |  37.180 μs |  0.43 |    0.02 |       - |       - |   75.29 KB |        0.18 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    216.08 μs |    447.02 μs |  24.503 μs |     ? |       ? |  0.9766 |       - |   100.7 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,956.12 μs |  3,867.67 μs | 212.000 μs |     ? |       ? |  7.8125 |       - | 1019.94 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,476.01 μs** |     **61.64 μs** |   **3.379 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,338.71 μs |    723.46 μs |  39.655 μs |  0.24 |    0.01 |       - |       - |    1.14 KB |        0.96 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,498.27 μs** |    **240.61 μs** |  **13.189 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,312.98 μs |    175.17 μs |   9.602 μs |  0.24 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,511.68 μs** |    **362.35 μs** |  **19.862 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,197.74 μs |    121.36 μs |   6.652 μs |  0.22 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,471.44 μs** |    **233.54 μs** |  **12.801 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,124.31 μs |    119.80 μs |   6.567 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.161 ms** | **25.683 ms** | **1.4078 ms** | **3,169.351 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    19.110 ms | 52.152 ms | 2.8586 ms |    20.197 ms | 0.006 |  594.98 KB |        7.97 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.163 ms** |  **7.512 ms** | **0.4118 ms** | **3,166.152 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.089 ms | 36.911 ms | 2.0232 ms |    15.627 ms | 0.005 |  783.28 KB |        3.13 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.425 ms** | **10.778 ms** | **0.5908 ms** | **3,167.496 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.659 ms | 43.073 ms | 2.3610 ms |    17.520 ms | 0.006 |   997.5 KB |        1.66 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.896 ms** |  **7.896 ms** | **0.4328 ms** | **3,166.779 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.296 ms |  4.662 ms | 0.2556 ms |    16.307 ms | 0.005 | 2764.46 KB |        1.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.378 ms** | **39.703 ms** | **2.1762 ms** | **3,154.941 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.476 ms | 11.300 ms | 0.6194 ms |     6.601 ms | 0.002 |  184.47 KB |       76.66 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.015 ms** |  **8.437 ms** | **0.4625 ms** | **3,156.013 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.052 ms |  3.218 ms | 0.1764 ms |     6.108 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.058 ms** | **22.433 ms** | **1.2297 ms** | **3,156.291 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     9.496 ms | 93.981 ms | 5.1514 ms |     6.755 ms | 0.003 |  184.69 KB |       76.75 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.635 ms** |  **5.436 ms** | **0.2980 ms** | **3,157.567 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.853 ms | 15.072 ms | 0.8261 ms |     6.780 ms | 0.002 |  188.44 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.418 μs |   7.569 μs |  0.4149 μs | 23.463 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.747 μs |  36.122 μs |  1.9800 μs | 14.245 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.828 μs |   9.764 μs |  0.5352 μs |  9.664 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.503 μs |   6.378 μs |  0.3496 μs | 25.334 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.957 μs |   4.422 μs |  0.2424 μs | 12.042 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.440 μs | 206.366 μs | 11.3116 μs | 17.117 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 16.833 μs |  36.585 μs |  2.0053 μs | 16.208 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 18.428 μs |  36.648 μs |  2.0088 μs | 17.391 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.913 μs |   9.024 μs |  0.4946 μs |  4.638 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.045 μs |  19.436 μs |  1.0654 μs | 11.317 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,484.3 ns |  2,774.0 ns |   152.1 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,448.7 ns |  3,666.8 ns |   201.0 ns |  0.31 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,409.7 ns |  5,936.0 ns |   325.4 ns |  0.31 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  4,026.5 ns | 13,971.5 ns |   765.8 ns |  0.87 |    0.18 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    840.5 ns |  6,266.0 ns |   343.5 ns |  0.18 |    0.07 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,281.3 ns | 65,678.6 ns | 3,600.1 ns |  8.53 |    1.18 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,655.2 ns | 10,540.2 ns |   577.7 ns |  1.01 |    0.16 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,276.5 ns |  6,748.6 ns |   369.9 ns |  0.93 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.61 μs |  12.25 μs |  0.672 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   459.42 μs | 108.67 μs |  5.956 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    12.35 μs |  22.30 μs |  1.223 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,243.92 μs | 926.79 μs | 50.801 μs |    1280 B |


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