---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 02:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,164.88 μs** | **1,765.47 μs** |  **96.771 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,313.99 μs |   629.37 μs |  34.498 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,361.28 μs** |   **267.38 μs** |  **14.656 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,315.69 μs |   241.16 μs |  13.219 μs |  0.31 |    0.00 |  15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,337.06 μs** | **1,127.90 μs** |  **61.824 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,337.26 μs |   800.69 μs |  43.888 μs |  0.21 |    0.01 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,211.67 μs** | **1,582.39 μs** |  **86.736 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,456.77 μs | 6,480.22 μs | 355.203 μs |  0.53 |    0.03 |  15.6250 |       - |  361.82 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.61 μs** |    **47.32 μs** |   **2.594 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.19 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.61 μs |   144.55 μs |   7.923 μs |  0.43 |    0.05 |        - |       - |    10.4 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,412.11 μs** |   **399.57 μs** |  **21.902 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **420.94 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    674.04 μs |   729.11 μs |  39.965 μs |  0.48 |    0.03 |        - |       - |  106.75 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.26 μs |   343.04 μs |  18.803 μs |     ? |       ? |   0.9766 |       - |  118.98 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,162.58 μs | 4,618.68 μs | 253.165 μs |     ? |       ? |   7.8125 |       - | 1018.89 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,423.56 μs** |   **224.64 μs** |  **12.314 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,347.84 μs |   667.76 μs |  36.602 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.98 μs** |   **390.89 μs** |  **21.426 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,294.58 μs |   355.25 μs |  19.472 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,456.29 μs** |   **612.13 μs** |  **33.553 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,168.63 μs |   491.44 μs |  26.937 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,475.36 μs** |    **60.36 μs** |   **3.309 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,110.77 μs |    22.84 μs |   1.252 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.287 ms** | **17.911 ms** | **0.9818 ms** | **3,169.561 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.217 ms | 25.776 ms | 1.4129 ms |    17.397 ms | 0.005 |  604.39 KB |        8.10 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.561 ms** | **25.738 ms** | **1.4108 ms** | **3,166.041 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.854 ms | 42.990 ms | 2.3564 ms |    16.204 ms | 0.005 |  776.23 KB |        3.10 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.863 ms** | **25.397 ms** | **1.3921 ms** | **3,167.190 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.893 ms | 44.793 ms | 2.4553 ms |    14.169 ms | 0.005 |  994.54 KB |        1.65 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.293 ms** | **21.177 ms** | **1.1608 ms** | **3,166.753 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.286 ms | 35.165 ms | 1.9275 ms |    15.811 ms | 0.005 | 2763.52 KB |        1.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.068 ms** |  **7.637 ms** | **0.4186 ms** | **3,156.860 ms** | **1.000** |    **3.34 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.618 ms | 16.629 ms | 0.9115 ms |     5.311 ms | 0.002 |  188.92 KB |       56.50 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.666 ms** | **32.462 ms** | **1.7794 ms** | **3,156.270 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.231 ms | 58.513 ms | 3.2073 ms |     5.401 ms | 0.002 |  195.47 KB |       46.94 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.390 ms** | **30.083 ms** | **1.6489 ms** | **3,156.428 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.709 ms | 14.334 ms | 0.7857 ms |     6.577 ms | 0.002 |   184.7 KB |       76.76 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.882 ms** | **30.307 ms** | **1.6612 ms** | **3,157.685 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.852 ms |  7.184 ms | 0.3938 ms |     5.862 ms | 0.002 |  189.38 KB |       45.31 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.355 μs |   4.469 μs |  0.2449 μs | 15.409 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.205 μs |   3.802 μs |  0.2084 μs | 10.095 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.535 μs |   9.456 μs |  0.5183 μs | 12.688 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.759 μs |   7.461 μs |  0.4090 μs | 32.900 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.398 μs |  10.633 μs |  0.5828 μs | 13.296 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.595 μs |   3.842 μs |  0.2106 μs | 22.685 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 25.245 μs | 218.447 μs | 11.9738 μs | 19.910 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.446 μs |  33.542 μs |  1.8385 μs | 19.564 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.397 μs |  10.540 μs |  0.5777 μs |  4.146 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.678 μs |   9.305 μs |  0.5101 μs | 10.540 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,017.0 ns |  4,092.9 ns | 224.35 ns |  0.23 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,379.3 ns |  5,367.8 ns | 294.23 ns |  0.31 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,489.3 ns |  3,564.3 ns | 195.37 ns |  0.33 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,447.3 ns |  1,827.4 ns | 100.17 ns |  0.55 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    674.3 ns |  1,635.2 ns |  89.63 ns |  0.15 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,786.3 ns |  4,659.6 ns | 255.41 ns |  8.05 |    1.01 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,514.8 ns | 12,944.2 ns | 709.52 ns |  1.02 |    0.19 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,916.0 ns | 12,023.4 ns | 659.05 ns |  0.88 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.299 μs |    25.992 μs |   1.4247 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.723 μs |   358.570 μs |  19.6544 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.436 μs |     1.239 μs |   0.0679 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,623.387 μs | 1,903.775 μs | 104.3523 μs |    1280 B |


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