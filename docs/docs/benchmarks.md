---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 14:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,233.91 μs** | **1,608.92 μs** |  **88.190 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,316.19 μs |   491.02 μs |  26.915 μs |  0.21 |    0.00 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,319.50 μs** |    **69.98 μs** |   **3.836 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,358.65 μs |   836.34 μs |  45.843 μs |  0.32 |    0.01 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,330.53 μs** |   **515.14 μs** |  **28.236 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,305.24 μs |   644.13 μs |  35.307 μs |  0.21 |    0.00 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,276.95 μs** | **1,759.57 μs** |  **96.448 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,576.40 μs | 3,546.03 μs | 194.370 μs |  0.54 |    0.01 |  15.6250 |       - |  361.83 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **142.87 μs** |   **112.87 μs** |   **6.187 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.96 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.97 μs |    39.94 μs |   2.189 μs |  0.44 |    0.02 |        - |       - |    8.46 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,655.27 μs** | **2,185.29 μs** | **119.783 μs** |  **1.00** |    **0.09** |  **25.3906** |       **-** |   **431.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    613.42 μs |   389.03 μs |  21.324 μs |  0.37 |    0.03 |        - |       - |   98.97 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    195.13 μs |   308.23 μs |  16.895 μs |     ? |       ? |   0.9766 |       - |  102.65 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,108.52 μs | 1,437.59 μs |  78.799 μs |     ? |       ? |   7.8125 |       - |  981.36 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,415.03 μs** |    **19.54 μs** |   **1.071 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,367.11 μs |   583.77 μs |  31.998 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,410.74 μs** |   **101.09 μs** |   **5.541 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,379.45 μs |   293.77 μs |  16.103 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,431.15 μs** |   **429.31 μs** |  **23.532 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,302.94 μs |   193.05 μs |  10.582 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,419.08 μs** |   **116.30 μs** |   **6.375 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.05 μs |    93.05 μs |   5.100 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.228 ms** | **10.442 ms** | **0.5724 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.260 ms | 35.625 ms | 1.9527 ms | 0.005 |  591.96 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.519 ms** | **20.869 ms** | **1.1439 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.414 ms | 14.049 ms | 0.7701 ms | 0.004 |  788.23 KB |        3.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.958 ms** | **16.208 ms** | **0.8884 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.182 ms | 84.416 ms | 4.6271 ms | 0.006 |  995.62 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.287 ms** | **25.608 ms** | **1.4036 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.547 ms | 29.181 ms | 1.5995 ms | 0.005 | 2858.06 KB |        1.21 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.166 ms** | **37.660 ms** | **2.0643 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.397 ms | 13.687 ms | 0.7502 ms | 0.002 |  185.52 KB |       77.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.202 ms** | **37.841 ms** | **2.0742 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.013 ms | 33.921 ms | 1.8593 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.555 ms** | **29.282 ms** | **1.6051 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.778 ms | 11.644 ms | 0.6382 ms | 0.002 |  184.84 KB |       76.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.360 ms** | **36.062 ms** | **1.9767 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.167 ms | 31.027 ms | 1.7007 ms | 0.002 |  209.55 KB |       50.13 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.134 μs | 100.6748 μs | 5.5183 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.361 μs |   1.7741 μs | 0.0972 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.719 μs |  28.6868 μs | 1.5724 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.797 μs |   6.0841 μs | 0.3335 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.954 μs |   0.7373 μs | 0.0404 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.293 μs |   2.7475 μs | 0.1506 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.284 μs |   8.6837 μs | 0.4760 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.455 μs |  10.1891 μs | 0.5585 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.515 μs |   1.0048 μs | 0.0551 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.370 μs |   4.9251 μs | 0.2700 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,553.3 ns |  2,658.9 ns | 145.74 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,515.7 ns |  5,336.7 ns | 292.52 ns |  0.34 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,400.0 ns |    641.7 ns |  35.17 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,332.3 ns | 10,218.5 ns | 560.11 ns |  0.75 |    0.11 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    764.0 ns |  2,006.6 ns | 109.99 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,738.7 ns |  5,326.4 ns | 291.96 ns |  8.90 |    0.44 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,476.8 ns |  4,516.4 ns | 247.56 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,883.7 ns |  2,138.0 ns | 117.19 ns |  0.87 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.023 μs |   1.691 μs |  0.0927 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   569.875 μs | 505.716 μs | 27.7200 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.593 μs |   1.654 μs |  0.0907 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,806.167 μs | 171.599 μs |  9.4059 μs |    1280 B |


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