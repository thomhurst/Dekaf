---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 00:09 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,113.95 μs** |   **644.81 μs** |  **35.344 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,420.13 μs | 2,749.06 μs | 150.685 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,331.90 μs** |   **619.07 μs** |  **33.933 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,346.59 μs |   629.47 μs |  34.503 μs |  0.32 |    0.00 |  15.6250 |       - |  339.58 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,428.44 μs** |   **216.91 μs** |  **11.890 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,191.76 μs | 1,468.68 μs |  80.503 μs |  0.19 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,746.65 μs** | **5,376.69 μs** | **294.715 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,444.50 μs | 5,754.47 μs | 315.422 μs |  0.51 |    0.02 |  15.6250 |       - |  361.68 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.23 μs** |    **73.73 μs** |   **4.042 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **41.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     50.20 μs |    51.05 μs |   2.798 μs |  0.36 |    0.02 |        - |       - |    9.08 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,416.51 μs** |   **638.27 μs** |  **34.986 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |   **425.9 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    627.15 μs |   510.76 μs |  27.997 μs |  0.44 |    0.02 |        - |       - |   55.78 KB |        0.13 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    204.14 μs |   115.85 μs |   6.350 μs |     ? |       ? |   0.9766 |       - |   96.68 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,048.50 μs | 2,099.84 μs | 115.100 μs |     ? |       ? |   7.8125 |       - | 1017.07 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.22 μs** |    **41.34 μs** |   **2.266 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,351.07 μs |   461.94 μs |  25.321 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.04 μs** |    **70.20 μs** |   **3.848 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,346.62 μs |   255.50 μs |  14.005 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,415.25 μs** |   **177.83 μs** |   **9.747 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,230.81 μs |   364.57 μs |  19.983 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.00 μs** |    **58.13 μs** |   **3.186 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,112.55 μs |    52.46 μs |   2.875 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.602 ms** |   **8.952 ms** | **0.4907 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.396 ms |  40.909 ms | 2.2424 ms | 0.005 |  596.76 KB |        8.00 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.769 ms** |  **65.809 ms** | **3.6072 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.301 ms |  21.324 ms | 1.1689 ms | 0.004 |  781.76 KB |        3.12 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.520 ms** |  **29.985 ms** | **1.6436 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.287 ms | 111.537 ms | 6.1137 ms | 0.006 |  996.66 KB |        1.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.709 ms** |   **6.520 ms** | **0.3574 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.551 ms |  27.762 ms | 1.5217 ms | 0.005 | 2760.34 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.660 ms** |  **42.270 ms** | **2.3170 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.360 ms |  17.773 ms | 0.9742 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.163 ms** |   **6.411 ms** | **0.3514 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.629 ms |  10.699 ms | 0.5865 ms | 0.002 |  186.39 KB |       44.76 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.613 ms** |  **35.899 ms** | **1.9678 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.466 ms |  14.350 ms | 0.7866 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.487 ms** |  **37.702 ms** | **2.0665 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.596 ms |  39.796 ms | 2.1813 ms | 0.002 |  187.09 KB |       44.76 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.225 μs |   2.1765 μs |  0.1193 μs | 15.188 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.515 μs |   3.6331 μs |  0.1991 μs |  9.448 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.311 μs |   3.2235 μs |  0.1767 μs | 10.335 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.768 μs |   3.3158 μs |  0.1818 μs | 26.711 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.911 μs |   1.3025 μs |  0.0714 μs |  8.898 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.211 μs |   0.9620 μs |  0.0527 μs | 19.230 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.230 μs |   6.3832 μs |  0.3499 μs | 18.043 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.332 μs |   5.2165 μs |  0.2859 μs | 20.439 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 | 10.994 μs | 204.3038 μs | 11.1986 μs |  4.539 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.630 μs |   6.8779 μs |  0.3770 μs | 10.790 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,419.8 ns | 21,427.7 ns | 1,174.52 ns |  1,828.5 ns |  0.55 |    0.23 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,362.0 ns |  1,196.3 ns |    65.57 ns |  1,352.0 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,340.0 ns |    927.9 ns |    50.86 ns |  1,333.0 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,521.7 ns |    862.2 ns |    47.26 ns |  2,505.0 ns |  0.57 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    717.3 ns |  2,754.2 ns |   150.96 ns |    731.0 ns |  0.16 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,405.3 ns | 10,928.8 ns |   599.04 ns | 41,469.0 ns |  9.39 |    0.41 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,415.0 ns |  4,029.6 ns |   220.88 ns |  4,308.0 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,040.0 ns |  2,930.2 ns |   160.61 ns |  4,047.0 ns |  0.92 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.34 μs |   7.710 μs |  0.423 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   523.24 μs | 278.967 μs | 15.291 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.25 μs |  11.267 μs |  0.618 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,693.14 μs | 152.016 μs |  8.332 μs |    1280 B |


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