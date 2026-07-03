---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 05:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,188.80 μs** |    **998.62 μs** |    **54.738 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,415.44 μs |  4,058.87 μs |   222.480 μs |  0.23 |    0.03 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,383.57 μs** |    **621.56 μs** |    **34.070 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,360.29 μs |    519.03 μs |    28.450 μs |  0.32 |    0.00 |  15.6250 |       - |  339.75 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,271.50 μs** |  **5,760.03 μs** |   **315.727 μs** |  **1.00** |    **0.06** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,368.22 μs |  2,671.92 μs |   146.457 μs |  0.22 |    0.02 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,178.08 μs** |  **5,787.65 μs** |   **317.241 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,747.34 μs |  7,775.03 μs |   426.175 μs |  0.51 |    0.03 |  15.6250 |       - |  369.62 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.67 μs** |     **22.74 μs** |     **1.246 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |    **42.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     77.48 μs |    278.93 μs |    15.289 μs |  0.54 |    0.09 |   0.4883 |       - |   14.31 KB |        0.34 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,436.87 μs** |    **212.02 μs** |    **11.621 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **418.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    721.72 μs |    489.65 μs |    26.839 μs |  0.50 |    0.02 |   3.9063 |       - |   136.5 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    318.29 μs |    280.64 μs |    15.383 μs |     ? |       ? |   6.8359 |  5.8594 |  198.13 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,288.40 μs** |  **1,261.26 μs** |    **69.134 μs** |  **1.00** |    **0.04** |  **70.3125** |       **-** | **1225.91 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,244.89 μs |  2,843.91 μs |   155.884 μs |  1.42 |    0.07 |  70.3125 | 62.5000 | 1954.79 KB |        1.59 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.06 μs** |     **40.21 μs** |     **2.204 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,115.51 μs |     20.50 μs |     1.124 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **6,219.24 μs** | **24,679.82 μs** | **1,352.784 μs** |  **1.03** |    **0.26** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,386.45 μs |    395.86 μs |    21.698 μs |  0.23 |    0.04 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,435.52 μs** |    **110.04 μs** |     **6.032 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,536.53 μs |  1,907.22 μs |   104.541 μs |  0.28 |    0.02 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,471.90 μs** |    **677.15 μs** |    **37.117 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,415.92 μs |  1,303.44 μs |    71.446 μs |  0.26 |    0.01 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.304 ms** |  **21.888 ms** | **1.1998 ms** | **3,170.647 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.380 ms |  58.407 ms | 3.2015 ms |    16.721 ms | 0.005 |  406.04 KB |        5.44 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.899 ms** |  **86.100 ms** | **4.7194 ms** | **3,166.186 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.972 ms |  43.021 ms | 2.3581 ms |    13.182 ms | 0.004 |  587.97 KB |        2.35 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.387 ms** |  **27.402 ms** | **1.5020 ms** | **3,167.096 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.029 ms | 130.898 ms | 7.1750 ms |    13.363 ms | 0.005 |  807.83 KB |        1.34 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.930 ms** |  **23.269 ms** | **1.2755 ms** | **3,165.241 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.048 ms |  46.617 ms | 2.5552 ms |    14.683 ms | 0.005 | 2574.14 KB |        1.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.515 ms** |  **31.040 ms** | **1.7014 ms** | **3,156.795 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.403 ms |  10.579 ms | 0.5799 ms |     6.323 ms | 0.002 |  181.98 KB |       75.63 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.255 ms** |  **21.778 ms** | **1.1937 ms** | **3,156.778 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.255 ms |  27.180 ms | 1.4899 ms |     5.425 ms | 0.002 |  193.98 KB |       46.58 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.071 ms** |  **27.852 ms** | **1.5267 ms** | **3,156.325 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.949 ms |  11.571 ms | 0.6342 ms |     6.709 ms | 0.002 |  182.23 KB |       75.73 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.440 ms** |  **15.432 ms** | **0.8459 ms** | **3,157.187 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.169 ms |   9.384 ms | 0.5143 ms |     5.890 ms | 0.002 |   183.7 KB |       43.95 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.296 μs |  3.695 μs | 0.2025 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.159 μs |  6.438 μs | 0.3529 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.155 μs | 11.923 μs | 0.6536 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.429 μs |  3.475 μs | 0.1905 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.010 μs |  3.324 μs | 0.1822 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 22.063 μs |  2.402 μs | 0.1317 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.675 μs | 20.718 μs | 1.1356 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.761 μs | 14.825 μs | 0.8126 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.352 μs | 12.275 μs | 0.6729 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,558.8 ns |  3,881.7 ns |   212.77 ns |  1,622.5 ns |  0.37 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,442.7 ns |  7,413.3 ns |   406.35 ns |  1,473.0 ns |  0.34 |    0.09 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  2,548.5 ns | 26,863.4 ns | 1,472.47 ns |  1,847.5 ns |  0.61 |    0.31 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,513.5 ns |  3,207.0 ns |   175.78 ns |  2,443.5 ns |  0.60 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    734.2 ns |    946.3 ns |    51.87 ns |    720.5 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 37,101.3 ns |  1,789.4 ns |    98.08 ns | 37,055.0 ns |  8.85 |    0.63 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,212.3 ns |  6,565.7 ns |   359.89 ns |  4,096.0 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,780.5 ns | 10,947.5 ns |   600.07 ns |  3,660.5 ns |  0.90 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.292 μs |  18.264 μs |  1.0011 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   527.408 μs | 379.155 μs | 20.7828 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.502 μs |   5.276 μs |  0.2892 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,589.564 μs | 397.929 μs | 21.8118 μs |    1280 B |


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