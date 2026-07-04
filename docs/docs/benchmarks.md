---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 07:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,143.91 μs** |   **865.42 μs** |  **47.437 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,458.94 μs | 2,643.83 μs | 144.918 μs |  0.24 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,403.61 μs** |   **739.37 μs** |  **40.528 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,330.12 μs |   221.67 μs |  12.151 μs |  0.31 |    0.00 |  15.6250 |       - |  339.73 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,229.00 μs** | **1,449.61 μs** |  **79.458 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.06 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,271.49 μs |   766.10 μs |  41.993 μs |  0.20 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,754.00 μs** | **2,008.72 μs** | **110.105 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,996.23 μs | 9,074.15 μs | 497.385 μs |  0.55 |    0.03 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.76 μs** |    **51.54 μs** |   **2.825 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.84 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.43 μs |    82.96 μs |   4.547 μs |  0.45 |    0.03 |        - |       - |    9.71 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,437.21 μs** |   **345.93 μs** |  **18.962 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **419.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    585.75 μs |   216.73 μs |  11.880 μs |  0.41 |    0.01 |        - |       - |  113.22 KB |        0.27 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    210.71 μs |   297.52 μs |  16.308 μs |     ? |       ? |   0.9766 |       - |  107.85 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,001.92 μs | 1,360.90 μs |  74.596 μs |     ? |       ? |   7.8125 |       - | 1031.91 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,436.09 μs** |   **476.17 μs** |  **26.101 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.55 μs |    75.54 μs |   4.141 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.59 μs** |    **39.73 μs** |   **2.178 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,115.05 μs |    16.24 μs |   0.890 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,426.05 μs** |   **140.65 μs** |   **7.710 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,120.80 μs |    12.98 μs |   0.711 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,425.74 μs** |    **62.96 μs** |   **3.451 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,164.82 μs |   229.48 μs |  12.579 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.191 ms** |  **58.271 ms** |  **3.1940 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.307 ms |  18.140 ms |  0.9943 ms | 0.005 |  601.73 KB |        8.06 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.347 ms** |  **19.233 ms** |  **1.0542 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.339 ms |  12.496 ms |  0.6849 ms | 0.005 |  780.81 KB |        3.12 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.612 ms** |  **16.966 ms** |  **0.9300 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    22.595 ms | 173.685 ms |  9.5202 ms | 0.007 |  995.79 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.358 ms** |  **22.096 ms** |  **1.2112 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.645 ms |  24.286 ms |  1.3312 ms | 0.005 | 2760.37 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.146 ms** |  **12.833 ms** |  **0.7034 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.494 ms |  12.887 ms |  0.7064 ms | 0.002 |   193.4 KB |       80.37 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.563 ms** |  **17.846 ms** |  **0.9782 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.083 ms |  16.360 ms |  0.8967 ms | 0.002 |  191.35 KB |       45.95 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,139.807 ms** | **528.704 ms** | **28.9801 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.517 ms |   9.279 ms |  0.5086 ms | 0.002 |  184.59 KB |       76.71 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.261 ms** |  **28.091 ms** |  **1.5398 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.081 ms |  19.946 ms |  1.0933 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 12.035 μs |  2.114 μs | 0.1159 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  7.573 μs |  4.940 μs | 0.2708 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.090 μs | 10.920 μs | 0.5986 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.285 μs |  1.563 μs | 0.0857 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  6.951 μs |  1.377 μs | 0.0755 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 16.321 μs | 14.287 μs | 0.7831 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 14.428 μs | 14.442 μs | 0.7916 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 16.088 μs |  5.325 μs | 0.2919 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.445 μs |  8.866 μs | 0.4860 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.216 μs |  7.428 μs | 0.4071 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,318.0 ns | 2,496.6 ns | 136.85 ns |  0.42 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,181.5 ns |   657.8 ns |  36.06 ns |  0.37 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,160.7 ns | 3,023.7 ns | 165.74 ns |  0.37 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,203.3 ns | 3,819.9 ns | 209.38 ns |  0.70 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    888.7 ns | 1,037.4 ns |  56.86 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 28,563.0 ns | 9,425.7 ns | 516.65 ns |  9.04 |    0.65 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,175.3 ns | 4,929.5 ns | 270.20 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,290.7 ns | 3,806.8 ns | 208.66 ns |  1.04 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     9.675 μs |   9.991 μs |  0.5476 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   416.045 μs | 414.740 μs | 22.7333 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     6.659 μs |   5.531 μs |  0.3032 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,187.899 μs | 389.098 μs | 21.3278 μs |    1280 B |


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