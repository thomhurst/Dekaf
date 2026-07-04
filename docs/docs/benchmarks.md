---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 01:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,098.41 μs** |   **505.24 μs** |  **27.694 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,328.55 μs |   705.69 μs |  38.681 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,174.21 μs** |   **771.48 μs** |  **42.287 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,326.71 μs |   518.04 μs |  28.396 μs |  0.32 |    0.00 |  15.6250 |       - |  339.53 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,622.63 μs** |   **676.12 μs** |  **37.060 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,157.39 μs |   870.96 μs |  47.740 μs |  0.17 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,730.47 μs** | **5,596.89 μs** | **306.784 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,741.11 μs | 1,747.65 μs |  95.795 μs |  0.49 |    0.01 |  15.6250 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.34 μs** |   **226.11 μs** |  **12.394 μs** |  **1.00** |    **0.10** |   **2.4414** |       **-** |   **40.65 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.69 μs |    78.72 μs |   4.315 μs |  0.39 |    0.04 |   0.2441 |       - |    9.22 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,369.34 μs** | **1,080.66 μs** |  **59.234 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **419.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    610.62 μs |   427.73 μs |  23.445 μs |  0.45 |    0.02 |        - |       - |    73.5 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.49 μs |   117.17 μs |   6.422 μs |     ? |       ? |   0.4883 |       - |   27.58 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,949.27 μs | 3,025.42 μs | 165.833 μs |     ? |       ? |   7.8125 |       - | 1013.21 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,403.21 μs** |    **46.70 μs** |   **2.560 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,104.30 μs |    27.29 μs |   1.496 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,404.62 μs** |    **65.61 μs** |   **3.596 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,355.82 μs |   122.16 μs |   6.696 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,413.12 μs** |    **59.37 μs** |   **3.254 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,291.02 μs |   202.09 μs |  11.077 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,418.10 μs** |   **226.83 μs** |  **12.433 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,108.72 μs |   134.56 μs |   7.376 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.344 ms** | **23.644 ms** | **1.2960 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.179 ms | 24.239 ms | 1.3286 ms | 0.005 |     597 KB |        8.00 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.841 ms** | **76.206 ms** | **4.1771 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.117 ms | 37.633 ms | 2.0628 ms | 0.005 |  778.08 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.524 ms** | **12.797 ms** | **0.7015 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.824 ms | 29.483 ms | 1.6160 ms | 0.005 |  996.69 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.533 ms** | **33.085 ms** | **1.8135 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.457 ms | 41.308 ms | 2.2642 ms | 0.005 | 2761.41 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.435 ms** | **10.778 ms** | **0.5908 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.706 ms | 57.139 ms | 3.1320 ms | 0.002 |  188.02 KB |       78.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.503 ms** |  **2.669 ms** | **0.1463 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.023 ms | 38.261 ms | 2.0972 ms | 0.002 |  187.62 KB |       45.06 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.695 ms** | **12.506 ms** | **0.6855 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.353 ms |  8.557 ms | 0.4690 ms | 0.002 |  184.59 KB |       76.71 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.064 ms** | **22.039 ms** | **1.2080 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.206 ms |  7.686 ms | 0.4213 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.577 μs |  8.634 μs | 0.4733 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.718 μs |  5.546 μs | 0.3040 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.910 μs |  9.978 μs | 0.5469 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.781 μs | 33.230 μs | 1.8215 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.780 μs |  3.878 μs | 0.2126 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.642 μs | 11.356 μs | 0.6225 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.205 μs | 24.518 μs | 1.3439 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.483 μs | 24.728 μs | 1.3554 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.877 μs | 18.097 μs | 0.9920 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.143 μs | 20.497 μs | 1.1235 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,493.5 ns |  2,668.6 ns | 146.3 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,256.5 ns |  2,867.2 ns | 157.2 ns |  0.28 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,247.0 ns |  2,832.2 ns | 155.2 ns |  0.28 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,450.3 ns |  3,444.2 ns | 188.8 ns |  0.54 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    522.7 ns |  4,068.5 ns | 223.0 ns |  0.12 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,727.3 ns |  9,648.2 ns | 528.9 ns |  7.91 |    0.97 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,582.7 ns | 12,443.4 ns | 682.1 ns |  1.01 |    0.18 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,022.5 ns |  8,618.6 ns | 472.4 ns |  0.89 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.23 μs |  17.38 μs |  0.953 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   516.39 μs | 127.23 μs |  6.974 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.42 μs |  20.81 μs |  1.141 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,566.30 μs | 410.18 μs | 22.484 μs |    1280 B |


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