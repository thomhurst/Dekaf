---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 23:02 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,387.84 μs** |  **2,106.57 μs** |   **115.468 μs** |  **1.00** |    **0.02** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,370.54 μs |  1,109.34 μs |    60.807 μs |  0.21 |    0.01 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,635.65 μs** |    **533.48 μs** |    **29.242 μs** |  **1.00** |    **0.00** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,321.15 μs |     92.69 μs |     5.081 μs |  0.30 |    0.00 |       - |  339.63 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,778.26 μs** |    **809.73 μs** |    **44.384 μs** |  **1.00** |    **0.01** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,182.38 μs |    922.97 μs |    50.591 μs |  0.17 |    0.01 |       - |   36.32 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,454.54 μs** |    **640.36 μs** |    **35.100 μs** |  **1.00** |    **0.01** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,525.37 μs | 19,960.22 μs | 1,094.087 μs |  0.65 |    0.11 |       - |   361.5 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **149.79 μs** |     **19.74 μs** |     **1.082 μs** |  **1.00** |    **0.01** |  **0.4883** |   **42.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    58.37 μs |     22.68 μs |     1.243 μs |  0.39 |    0.01 |       - |   11.25 KB |        0.27 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,797.29 μs** | **12,017.06 μs** |   **658.696 μs** |  **1.09** |    **0.48** |  **3.9063** |  **453.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   580.91 μs |  1,783.37 μs |    97.753 μs |  0.35 |    0.12 |       - |    49.8 KB |        0.11 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   175.16 μs |    473.77 μs |    25.969 μs |     ? |       ? |       - |   60.79 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,529.57 μs |    894.96 μs |    49.056 μs |     ? |       ? |       - | 1321.97 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **6,880.40 μs** | **36,131.07 μs** | **1,980.466 μs** |  **1.05** |    **0.35** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,121.98 μs |    105.52 μs |     5.784 μs |  0.17 |    0.04 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,596.66 μs** |  **3,435.74 μs** |   **188.324 μs** |  **1.00** |    **0.04** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,538.99 μs |  4,388.94 μs |   240.573 μs |  0.28 |    0.04 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,421.28 μs** |    **754.34 μs** |    **41.348 μs** |  **1.00** |    **0.01** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,377.41 μs |  1,061.05 μs |    58.160 μs |  0.25 |    0.01 |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,500.83 μs** |    **201.16 μs** |    **11.026 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,117.06 μs |     55.74 μs |     3.056 μs |  0.20 |    0.00 |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,173.881 ms** |  **13.888 ms** | **0.7613 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.154 ms |  17.565 ms | 0.9628 ms | 0.005 |  602.66 KB |        8.08 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.331 ms** |   **8.888 ms** | **0.4872 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.431 ms |  22.505 ms | 1.2336 ms | 0.006 |  790.41 KB |        3.16 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.529 ms** |   **9.492 ms** | **0.5203 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    24.131 ms | 118.599 ms | 6.5008 ms | 0.008 | 1000.24 KB |        1.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,169.733 ms** |  **14.862 ms** | **0.8147 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    20.203 ms |  45.952 ms | 2.5188 ms | 0.006 | 2763.69 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,158.064 ms** |  **11.552 ms** | **0.6332 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.136 ms |   5.753 ms | 0.3154 ms | 0.002 |  193.62 KB |       80.46 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.200 ms** |  **24.919 ms** | **1.3659 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.342 ms |  21.067 ms | 1.1547 ms | 0.002 |  191.22 KB |       45.92 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.346 ms** |  **28.004 ms** | **1.5350 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.520 ms |  14.034 ms | 0.7692 ms | 0.002 |  258.25 KB |      107.32 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.593 ms** |  **38.454 ms** | **2.1078 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.794 ms |  14.281 ms | 0.7828 ms | 0.002 |  261.64 KB |       62.60 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.149 μs |  48.3842 μs |  2.6521 μs | 25.634 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.905 μs |   1.7432 μs |  0.0956 μs |  9.909 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.242 μs |   4.8257 μs |  0.2645 μs | 10.190 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 31.414 μs |  33.7053 μs |  1.8475 μs | 31.625 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.994 μs |   0.7698 μs |  0.0422 μs |  9.008 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.324 μs |   1.9556 μs |  0.1072 μs | 20.267 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.223 μs |   3.5276 μs |  0.1934 μs | 18.160 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.264 μs |  10.1536 μs |  0.5566 μs | 20.053 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.489 μs |   2.2119 μs |  0.1212 μs |  4.469 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 19.788 μs | 255.1394 μs | 13.9850 μs | 12.694 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,249.3 ns |   752.3 ns |  41.24 ns |  0.26 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,278.3 ns | 2,111.9 ns | 115.76 ns |  0.26 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,604.5 ns | 6,604.3 ns | 362.00 ns |  0.33 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,427.7 ns | 1,063.8 ns |  58.31 ns |  0.50 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    749.5 ns |   286.7 ns |  15.72 ns |  0.15 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,031.8 ns | 7,633.9 ns | 418.44 ns |  8.23 |    0.27 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,866.0 ns | 3,234.7 ns | 177.30 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,007.5 ns | 5,147.2 ns | 282.13 ns |  0.82 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    15.98 μs |  21.775 μs |  1.194 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   545.76 μs | 518.214 μs | 28.405 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.04 μs |   3.281 μs |  0.180 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,685.44 μs | 754.191 μs | 41.340 μs |    1280 B |


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