---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 08:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,190.18 μs** |    **207.47 μs** |    **11.372 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,332.99 μs |    834.46 μs |    45.740 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,350.70 μs** |    **353.47 μs** |    **19.375 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,347.07 μs |    223.85 μs |    12.270 μs |  0.32 |    0.00 |  15.6250 |       - |  339.78 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,131.68 μs** |    **460.11 μs** |    **25.220 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,254.84 μs |  1,640.17 μs |    89.903 μs |  0.20 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **28,362.52 μs** | **90,480.53 μs** | **4,959.543 μs** |  **1.02** |    **0.21** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,270.43 μs | 19,295.07 μs | 1,057.628 μs |  0.26 |    0.05 |  15.6250 |       - |   361.9 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.58 μs** |     **99.18 μs** |     **5.436 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **41.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.66 μs |     46.09 μs |     2.526 μs |  0.45 |    0.02 |        - |       - |    7.51 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,431.10 μs** |    **281.11 μs** |    **15.409 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **421.44 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    668.39 μs |  1,126.34 μs |    61.739 μs |  0.47 |    0.04 |        - |       - |    67.8 KB |        0.16 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    212.84 μs |    304.00 μs |    16.663 μs |     ? |       ? |        - |       - |   95.03 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,922.96 μs |  4,737.32 μs |   259.668 μs |     ? |       ? |   7.8125 |       - | 1053.72 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,425.81 μs** |    **144.26 μs** |     **7.908 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,116.12 μs |     40.98 μs |     2.246 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,426.69 μs** |    **145.73 μs** |     **7.988 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,219.14 μs |  1,182.65 μs |    64.825 μs |  0.22 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,439.89 μs** |    **332.63 μs** |    **18.233 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,395.35 μs |    152.28 μs |     8.347 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,430.21 μs** |    **115.80 μs** |     **6.348 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,385.90 μs |    435.67 μs |    23.880 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.984 ms** |  **37.313 ms** | **2.0452 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.209 ms |  43.051 ms | 2.3598 ms | 0.005 |  592.92 KB |        7.95 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.142 ms** |  **11.961 ms** | **0.6556 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.816 ms |  37.109 ms | 2.0341 ms | 0.005 |  777.34 KB |        3.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.229 ms** |  **15.925 ms** | **0.8729 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21.259 ms | 149.245 ms | 8.1806 ms | 0.007 | 1012.84 KB |        1.68 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.409 ms** |  **17.445 ms** | **0.9562 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.806 ms |  29.007 ms | 1.5900 ms | 0.005 | 2761.72 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.544 ms** |  **25.391 ms** | **1.3918 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.286 ms |   4.700 ms | 0.2576 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.699 ms** |  **47.306 ms** | **2.5930 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.293 ms |  10.916 ms | 0.5983 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.854 ms** |   **8.884 ms** | **0.4870 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.156 ms |  30.722 ms | 1.6840 ms | 0.002 |  204.02 KB |       84.79 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.126 ms** |  **29.198 ms** | **1.6004 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.125 ms |  11.649 ms | 0.6385 ms | 0.002 |  188.34 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.847 μs |   2.0560 μs |  0.1127 μs | 14.787 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 19.076 μs | 309.9343 μs | 16.9885 μs |  9.287 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.267 μs |   3.4993 μs |  0.1918 μs | 10.323 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.824 μs |   1.3693 μs |  0.0751 μs | 26.781 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.930 μs |   0.7595 μs |  0.0416 μs |  8.917 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.255 μs |   1.4249 μs |  0.0781 μs | 19.295 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.767 μs |  10.2016 μs |  0.5592 μs | 17.583 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 30.249 μs | 309.0283 μs | 16.9389 μs | 20.484 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.615 μs |   2.1612 μs |  0.1185 μs |  4.678 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.536 μs |   4.9168 μs |  0.2695 μs | 10.649 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,248.7 ns | 1,114.7 ns |  61.10 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,728.7 ns | 2,337.1 ns | 128.10 ns |  0.40 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,556.2 ns | 1,659.1 ns |  90.94 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,732.0 ns | 1,978.2 ns | 108.43 ns |  0.63 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    802.7 ns | 1,916.3 ns | 105.04 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,219.0 ns | 6,086.6 ns | 333.63 ns |  9.27 |    0.26 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,340.7 ns | 2,456.5 ns | 134.65 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,198.3 ns | 5,744.9 ns | 314.90 ns |  0.97 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.77 μs |   7.058 μs |  0.387 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   504.66 μs |  43.761 μs |  2.399 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.12 μs |   2.874 μs |  0.158 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,678.96 μs | 529.666 μs | 29.033 μs |    1280 B |


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