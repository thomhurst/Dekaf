---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 04:37 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,131.59 μs** |   **279.04 μs** |  **15.295 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,500.24 μs | 2,399.08 μs | 131.502 μs |  0.24 |    0.02 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,382.96 μs** |   **699.67 μs** |  **38.351 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,271.01 μs |   214.11 μs |  11.736 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.86 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,240.33 μs** | **1,282.49 μs** |  **70.298 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,328.61 μs |   280.78 μs |  15.390 μs |  0.21 |    0.00 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,716.15 μs** | **1,882.64 μs** | **103.194 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,431.22 μs | 1,531.42 μs |  83.942 μs |  0.51 |    0.01 |  15.6250 |       - |  369.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.58 μs** |    **26.10 μs** |   **1.431 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.10 μs |    46.69 μs |   2.559 μs |  0.47 |    0.02 |   0.4883 |       - |   16.44 KB |        0.39 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,408.69 μs** |   **716.55 μs** |  **39.277 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **421.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    775.28 μs |   865.57 μs |  47.445 μs |  0.55 |    0.03 |   3.9063 |       - |  159.23 KB |        0.38 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    328.05 μs |   315.30 μs |  17.282 μs |     ? |       ? |   6.8359 |  5.8594 |  199.59 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,117.80 μs | 3,204.42 μs | 175.645 μs |     ? |       ? |  70.3125 | 62.5000 | 1957.17 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,419.69 μs** |    **79.69 μs** |   **4.368 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,114.66 μs |    10.08 μs |   0.553 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.24 μs** |   **214.77 μs** |  **11.772 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,120.29 μs |    74.40 μs |   4.078 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,421.03 μs** |    **98.52 μs** |   **5.400 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,170.84 μs |   224.18 μs |  12.288 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,420.61 μs** |    **56.62 μs** |   **3.103 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,279.72 μs |   260.13 μs |  14.258 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.058 ms** |   **9.729 ms** | **0.5333 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.292 ms |  33.332 ms | 1.8270 ms | 0.005 |  404.77 KB |        5.42 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.827 ms** |  **20.874 ms** | **1.1442 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.458 ms |  24.961 ms | 1.3682 ms | 0.004 |  586.46 KB |        2.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.481 ms** |  **16.560 ms** | **0.9077 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.853 ms | 157.792 ms | 8.6491 ms | 0.006 |  806.05 KB |        1.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.138 ms** |  **11.846 ms** | **0.6493 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.365 ms |  31.098 ms | 1.7046 ms | 0.005 | 2648.23 KB |        1.12 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.947 ms** |  **37.297 ms** | **2.0444 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.550 ms |  27.035 ms | 1.4819 ms | 0.002 |  183.07 KB |       76.08 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.097 ms** |  **27.856 ms** | **1.5269 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.397 ms |   3.254 ms | 0.1784 ms | 0.002 |   182.6 KB |       43.85 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.618 ms** |  **62.697 ms** | **3.4366 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.455 ms |   3.644 ms | 0.1997 ms | 0.002 |  180.79 KB |       75.13 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.485 ms** |  **19.425 ms** | **1.0647 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.698 ms |   8.936 ms | 0.4898 ms | 0.002 |  259.95 KB |       62.19 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.696 μs | 95.872 μs | 5.2550 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.480 μs |  3.800 μs | 0.2083 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.513 μs |  4.869 μs | 0.2669 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.840 μs |  3.069 μs | 0.1682 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.966 μs |  1.425 μs | 0.0781 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.366 μs |  2.943 μs | 0.1613 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 20.963 μs | 34.927 μs | 1.9145 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.661 μs |  1.695 μs | 0.0929 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.947 μs | 17.141 μs | 0.9395 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,566.7 ns |  6,458.9 ns | 354.03 ns |  0.35 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,637.3 ns |  8,023.2 ns | 439.78 ns |  0.36 |    0.09 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,583.0 ns |  2,832.2 ns | 155.24 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,822.2 ns |  3,315.8 ns | 181.75 ns |  0.62 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    694.5 ns |  1,169.2 ns |  64.09 ns |  0.15 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,327.5 ns |  8,520.1 ns | 467.01 ns |  9.15 |    1.05 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,576.0 ns | 11,851.0 ns | 649.59 ns |  1.01 |    0.17 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,360.2 ns |  8,548.0 ns | 468.54 ns |  0.96 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.49 μs |  45.63 μs |  2.501 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   555.88 μs | 159.92 μs |  8.766 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.96 μs |  21.77 μs |  1.194 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,679.32 μs | 352.49 μs | 19.321 μs |    1280 B |


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