---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 11:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,185.91 μs** |   **637.34 μs** |  **34.935 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,352.18 μs | 1,119.86 μs |  61.383 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,468.34 μs** |   **472.49 μs** |  **25.899 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,331.45 μs |   542.80 μs |  29.753 μs |  0.31 |    0.00 |  15.6250 |       - |  339.45 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,197.67 μs** |   **736.92 μs** |  **40.393 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,416.65 μs | 1,126.64 μs |  61.755 μs |  0.23 |    0.01 |        - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,799.82 μs** | **1,948.19 μs** | **106.787 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,750.92 μs | 3,551.18 μs | 194.652 μs |  0.53 |    0.01 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.74 μs** |    **34.46 μs** |   **1.889 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.26 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.46 μs |    25.99 μs |   1.425 μs |  0.50 |    0.01 |        - |       - |    8.75 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,430.44 μs** |    **54.48 μs** |   **2.986 μs** |  **1.00** |    **0.00** |  **23.4375** |       **-** |  **417.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    599.91 μs |   320.57 μs |  17.572 μs |  0.42 |    0.01 |        - |       - |   71.69 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    253.02 μs | 1,707.05 μs |  93.569 μs |     ? |       ? |   0.4883 |       - |    11.8 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,080.26 μs | 2,904.42 μs | 159.201 μs |     ? |       ? |   7.8125 |       - | 1015.22 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,436.31 μs** |    **55.84 μs** |   **3.061 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,172.37 μs |   290.89 μs |  15.945 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,442.49 μs** |   **112.17 μs** |   **6.148 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,198.85 μs |   293.85 μs |  16.107 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,440.08 μs** |   **101.31 μs** |   **5.553 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,288.72 μs |   204.80 μs |  11.226 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,449.73 μs** |   **279.13 μs** |  **15.300 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,321.54 μs |   309.81 μs |  16.982 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.653 ms** | **23.6317 ms** | **1.2953 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.976 ms | 27.6153 ms | 1.5137 ms | 0.005 |  594.45 KB |        7.97 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.652 ms** |  **8.9628 ms** | **0.4913 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.372 ms | 32.9024 ms | 1.8035 ms | 0.005 |  781.34 KB |        3.12 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.282 ms** | **12.7726 ms** | **0.7001 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.351 ms |  5.3828 ms | 0.2950 ms | 0.005 |   994.6 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.557 ms** |  **4.6086 ms** | **0.2526 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.142 ms | 47.9263 ms | 2.6270 ms | 0.005 | 2762.63 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.605 ms** | **29.9237 ms** | **1.6402 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.752 ms | 26.5639 ms | 1.4561 ms | 0.002 |  188.87 KB |       78.49 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.872 ms** | **28.3682 ms** | **1.5550 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.693 ms |  8.3839 ms | 0.4595 ms | 0.002 |  192.29 KB |       46.18 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.657 ms** |  **0.6624 ms** | **0.0363 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.169 ms | 19.4262 ms | 1.0648 ms | 0.002 |   184.8 KB |       76.80 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.540 ms** | **21.7973 ms** | **1.1948 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.507 ms | 10.5750 ms | 0.5797 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 11.901 μs |  1.4171 μs | 0.0777 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  7.626 μs |  2.9457 μs | 0.1615 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.683 μs |  3.7796 μs | 0.2072 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.387 μs |  4.0446 μs | 0.2217 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.883 μs |  2.3458 μs | 0.1286 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 16.698 μs |  0.6948 μs | 0.0381 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 13.584 μs | 19.9449 μs | 1.0932 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 18.241 μs | 21.7198 μs | 1.1905 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.726 μs |  1.2771 μs | 0.0700 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.440 μs | 10.9949 μs | 0.6027 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,042.0 ns |  2,724.4 ns |   149.33 ns |  0.23 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |    980.3 ns |  1,750.9 ns |    95.97 ns |  0.21 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,082.0 ns |  3,117.5 ns |   170.88 ns |  0.24 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,919.0 ns |  3,384.5 ns |   185.52 ns |  0.42 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    565.0 ns |    906.1 ns |    49.67 ns |  0.12 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 28,396.3 ns | 12,782.1 ns |   700.63 ns |  6.17 |    1.25 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,786.8 ns | 21,714.9 ns | 1,190.27 ns |  1.04 |    0.31 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,374.7 ns |  9,651.3 ns |   529.02 ns |  0.73 |    0.18 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     9.411 μs |   6.330 μs | 0.3470 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   400.531 μs |  78.702 μs | 4.3139 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     7.384 μs |  11.049 μs | 0.6056 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,172.256 μs | 165.782 μs | 9.0871 μs |    1280 B |


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