---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 06:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,177.34 μs** |   **817.74 μs** |  **44.823 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,333.34 μs |   139.43 μs |   7.643 μs |  0.22 |    0.00 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,153.32 μs** | **1,649.76 μs** |  **90.429 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,306.21 μs |   686.35 μs |  37.621 μs |  0.32 |    0.01 |  15.6250 |       - |  339.86 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,684.92 μs** |   **441.04 μs** |  **24.175 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,175.30 μs |   996.71 μs |  54.633 μs |  0.18 |    0.01 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,686.50 μs** |   **314.49 μs** |  **17.238 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,927.99 μs | 2,823.53 μs | 154.767 μs |  0.51 |    0.01 |  15.6250 |       - |  369.58 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.42 μs** |   **158.45 μs** |   **8.685 μs** |  **1.00** |    **0.08** |   **2.4414** |       **-** |   **41.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.31 μs |   131.55 μs |   7.211 μs |  0.48 |    0.05 |   0.4883 |  0.2441 |   14.03 KB |        0.34 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,271.17 μs** |   **945.94 μs** |  **51.850 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **423.32 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    744.56 μs |   493.54 μs |  27.053 μs |  0.59 |    0.03 |   3.9063 |       - |   89.59 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    336.04 μs |   295.37 μs |  16.190 μs |     ? |       ? |   6.8359 |  5.8594 |  194.09 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,133.89 μs | 2,350.05 μs | 128.814 μs |     ? |       ? |  62.5000 | 54.6875 | 1819.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,403.74 μs** |    **39.15 μs** |   **2.146 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,128.22 μs |   112.13 μs |   6.146 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,403.21 μs** |    **90.76 μs** |   **4.975 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,269.33 μs |   396.02 μs |  21.707 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,436.80 μs** |   **839.13 μs** |  **45.996 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,287.31 μs |   493.26 μs |  27.037 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,451.36 μs** | **1,231.12 μs** |  **67.482 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,337.99 μs |   757.99 μs |  41.548 μs |  0.25 |    0.01 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.484 ms** | **28.410 ms** | **1.5573 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.357 ms | 64.673 ms | 3.5450 ms | 0.006 |  416.99 KB |        5.59 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.968 ms** | **37.111 ms** | **2.0342 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.782 ms | 12.067 ms | 0.6615 ms | 0.004 |  590.52 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.163 ms** |  **8.120 ms** | **0.4451 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.354 ms | 45.054 ms | 2.4696 ms | 0.005 |  809.85 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.988 ms** |  **8.783 ms** | **0.4814 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.935 ms | 26.213 ms | 1.4368 ms | 0.006 | 2584.07 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.750 ms** | **22.580 ms** | **1.2377 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.244 ms |  2.750 ms | 0.1507 ms | 0.002 |  189.19 KB |       78.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.789 ms** | **16.131 ms** | **0.8842 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.398 ms | 12.228 ms | 0.6703 ms | 0.002 |  184.34 KB |       44.27 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.324 ms** | **69.290 ms** | **3.7980 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.534 ms | 10.776 ms | 0.5906 ms | 0.002 |   181.5 KB |       75.43 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.909 ms** | **19.810 ms** | **1.0858 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     8.549 ms | 44.592 ms | 2.4443 ms | 0.003 |  192.98 KB |       46.17 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.634 μs |   5.1308 μs |  0.2812 μs | 14.648 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.670 μs |   7.2536 μs |  0.3976 μs |  9.584 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.137 μs |   4.3903 μs |  0.2406 μs | 10.014 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.945 μs |   3.7029 μs |  0.2030 μs | 26.882 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.933 μs |   1.3448 μs |  0.0737 μs |  8.906 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.216 μs |   0.8360 μs |  0.0458 μs | 19.206 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 26.050 μs | 254.5382 μs | 13.9521 μs | 18.495 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.412 μs |   1.5552 μs |  0.0852 μs |  4.378 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.020 μs |  17.8241 μs |  0.9770 μs | 10.905 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,555.7 ns |   1,304.2 ns |     71.49 ns |  0.38 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,278.0 ns |     364.9 ns |     20.00 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.2 ns |   2,084.9 ns |    114.28 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,635.3 ns |   2,582.0 ns |    141.53 ns |  0.64 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    818.7 ns |   2,483.6 ns |    136.14 ns |  0.20 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,655.0 ns | 215,175.0 ns | 11,794.47 ns | 11.35 |    2.50 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,111.3 ns |   2,229.4 ns |    122.20 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,716.3 ns |   4,700.7 ns |    257.66 ns |  0.90 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.128 μs |     5.500 μs |   0.3015 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   828.923 μs | 3,070.544 μs | 168.3069 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.064 μs |    21.593 μs |   1.1836 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,725.898 μs |   266.506 μs |  14.6081 μs |    1280 B |


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