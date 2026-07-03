---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 02:17 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,134.80 μs** |    **849.91 μs** |  **46.587 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,296.21 μs |  1,329.68 μs |  72.884 μs |  0.21 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,317.20 μs** |    **886.52 μs** |  **48.593 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,264.18 μs |    351.50 μs |  19.267 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.88 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,841.84 μs** | **10,709.03 μs** | **586.998 μs** |  **1.00** |    **0.10** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,232.20 μs |  1,795.59 μs |  98.422 μs |  0.18 |    0.02 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,249.16 μs** |  **4,034.73 μs** | **221.157 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,466.74 μs |  5,049.14 μs | 276.760 μs |  0.53 |    0.02 |  15.6250 |       - |  369.36 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.17 μs** |     **42.15 μs** |   **2.311 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     72.76 μs |    114.06 μs |   6.252 μs |  0.53 |    0.04 |   0.4883 |       - |   13.13 KB |        0.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,390.55 μs** |    **861.56 μs** |  **47.225 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **420.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    717.18 μs |  1,286.45 μs |  70.515 μs |  0.52 |    0.05 |   3.9063 |       - |  143.45 KB |        0.34 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    308.69 μs |    354.08 μs |  19.408 μs |     ? |       ? |   6.8359 |  5.8594 |   199.7 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,170.91 μs |  3,987.30 μs | 218.558 μs |     ? |       ? |  62.5000 | 54.6875 | 1920.36 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,399.57 μs** |     **64.41 μs** |   **3.531 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,112.10 μs |     26.12 μs |   1.432 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,436.44 μs** |    **210.71 μs** |  **11.549 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,118.82 μs |     78.13 μs |   4.283 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.57 μs** |    **291.47 μs** |  **15.977 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,126.86 μs |    158.99 μs |   8.715 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.78 μs** |    **103.18 μs** |   **5.656 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,131.94 μs |    199.88 μs |  10.956 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.293 ms** | **18.555 ms** | **1.0171 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.889 ms | 25.734 ms | 1.4106 ms | 0.004 |  411.79 KB |        5.52 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.806 ms** |  **9.114 ms** | **0.4996 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.749 ms | 41.760 ms | 2.2890 ms | 0.005 |  595.42 KB |        2.38 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.638 ms** |  **7.963 ms** | **0.4365 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.220 ms | 93.902 ms | 5.1471 ms | 0.005 |  805.48 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.886 ms** | **45.172 ms** | **2.4760 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.360 ms | 33.144 ms | 1.8168 ms | 0.005 | 2646.59 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.764 ms** | **14.876 ms** | **0.8154 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.148 ms | 24.716 ms | 1.3548 ms | 0.002 |  180.84 KB |       75.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.405 ms** | **45.954 ms** | **2.5189 ms** | **1.000** |    **4.77 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.240 ms |  7.244 ms | 0.3971 ms | 0.002 |  184.25 KB |       38.60 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.408 ms** | **21.993 ms** | **1.2055 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.123 ms | 11.088 ms | 0.6078 ms | 0.002 |  181.05 KB |       75.24 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.539 ms** | **14.147 ms** | **0.7755 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.124 ms |  1.372 ms | 0.0752 ms | 0.002 |  183.46 KB |       43.89 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.393 μs |   1.695 μs |  0.0929 μs | 14.437 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.340 μs |   4.937 μs |  0.2706 μs |  9.237 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.191 μs |   5.756 μs |  0.3155 μs | 10.194 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.106 μs | 175.442 μs |  9.6166 μs | 26.619 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.924 μs |   1.219 μs |  0.0668 μs |  8.958 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 25.277 μs | 187.413 μs | 10.2727 μs | 19.417 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 22.368 μs |  11.934 μs |  0.6541 μs | 22.191 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.513 μs |   3.025 μs |  0.1658 μs |  4.433 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.192 μs |   6.906 μs |  0.3786 μs | 10.370 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,196.0 ns |    94.80 ns |   5.20 ns |  0.28 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,226.0 ns | 1,705.28 ns |  93.47 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,329.3 ns | 1,112.77 ns |  60.99 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,761.3 ns | 2,482.11 ns | 136.05 ns |  0.64 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    728.7 ns |   586.45 ns |  32.15 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,808.7 ns | 7,540.21 ns | 413.30 ns | 10.63 |    0.86 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,334.8 ns | 7,722.97 ns | 423.32 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,907.5 ns | 1,493.31 ns |  81.85 ns |  0.91 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev    | Allocated |
|------------------------ |------------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.32 μs |     3.949 μs |  0.216 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   570.96 μs |   384.851 μs | 21.095 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.35 μs |     4.500 μs |  0.247 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,700.73 μs | 1,681.868 μs | 92.189 μs |    1280 B |


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