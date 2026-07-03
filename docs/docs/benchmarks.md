---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 05:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,086.75 μs** |   **216.16 μs** |  **11.849 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,337.13 μs | 1,992.61 μs | 109.222 μs |  0.22 |    0.02 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,244.15 μs** |   **130.83 μs** |   **7.171 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,311.00 μs |   673.42 μs |  36.912 μs |  0.32 |    0.00 |  15.6250 |       - |  339.89 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,623.99 μs** |   **240.67 μs** |  **13.192 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,173.18 μs | 1,143.87 μs |  62.699 μs |  0.18 |    0.01 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,829.86 μs** | **2,529.99 μs** | **138.677 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,053.66 μs | 1,485.84 μs |  81.444 μs |  0.51 |    0.01 |  15.6250 |       - |  369.47 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **142.16 μs** |   **355.60 μs** |  **19.492 μs** |  **1.01** |    **0.16** |   **2.4414** |       **-** |   **41.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     60.88 μs |    30.44 μs |   1.668 μs |  0.43 |    0.05 |   0.7324 |  0.4883 |    19.1 KB |        0.46 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,522.22 μs** |   **848.71 μs** |  **46.520 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **424.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    688.60 μs |   998.20 μs |  54.715 μs |  0.45 |    0.03 |   3.9063 |       - |    80.5 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    326.63 μs |   208.97 μs |  11.454 μs |     ? |       ? |   6.8359 |  5.8594 |  197.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,269.06 μs | 3,031.13 μs | 166.146 μs |     ? |       ? |  62.5000 | 54.6875 | 1876.73 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,406.47 μs** |    **75.27 μs** |   **4.126 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,104.33 μs |    12.78 μs |   0.701 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,408.30 μs** |   **131.51 μs** |   **7.209 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,106.22 μs |    21.70 μs |   1.190 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.80 μs** |    **91.95 μs** |   **5.040 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,104.50 μs |    49.69 μs |   2.724 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.18 μs** |   **104.61 μs** |   **5.734 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,104.81 μs |    14.75 μs |   0.809 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.878 ms** | **39.422 ms** | **2.1609 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.384 ms | 40.323 ms | 2.2102 ms | 0.005 |  409.02 KB |        5.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.562 ms** |  **8.178 ms** | **0.4482 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.052 ms | 30.963 ms | 1.6972 ms | 0.005 |  589.51 KB |        2.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.102 ms** | **10.970 ms** | **0.6013 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.912 ms |  9.644 ms | 0.5286 ms | 0.005 |  806.01 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.590 ms** | **15.647 ms** | **0.8576 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.940 ms | 13.880 ms | 0.7608 ms | 0.005 | 2576.47 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.272 ms** |  **4.436 ms** | **0.2431 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.058 ms |  4.722 ms | 0.2588 ms | 0.002 |  185.06 KB |       76.91 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.933 ms** | **35.375 ms** | **1.9390 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.998 ms | 43.999 ms | 2.4117 ms | 0.002 |  182.52 KB |       43.83 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.252 ms** | **10.678 ms** | **0.5853 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.223 ms |  8.680 ms | 0.4758 ms | 0.002 |  216.78 KB |       90.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.176 ms** | **11.136 ms** | **0.6104 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.551 ms |  9.636 ms | 0.5282 ms | 0.002 |  183.16 KB |       43.82 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.901 μs |  88.300 μs |  4.8400 μs | 23.636 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 11.053 μs |  10.686 μs |  0.5857 μs | 11.176 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 17.673 μs | 210.845 μs | 11.5571 μs | 11.167 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.499 μs |   8.114 μs |  0.4447 μs | 32.373 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.947 μs |   2.067 μs |  0.1133 μs |  8.913 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.495 μs |   6.373 μs |  0.3493 μs | 21.431 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.692 μs |  21.342 μs |  1.1698 μs | 18.532 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.617 μs |   9.873 μs |  0.5412 μs |  4.527 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.583 μs |   8.242 μs |  0.4518 μs | 10.446 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,389.0 ns | 1,845.0 ns | 101.1 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,356.0 ns | 3,020.8 ns | 165.6 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,677.2 ns | 3,355.6 ns | 183.9 ns |  0.40 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,588.8 ns | 2,593.6 ns | 142.2 ns |  0.61 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    734.3 ns | 2,206.9 ns | 121.0 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,673.0 ns | 6,473.3 ns | 354.8 ns |  8.40 |    0.53 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,260.0 ns | 5,738.2 ns | 314.5 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,180.0 ns | 6,367.2 ns | 349.0 ns |  0.98 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.110 μs |  12.890 μs |  0.7066 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.693 μs | 323.861 μs | 17.7519 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.701 μs |   6.661 μs |  0.3651 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,608.936 μs | 191.784 μs | 10.5124 μs |    1280 B |


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