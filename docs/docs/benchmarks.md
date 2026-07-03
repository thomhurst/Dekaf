---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 09:17 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,933.94 μs** |    **239.55 μs** |  **13.131 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,193.25 μs |    302.82 μs |  16.599 μs |  0.20 |    0.00 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,010.78 μs** |    **620.26 μs** |  **33.999 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,218.18 μs |    736.50 μs |  40.370 μs |  0.32 |    0.01 |  19.5313 |  3.9063 |  339.77 KB |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,673.68 μs** |  **3,928.26 μs** | **215.321 μs** |  **1.00** |    **0.04** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,098.90 μs |    178.44 μs |   9.781 μs |  0.16 |    0.00 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,814.03 μs** |  **8,171.08 μs** | **447.884 μs** |  **1.00** |    **0.06** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,878.27 μs | 16,407.36 μs | 899.343 μs |  0.60 |    0.08 |  15.6250 |       - |  369.57 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    49.54 μs |    109.57 μs |   6.006 μs |     ? |       ? |   0.4883 |  0.2441 |   16.73 KB |           ? |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,024.46 μs** |  **1,434.01 μs** |  **78.603 μs** |  **1.00** |    **0.10** |  **25.3906** |       **-** |  **418.31 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   589.08 μs |  1,100.31 μs |  60.312 μs |  0.58 |    0.06 |   3.9063 |       - |   83.12 KB |        0.20 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   273.68 μs |    232.22 μs |  12.729 μs |     ? |       ? |   0.9766 |  0.4883 |   26.31 KB |           ? |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,742.76 μs |  2,351.00 μs | 128.866 μs |     ? |       ? |  78.1250 | 70.3125 | 2358.69 KB |           ? |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,428.34 μs** |  **2,541.73 μs** | **139.321 μs** |  **1.00** |    **0.03** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,307.94 μs |    205.55 μs |  11.267 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,348.03 μs** |     **14.05 μs** |   **0.770 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,258.87 μs |  1,595.31 μs |  87.444 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,394.28 μs** |  **1,535.02 μs** |  **84.140 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,148.78 μs |    916.67 μs |  50.246 μs |  0.21 |    0.01 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,343.10 μs** |     **14.88 μs** |   **0.816 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,325.69 μs |    452.85 μs |  24.822 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165.823 ms** |  **9.2119 ms** | **0.5049 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    12.105 ms | 33.2403 ms | 1.8220 ms | 0.004 |  406.93 KB |        5.45 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.181 ms** |  **1.9880 ms** | **0.1090 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.903 ms | 25.3780 ms | 1.3911 ms | 0.004 |  601.27 KB |        2.40 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.067 ms** |  **3.2074 ms** | **0.1758 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13.227 ms | 23.6322 ms | 1.2954 ms | 0.004 |  810.98 KB |        1.35 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.511 ms** | **11.4206 ms** | **0.6260 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    12.442 ms | 31.9060 ms | 1.7489 ms | 0.004 | 2575.64 KB |        1.09 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.332 ms** | **20.4033 ms** | **1.1184 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.475 ms | 13.3739 ms | 0.7331 ms | 0.002 |   183.8 KB |       76.39 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.612 ms** | **12.9254 ms** | **0.7085 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.919 ms |  3.0441 ms | 0.1669 ms | 0.002 |  183.19 KB |       43.99 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.323 ms** | **17.8550 ms** | **0.9787 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.420 ms | 13.4317 ms | 0.7362 ms | 0.002 |  183.73 KB |       76.36 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.867 ms** |  **0.4608 ms** | **0.0253 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.730 ms |  9.9321 ms | 0.5444 ms | 0.002 |  255.83 KB |       61.21 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.660 μs |   2.1302 μs |  0.1168 μs | 14.683 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.535 μs |   2.4372 μs |  0.1336 μs |  9.568 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.099 μs |   1.0142 μs |  0.0556 μs | 10.089 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 36.028 μs |  42.1068 μs |  2.3080 μs | 34.736 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.977 μs |   0.9585 μs |  0.0525 μs |  8.947 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.383 μs |   1.7719 μs |  0.0971 μs | 19.406 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 28.566 μs | 233.2748 μs | 12.7866 μs | 21.189 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.846 μs |  11.2255 μs |  0.6153 μs |  4.629 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.540 μs |   1.2875 μs |  0.0706 μs | 10.509 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,273.0 ns |     657.8 ns |    36.06 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,299.3 ns |     276.9 ns |    15.18 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,482.3 ns |   1,188.0 ns |    65.12 ns |  0.37 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,524.7 ns |     738.9 ns |    40.50 ns |  0.63 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    765.3 ns |     105.3 ns |     5.77 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 43,889.0 ns | 162,475.9 ns | 8,905.85 ns | 10.89 |    1.94 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,034.5 ns |   2,345.4 ns |   128.56 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,857.7 ns |   1,859.6 ns |   101.93 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.06 μs |   1.215 μs |  0.067 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   529.86 μs | 425.021 μs | 23.297 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.11 μs |   3.172 μs |  0.174 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,663.17 μs | 488.217 μs | 26.761 μs |    1280 B |


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