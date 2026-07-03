---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 16:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,142.24 μs** |   **384.22 μs** |  **21.060 μs** |  **1.00** |    **0.00** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,374.27 μs | 1,465.34 μs |  80.320 μs |  0.22 |    0.01 |        - |        - |   35.04 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,357.63 μs** |   **801.38 μs** |  **43.926 μs** |  **1.00** |    **0.01** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,278.28 μs |   269.67 μs |  14.782 μs |  0.31 |    0.00 |  19.5313 |   3.9063 |  340.06 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,156.62 μs** | **1,138.29 μs** |  **62.394 μs** |  **1.00** |    **0.01** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,336.46 μs | 1,192.64 μs |  65.372 μs |  0.22 |    0.01 |        - |        - |   37.23 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,838.84 μs** |   **561.39 μs** |  **30.772 μs** |  **1.00** |    **0.00** | **109.3750** |  **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,933.30 μs | 6,394.26 μs | 350.491 μs |  0.54 |    0.02 |  15.6250 |        - |  372.19 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.09 μs** |    **13.86 μs** |   **0.760 μs** |  **1.00** |    **0.01** |   **2.4414** |        **-** |   **42.32 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     75.66 μs |   161.05 μs |   8.828 μs |  0.53 |    0.05 |   0.9766 |   0.4883 |   19.53 KB |        0.46 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,506.21 μs** |   **756.91 μs** |  **41.489 μs** |  **1.00** |    **0.03** |  **25.3906** |        **-** |  **418.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    812.22 μs |   306.83 μs |  16.818 μs |  0.54 |    0.02 |   3.9063 |        - |  141.83 KB |        0.34 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    362.61 μs |   198.87 μs |  10.900 μs |     ? |       ? |  12.6953 |  11.7188 |     269 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,287.48 μs** |   **261.45 μs** |  **14.331 μs** |  **1.00** |    **0.01** |  **70.3125** |        **-** | **1225.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,647.19 μs | 5,548.68 μs | 304.142 μs |  1.59 |    0.12 | 117.1875 | 109.3750 | 2511.24 KB |        2.05 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,424.36 μs** |    **80.17 μs** |   **4.395 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,398.70 μs |    20.87 μs |   1.144 μs |  0.26 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,434.69 μs** |   **296.27 μs** |  **16.239 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,378.62 μs |   185.83 μs |  10.186 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,438.91 μs** |   **261.67 μs** |  **14.343 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,365.11 μs |   404.20 μs |  22.155 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.24 μs** |   **105.01 μs** |   **5.756 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,315.92 μs |   215.72 μs |  11.824 μs |  0.24 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.258 ms** |  **35.596 ms** | **1.9512 ms** | **3,168.050 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.682 ms |  57.663 ms | 3.1607 ms |    14.678 ms | 0.005 |  412.74 KB |        5.53 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.841 ms** |  **11.732 ms** | **0.6430 ms** | **3,165.049 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.547 ms |  42.609 ms | 2.3356 ms |    13.573 ms | 0.004 |  589.92 KB |        2.36 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.231 ms** |  **33.559 ms** | **1.8395 ms** | **3,166.229 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.539 ms | 164.632 ms | 9.0240 ms |    13.758 ms | 0.006 |  808.77 KB |        1.34 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.143 ms** |  **12.426 ms** | **0.6811 ms** | **3,165.130 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.430 ms |  11.956 ms | 0.6554 ms |    14.217 ms | 0.005 | 2577.74 KB |        1.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.862 ms** |   **8.482 ms** | **0.4649 ms** | **3,156.981 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.645 ms |   5.633 ms | 0.3088 ms |     5.698 ms | 0.002 |  194.92 KB |       81.01 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.723 ms** |  **35.817 ms** | **1.9633 ms** | **3,156.533 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.427 ms |   8.561 ms | 0.4693 ms |     5.159 ms | 0.002 |  184.59 KB |       44.33 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.035 ms** |   **7.730 ms** | **0.4237 ms** | **3,157.211 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.853 ms |  14.225 ms | 0.7797 ms |     6.832 ms | 0.002 |  199.52 KB |       82.92 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.007 ms** |  **12.096 ms** | **0.6630 ms** | **3,157.219 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.444 ms |  14.519 ms | 0.7958 ms |     6.023 ms | 0.002 |   183.8 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 20.970 μs | 99.641 μs | 5.4617 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.217 μs |  2.396 μs | 0.1313 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.316 μs |  1.425 μs | 0.0781 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.245 μs | 81.660 μs | 4.4761 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.936 μs |  2.020 μs | 0.1107 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.399 μs |  2.018 μs | 0.1106 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.803 μs | 31.528 μs | 1.7282 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.075 μs |  8.194 μs | 0.4492 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.789 μs |  6.551 μs | 0.3591 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.677 μs |  5.956 μs | 0.3265 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,398.3 ns |   2,140.1 ns |    117.30 ns |  1,422.0 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,289.7 ns |   1,158.6 ns |     63.51 ns |  1,253.0 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,324.5 ns |     590.3 ns |     32.36 ns |  1,338.5 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,552.0 ns |   1,058.6 ns |     58.03 ns |  2,585.0 ns |  0.60 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    728.3 ns |     532.0 ns |     29.16 ns |    712.0 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 54,085.5 ns | 385,755.9 ns | 21,144.58 ns | 42,033.5 ns | 12.69 |    4.30 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,261.7 ns |   1,427.3 ns |     78.23 ns |  4,238.0 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,800.0 ns |   2,778.6 ns |    152.31 ns |  3,827.0 ns |  0.89 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error         | StdDev     | Allocated |
|------------------------ |-------------:|--------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.977 μs |     0.5865 μs |  0.0321 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   567.554 μs | 1,246.6912 μs | 68.3354 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.271 μs |    12.7268 μs |  0.6976 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,727.908 μs | 1,420.6091 μs | 77.8684 μs |    1280 B |


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