---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 04:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,108.90 μs** |   **713.80 μs** |  **39.126 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,487.50 μs | 2,971.39 μs | 162.872 μs |  0.24 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,185.56 μs** | **1,077.51 μs** |  **59.062 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,300.64 μs |   301.81 μs |  16.543 μs |  0.32 |    0.00 |  15.6250 |       - |  339.38 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,647.50 μs** |   **434.57 μs** |  **23.820 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,144.02 μs |   674.63 μs |  36.979 μs |  0.17 |    0.00 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,669.65 μs** | **2,657.90 μs** | **145.689 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,718.11 μs | 1,024.63 μs |  56.163 μs |  0.49 |    0.01 |  15.6250 |       - |  361.63 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.35 μs** |   **306.06 μs** |  **16.776 μs** |  **1.01** |    **0.14** |   **2.4414** |       **-** |   **41.46 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.98 μs |    72.12 μs |   3.953 μs |  0.40 |    0.05 |        - |       - |    7.04 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,371.95 μs** | **3,029.98 μs** | **166.083 μs** |  **1.01** |    **0.15** |  **25.3906** |       **-** |  **420.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    663.74 μs | 1,392.26 μs |  76.315 μs |  0.49 |    0.07 |        - |       - |   75.19 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    217.74 μs |   390.89 μs |  21.426 μs |     ? |       ? |   0.4883 |       - |   12.17 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,245.33 μs | 4,165.99 μs | 228.352 μs |     ? |       ? |   7.8125 |       - |  915.25 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,404.40 μs** |    **77.39 μs** |   **4.242 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,379.08 μs |   214.52 μs |  11.759 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,406.19 μs** |    **47.04 μs** |   **2.578 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,341.55 μs |   378.64 μs |  20.754 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,411.46 μs** |   **141.38 μs** |   **7.750 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,295.98 μs |   365.77 μs |  20.049 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.67 μs** |    **90.07 μs** |   **4.937 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,107.67 μs |    76.14 μs |   4.173 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,172.153 ms** | **70.021 ms** | **3.8381 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.858 ms | 19.092 ms | 1.0465 ms | 0.006 | 592.71 KB |        7.94 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.039 ms** | **20.809 ms** | **1.1406 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.485 ms | 30.430 ms | 1.6680 ms | 0.006 | 776.97 KB |        3.10 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.723 ms** | **11.016 ms** | **0.6038 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.960 ms | 57.230 ms | 3.1370 ms | 0.007 | 996.64 KB |        1.66 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.859 ms** | **11.399 ms** | **0.6248 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.863 ms | 44.396 ms | 2.4335 ms | 0.005 |   2760 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.094 ms** | **12.837 ms** | **0.7036 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.131 ms | 16.964 ms | 0.9298 ms | 0.002 | 186.81 KB |       77.64 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.871 ms** | **11.580 ms** | **0.6347 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.753 ms | 10.247 ms | 0.5617 ms | 0.002 |  195.4 KB |       46.92 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.363 ms** | **30.408 ms** | **1.6667 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.953 ms | 25.185 ms | 1.3805 ms | 0.003 | 184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.334 ms** | **36.095 ms** | **1.9785 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.446 ms |  6.037 ms | 0.3309 ms | 0.002 | 188.88 KB |       45.19 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.824 μs |  2.1765 μs | 0.1193 μs | 14.877 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.460 μs | 27.5984 μs | 1.5128 μs | 12.299 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.066 μs |  5.6773 μs | 0.3112 μs |  9.939 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.694 μs |  2.5205 μs | 0.1382 μs | 26.640 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.338 μs | 79.0406 μs | 4.3325 μs |  8.857 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.355 μs |  1.6352 μs | 0.0896 μs | 19.401 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.644 μs |  9.3432 μs | 0.5121 μs | 17.683 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.117 μs |  6.2345 μs | 0.3417 μs | 20.017 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.659 μs |  1.7968 μs | 0.0985 μs |  4.629 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.319 μs |  0.3740 μs | 0.0205 μs | 10.319 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,309.7 ns |     640.7 ns |    35.12 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,321.7 ns |     556.5 ns |    30.50 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,786.0 ns |   8,676.4 ns |   475.58 ns |  0.42 |    0.10 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,709.8 ns |   8,723.7 ns |   478.17 ns |  0.64 |    0.11 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    741.7 ns |   2,224.0 ns |   121.90 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 54,074.3 ns | 111,247.7 ns | 6,097.86 ns | 12.68 |    1.67 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,295.3 ns |   8,373.9 ns |   459.00 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,960.5 ns |   2,794.7 ns |   153.19 ns |  0.93 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.13 μs |   4.140 μs |  0.227 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   547.87 μs | 638.365 μs | 34.991 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.18 μs |   9.356 μs |  0.513 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,679.27 μs | 819.439 μs | 44.916 μs |    1280 B |


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