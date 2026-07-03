---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 21:36 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,313.89 μs** |   **164.18 μs** |   **8.999 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,385.79 μs | 1,245.49 μs |  68.270 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,467.66 μs** |   **516.00 μs** |  **28.284 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,389.62 μs | 1,498.09 μs |  82.116 μs |  0.32 |    0.01 |  15.6250 |       - |  339.35 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,124.97 μs** |   **816.08 μs** |  **44.732 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,468.67 μs | 2,488.53 μs | 136.405 μs |  0.24 |    0.02 |        - |       - |   36.33 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,124.64 μs** | **2,416.37 μs** | **132.449 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,807.43 μs | 6,811.03 μs | 373.335 μs |  0.52 |    0.03 |  15.6250 |       - |  361.88 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.42 μs** |    **73.28 μs** |   **4.017 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.26 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.86 μs |   116.56 μs |   6.389 μs |  0.47 |    0.04 |        - |       - |    10.2 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,440.43 μs** |   **506.72 μs** |  **27.775 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **420.39 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    601.88 μs |   450.72 μs |  24.706 μs |  0.42 |    0.02 |        - |       - |   79.35 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    214.38 μs |   295.22 μs |  16.182 μs |     ? |       ? |   0.9766 |       - |  102.28 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,043.67 μs | 4,669.12 μs | 255.930 μs |     ? |       ? |   7.8125 |       - |     997 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,468.02 μs** |   **287.05 μs** |  **15.734 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,359.86 μs |    45.65 μs |   2.502 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,449.06 μs** |    **77.81 μs** |   **4.265 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,353.56 μs |   140.53 μs |   7.703 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,463.48 μs** |   **161.39 μs** |   **8.846 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,322.12 μs |   146.42 μs |   8.026 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,470.43 μs** |    **90.60 μs** |   **4.966 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,341.36 μs |   404.80 μs |  22.189 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.310 ms** |  **12.714 ms** | **0.6969 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.185 ms |  53.921 ms | 2.9556 ms | 0.006 | 592.23 KB |        7.94 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,169.248 ms** |  **69.387 ms** | **3.8034 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.777 ms |  25.029 ms | 1.3719 ms | 0.005 | 775.74 KB |        3.10 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.295 ms** |  **30.731 ms** | **1.6845 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21.692 ms | 114.243 ms | 6.2620 ms | 0.007 | 996.37 KB |        1.66 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.512 ms** |   **6.828 ms** | **0.3743 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.513 ms |  28.960 ms | 1.5874 ms | 0.006 | 2761.7 KB |        1.17 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.389 ms** |  **12.061 ms** | **0.6611 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.089 ms |  36.226 ms | 1.9857 ms | 0.002 | 184.38 KB |       76.62 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.621 ms** |  **22.140 ms** | **1.2136 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.285 ms |   9.198 ms | 0.5042 ms | 0.002 | 186.35 KB |       44.75 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.808 ms** |  **12.738 ms** | **0.6982 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.927 ms |  34.334 ms | 1.8819 ms | 0.003 | 256.68 KB |      106.67 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.594 ms** |  **10.884 ms** | **0.5966 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.287 ms |  13.359 ms | 0.7322 ms | 0.002 | 188.34 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.652 μs |  2.930 μs | 0.1606 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.235 μs |  3.863 μs | 0.2117 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.383 μs |  2.640 μs | 0.1447 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.054 μs |  5.668 μs | 0.3107 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.795 μs | 71.103 μs | 3.8974 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.403 μs |  1.040 μs | 0.0570 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.011 μs | 14.303 μs | 0.7840 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.542 μs | 10.528 μs | 0.5771 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.745 μs |  2.930 μs | 0.1606 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.510 μs |  7.622 μs | 0.4178 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,451.2 ns |  4,869.4 ns |   266.9 ns |  1,317.5 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,419.3 ns |  2,111.9 ns |   115.8 ns |  1,353.0 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,460.8 ns |  3,793.3 ns |   207.9 ns |  1,377.5 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,541.2 ns | 26,282.4 ns | 1,440.6 ns |  2,729.5 ns |  0.77 |    0.28 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    907.7 ns |  5,143.8 ns |   281.9 ns |    770.0 ns |  0.20 |    0.06 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,705.0 ns |  4,594.3 ns |   251.8 ns | 40,602.0 ns |  8.87 |    0.61 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,606.8 ns |  6,483.2 ns |   355.4 ns |  4,674.5 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,914.3 ns |  3,676.0 ns |   201.5 ns |  3,858.0 ns |  0.85 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.361 μs |  15.368 μs |  0.8424 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   541.395 μs | 352.783 μs | 19.3372 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.446 μs |  27.484 μs |  1.5065 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,754.486 μs | 834.889 μs | 45.7631 μs |    1280 B |


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