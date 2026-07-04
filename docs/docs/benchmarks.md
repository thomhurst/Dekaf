---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 20:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,144.13 μs** |   **222.476 μs** |  **12.195 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,442.36 μs | 3,003.736 μs | 164.645 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,405.27 μs** |   **815.080 μs** |  **44.677 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,353.81 μs | 1,187.334 μs |  65.082 μs |  0.32 |    0.01 |  15.6250 |       - |  339.44 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,222.92 μs** |   **214.906 μs** |  **11.780 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,349.59 μs | 2,945.665 μs | 161.462 μs |  0.22 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,729.07 μs** | **5,149.157 μs** | **282.243 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,741.55 μs | 7,399.083 μs | 405.569 μs |  0.53 |    0.03 |  15.6250 |       - |  361.66 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **145.35 μs** |    **22.527 μs** |   **1.235 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |    **41.5 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     80.87 μs |   273.425 μs |  14.987 μs |  0.56 |    0.09 |        - |       - |    8.03 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,400.44 μs** |   **504.598 μs** |  **27.659 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **418.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    665.96 μs | 1,548.912 μs |  84.901 μs |  0.48 |    0.05 |        - |       - |   87.58 KB |        0.21 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    247.24 μs | 1,708.263 μs |  93.636 μs |     ? |       ? |   0.4883 |       - |    11.8 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,049.81 μs | 3,736.574 μs | 204.814 μs |     ? |       ? |   7.8125 |       - | 1015.08 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,424.86 μs** |    **73.081 μs** |   **4.006 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,370.93 μs |   504.335 μs |  27.644 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,447.79 μs** |   **336.015 μs** |  **18.418 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,379.91 μs |   309.317 μs |  16.955 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,429.25 μs** |     **8.937 μs** |   **0.490 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,387.91 μs |   168.119 μs |   9.215 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,429.24 μs** |    **73.936 μs** |   **4.053 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,387.56 μs |   392.521 μs |  21.515 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.381 ms** | **33.521 ms** | **1.8374 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.197 ms | 13.036 ms | 0.7145 ms | 0.005 |  593.95 KB |        7.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.366 ms** | **18.165 ms** | **0.9957 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.703 ms | 19.066 ms | 1.0451 ms | 0.005 |   779.1 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.376 ms** | **22.877 ms** | **1.2540 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.334 ms | 18.516 ms | 1.0149 ms | 0.005 |  998.05 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.382 ms** | **29.237 ms** | **1.6026 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.182 ms | 27.085 ms | 1.4846 ms | 0.005 | 2760.11 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.137 ms** |  **2.224 ms** | **0.1219 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.492 ms |  5.304 ms | 0.2907 ms | 0.002 |  184.44 KB |       76.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.306 ms** | **32.552 ms** | **1.7843 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.590 ms |  7.720 ms | 0.4231 ms | 0.002 |  186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.302 ms** | **42.288 ms** | **2.3180 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.559 ms |  6.430 ms | 0.3525 ms | 0.002 |  256.65 KB |      106.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.202 ms** | **31.365 ms** | **1.7192 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.371 ms | 19.398 ms | 1.0633 ms | 0.002 |  259.09 KB |       61.99 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.552 μs | 223.2311 μs | 12.2360 μs | 26.710 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.069 μs |  31.3775 μs |  1.7199 μs | 12.774 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.524 μs |   2.5723 μs |  0.1410 μs | 10.510 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.842 μs |   0.7456 μs |  0.0409 μs | 26.835 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.298 μs |   8.0917 μs |  0.4435 μs |  9.067 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.899 μs |  78.9152 μs |  4.3256 μs | 20.518 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.916 μs |   5.5269 μs |  0.3029 μs | 18.033 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.203 μs |   7.7852 μs |  0.4267 μs | 20.003 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.635 μs |   0.6907 μs |  0.0379 μs |  4.618 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.523 μs |  18.9902 μs |  1.0409 μs | 11.025 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,512.5 ns |    482.7 ns |    26.46 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,278.0 ns |  1,264.0 ns |    69.28 ns |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,396.7 ns |    910.4 ns |    49.90 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,607.7 ns |  7,077.1 ns |   387.92 ns |  0.60 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    773.7 ns |    640.7 ns |    35.12 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,631.2 ns |  5,176.2 ns |   283.73 ns |  9.33 |    0.45 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,361.7 ns |  4,495.2 ns |   246.39 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  5,248.2 ns | 33,708.2 ns | 1,847.66 ns |  1.21 |    0.37 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.991 μs |  17.998 μs |  0.9865 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   523.149 μs | 182.812 μs | 10.0205 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.998 μs |  21.411 μs |  1.1736 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,672.402 μs | 546.864 μs | 29.9755 μs |    1280 B |


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