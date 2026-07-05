---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 17:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,126.00 μs** |   **352.13 μs** |  **19.301 μs** |  **6,134.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,261.62 μs |   425.68 μs |  23.333 μs |  1,248.57 μs |  0.21 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,325.87 μs** | **1,435.63 μs** |  **78.692 μs** |  **7,337.51 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,349.58 μs |   982.21 μs |  53.838 μs |  2,357.97 μs |  0.32 |    0.01 |  15.6250 |       - |  339.33 KB |        0.32 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,504.59 μs** | **1,360.96 μs** |  **74.599 μs** |  **6,532.81 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,210.12 μs | 1,139.41 μs |  62.455 μs |  1,230.64 μs |  0.19 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,879.83 μs** | **3,646.26 μs** | **199.864 μs** | **11,771.01 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,459.63 μs | 1,812.28 μs |  99.337 μs |  6,510.39 μs |  0.54 |    0.01 |  15.6250 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **153.28 μs** |   **231.02 μs** |  **12.663 μs** |    **152.62 μs** |  **1.00** |    **0.10** |   **2.4414** |       **-** |   **41.97 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     52.39 μs |    77.36 μs |   4.241 μs |     51.73 μs |  0.34 |    0.03 |        - |       - |    6.23 KB |        0.15 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,443.41 μs** |   **126.00 μs** |   **6.907 μs** |  **1,445.08 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **418.45 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    610.46 μs |   430.69 μs |  23.608 μs |    621.82 μs |  0.42 |    0.01 |        - |       - |   95.09 KB |        0.23 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    271.77 μs | 2,467.31 μs | 135.241 μs |    201.38 μs |     ? |       ? |   0.4883 |       - |   11.78 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,037.60 μs | 2,141.46 μs | 117.380 μs |  1,985.51 μs |     ? |       ? |   7.8125 |       - | 1017.59 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.13 μs** |    **55.79 μs** |   **3.058 μs** |  **5,397.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.21 μs |    43.39 μs |   2.378 μs |  1,113.79 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,408.19 μs** |   **309.88 μs** |  **16.985 μs** |  **5,399.43 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,116.08 μs |    49.36 μs |   2.705 μs |  1,114.62 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.12 μs** |    **70.01 μs** |   **3.838 μs** |  **5,398.75 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,385.75 μs |   442.70 μs |  24.266 μs |  1,391.19 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,400.40 μs** |    **53.44 μs** |   **2.929 μs** |  **5,399.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,328.15 μs |   410.61 μs |  22.507 μs |  1,318.38 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165.796 ms** | **13.436 ms** | **0.7365 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.133 ms | 43.886 ms | 2.4056 ms | 0.005 |  594.83 KB |        7.97 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163.825 ms** | **20.702 ms** | **1.1348 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.135 ms | 45.226 ms | 2.4790 ms | 0.005 |  788.83 KB |        3.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.599 ms** |  **5.086 ms** | **0.2788 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.187 ms | 45.160 ms | 2.4754 ms | 0.005 |  1022.3 KB |        1.70 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.290 ms** | **17.996 ms** | **0.9864 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.963 ms | 25.490 ms | 1.3972 ms | 0.005 | 2840.71 KB |        1.20 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.523 ms** | **23.799 ms** | **1.3045 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.803 ms | 15.437 ms | 0.8461 ms | 0.002 |  184.47 KB |       76.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.024 ms** | **18.736 ms** | **1.0270 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.583 ms |  6.537 ms | 0.3583 ms | 0.002 |  186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.597 ms** | **37.115 ms** | **2.0344 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.285 ms | 42.287 ms | 2.3179 ms | 0.003 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.732 ms** | **19.578 ms** | **1.0732 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.721 ms |  6.702 ms | 0.3674 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.823 μs |  4.434 μs | 0.2431 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.488 μs |  8.510 μs | 0.4664 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.506 μs |  3.853 μs | 0.2112 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.782 μs |  1.961 μs | 0.1075 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.275 μs | 69.398 μs | 3.8039 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.294 μs |  2.477 μs | 0.1358 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.825 μs | 13.652 μs | 0.7483 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.791 μs | 25.447 μs | 1.3948 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.003 μs |  8.243 μs | 0.4518 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.245 μs | 45.441 μs | 2.4908 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,996.3 ns |   1,845.5 ns |    101.16 ns |  1,943.0 ns |  0.46 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,494.5 ns |   2,227.5 ns |    122.09 ns |  1,468.5 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,574.5 ns |   6,267.6 ns |    343.55 ns |  1,417.5 ns |  0.36 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,174.2 ns |   6,939.1 ns |    380.36 ns |  2,990.5 ns |  0.72 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    759.0 ns |   1,644.7 ns |     90.15 ns |    753.0 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 55,773.0 ns | 146,637.5 ns |  8,037.69 ns | 53,779.0 ns | 12.72 |    1.77 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,400.7 ns |   5,602.9 ns |    307.12 ns |  4,437.0 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | 10,171.7 ns | 196,695.7 ns | 10,781.55 ns |  3,997.0 ns |  2.32 |    2.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.37 μs |   5.930 μs |  0.325 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   535.10 μs | 584.961 μs | 32.064 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.05 μs |   4.067 μs |  0.223 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,657.39 μs | 894.953 μs | 49.055 μs |    1280 B |


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