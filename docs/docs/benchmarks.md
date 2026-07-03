---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 12:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,078.27 μs** |    **586.36 μs** |  **32.141 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,363.98 μs |  2,091.48 μs | 114.641 μs |  0.22 |    0.02 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,206.76 μs** |    **500.61 μs** |  **27.440 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,254.54 μs |    338.51 μs |  18.555 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.85 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **7,026.95 μs** | **11,335.12 μs** | **621.316 μs** |  **1.00** |    **0.11** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,246.70 μs |  3,511.09 μs | 192.455 μs |  0.18 |    0.03 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,492.42 μs** |  **5,458.10 μs** | **299.177 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,868.97 μs |    550.80 μs |  30.191 μs |  0.51 |    0.01 |  15.6250 |       - |  369.48 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.83 μs** |    **207.21 μs** |  **11.358 μs** |  **1.00** |    **0.10** |   **2.1973** |       **-** |   **37.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.73 μs |     63.55 μs |   3.483 μs |  0.44 |    0.04 |   0.4883 |  0.2441 |   14.12 KB |        0.38 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,283.63 μs** |  **1,426.99 μs** |  **78.218 μs** |  **1.00** |    **0.08** |  **25.3906** |       **-** |  **428.93 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    629.47 μs |    996.32 μs |  54.612 μs |  0.49 |    0.05 |   3.9063 |  1.9531 |  129.29 KB |        0.30 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    333.97 μs |    247.44 μs |  13.563 μs |     ? |       ? |   5.8594 |  4.8828 |  166.35 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,240.01 μs |    647.65 μs |  35.500 μs |     ? |       ? |  62.5000 | 54.6875 | 1917.74 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,411.02 μs** |     **92.62 μs** |   **5.077 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,358.45 μs |    610.41 μs |  33.459 μs |  0.25 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.70 μs** |     **59.02 μs** |   **3.235 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,373.46 μs |     64.31 μs |   3.525 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,407.35 μs** |     **30.48 μs** |   **1.671 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,363.17 μs |    171.51 μs |   9.401 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.86 μs** |     **90.68 μs** |   **4.970 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,107.30 μs |     91.79 μs |   5.032 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.280 ms** | **27.002 ms** | **1.4801 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.256 ms | 68.817 ms | 3.7721 ms | 0.005 |  407.11 KB |        5.46 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.017 ms** | **10.778 ms** | **0.5908 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.001 ms | 28.588 ms | 1.5670 ms | 0.004 |  589.82 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.882 ms** | **43.472 ms** | **2.3829 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.770 ms | 49.108 ms | 2.6918 ms | 0.005 |  812.99 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.261 ms** |  **7.447 ms** | **0.4082 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.794 ms | 11.678 ms | 0.6401 ms | 0.005 | 2573.27 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.163 ms** |  **3.728 ms** | **0.2043 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.125 ms |  8.939 ms | 0.4900 ms | 0.002 |  182.59 KB |       75.88 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.700 ms** | **13.446 ms** | **0.7370 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.234 ms | 14.984 ms | 0.8213 ms | 0.002 |  185.46 KB |       44.54 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.713 ms** | **21.790 ms** | **1.1944 ms** | **1.000** |   **26.45 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.157 ms |  4.396 ms | 0.2410 ms | 0.002 |  186.05 KB |        7.03 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.975 ms** |  **7.826 ms** | **0.4290 ms** | **1.000** |   **28.23 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.291 ms | 14.760 ms | 0.8090 ms | 0.002 |   183.8 KB |        6.51 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 27.311 μs | 358.060 μs | 19.6265 μs | 16.000 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.458 μs |   4.108 μs |  0.2252 μs |  9.448 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.020 μs |   4.904 μs |  0.2688 μs |  9.870 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 28.130 μs |  18.173 μs |  0.9961 μs | 27.642 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 16.569 μs |  45.379 μs |  2.4874 μs | 15.194 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.441 μs |   2.900 μs |  0.1589 μs | 19.502 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.890 μs |   9.220 μs |  0.5054 μs | 17.692 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.426 μs |   2.646 μs |  0.1450 μs |  4.429 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.716 μs |   7.002 μs |  0.3838 μs | 10.770 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,710.3 ns |  1,891.6 ns | 103.68 ns |  0.43 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,306.3 ns |    690.7 ns |  37.86 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,373.0 ns |    632.0 ns |  34.64 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,477.8 ns |    105.3 ns |   5.77 ns |  0.62 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    874.5 ns |  4,377.6 ns | 239.95 ns |  0.22 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,522.3 ns | 13,841.4 ns | 758.69 ns |  9.70 |    0.28 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,973.7 ns |  1,916.3 ns | 105.04 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,878.5 ns |  3,394.9 ns | 186.08 ns |  0.98 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.20 μs |   4.734 μs |  0.260 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   553.99 μs | 210.559 μs | 11.541 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.13 μs |   7.135 μs |  0.391 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,649.04 μs | 298.141 μs | 16.342 μs |    1280 B |


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