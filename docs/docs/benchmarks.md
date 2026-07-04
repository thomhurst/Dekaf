---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 22:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,300.53 μs** |  **1,133.91 μs** |  **62.153 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,481.40 μs |  4,123.99 μs | 226.050 μs |  0.24 |    0.03 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,455.67 μs** |    **876.03 μs** |  **48.018 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,397.98 μs |  1,688.05 μs |  92.528 μs |  0.32 |    0.01 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,133.75 μs** |    **741.74 μs** |  **40.657 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,382.94 μs |  3,265.66 μs | 179.002 μs |  0.23 |    0.03 |        - |       - |   36.34 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,087.29 μs** |  **5,009.03 μs** | **274.562 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,912.05 μs | 12,663.51 μs | 694.130 μs |  0.53 |    0.05 |  15.6250 |       - |  361.64 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.81 μs** |     **51.10 μs** |   **2.801 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     53.81 μs |     59.98 μs |   3.287 μs |  0.37 |    0.02 |        - |       - |   10.36 KB |        0.25 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,477.40 μs** |    **723.30 μs** |  **39.647 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **421.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    628.33 μs |    645.42 μs |  35.378 μs |  0.43 |    0.02 |        - |       - |   64.43 KB |        0.15 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    201.63 μs |    415.39 μs |  22.769 μs |     ? |       ? |   0.9766 |       - |  102.18 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,288.52 μs** |  **1,388.35 μs** |  **76.100 μs** |  **1.00** |    **0.04** |  **70.3125** |       **-** | **1225.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,975.89 μs |  2,863.14 μs | 156.938 μs |  0.86 |    0.06 |   7.8125 |       - |    1050 KB |        0.86 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,510.32 μs** |     **85.79 μs** |   **4.703 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,189.40 μs |  1,093.90 μs |  59.960 μs |  0.22 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,507.71 μs** |     **68.83 μs** |   **3.773 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.45 μs |     84.48 μs |   4.631 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,505.67 μs** |     **38.61 μs** |   **2.116 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,124.28 μs |    228.45 μs |  12.522 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,508.22 μs** |     **84.06 μs** |   **4.608 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.81 μs |     36.78 μs |   2.016 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.566 ms** | **12.578 ms** | **0.6894 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.375 ms | 40.263 ms | 2.2069 ms | 0.005 |  594.26 KB |        7.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.519 ms** | **10.901 ms** | **0.5975 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.394 ms | 41.939 ms | 2.2988 ms | 0.005 |  774.98 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.149 ms** | **44.415 ms** | **2.4345 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.617 ms | 98.646 ms | 5.4071 ms | 0.006 |  995.79 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.045 ms** | **23.069 ms** | **1.2645 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.801 ms | 18.114 ms | 0.9929 ms | 0.005 | 2761.51 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.880 ms** | **43.037 ms** | **2.3590 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.903 ms | 19.707 ms | 1.0802 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.144 ms** | **24.332 ms** | **1.3337 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.450 ms | 22.945 ms | 1.2577 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.285 ms** | **23.611 ms** | **1.2942 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.345 ms | 19.989 ms | 1.0957 ms | 0.002 |  184.69 KB |       76.75 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.913 ms** | **16.229 ms** | **0.8896 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.368 ms |  6.893 ms | 0.3778 ms | 0.002 |  258.99 KB |       61.96 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.326 μs |   3.110 μs |  0.1705 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.794 μs |   5.796 μs |  0.3177 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.381 μs |   4.936 μs |  0.2706 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 39.493 μs | 202.396 μs | 11.0940 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.002 μs |  59.998 μs |  3.2887 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.957 μs |  83.521 μs |  4.5781 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.068 μs |  19.019 μs |  1.0425 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 18.970 μs |  21.634 μs |  1.1858 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.596 μs |   9.439 μs |  0.5174 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.476 μs |  13.536 μs |  0.7419 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,578.7 ns |  4,217.2 ns | 231.2 ns |  0.36 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,291.0 ns |  2,935.3 ns | 160.9 ns |  0.30 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,240.3 ns |  3,108.6 ns | 170.4 ns |  0.28 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,488.5 ns |  2,919.0 ns | 160.0 ns |  0.57 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    725.8 ns |  2,113.5 ns | 115.8 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,192.3 ns | 16,649.7 ns | 912.6 ns |  8.07 |    0.79 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,399.3 ns |  9,261.9 ns | 507.7 ns |  1.01 |    0.14 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,686.0 ns | 10,842.2 ns | 594.3 ns |  0.85 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.820 μs |  11.615 μs |  0.6367 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   565.841 μs | 490.985 μs | 26.9125 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.616 μs |   4.339 μs |  0.2378 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,562.479 μs | 482.725 μs | 26.4598 μs |    1280 B |


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