---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 05:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,498.55 μs** |    **188.44 μs** |    **10.329 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,771.41 μs |  4,159.38 μs |   227.990 μs |  0.27 |    0.03 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,628.61 μs** |  **1,073.90 μs** |    **58.864 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,414.18 μs |    261.62 μs |    14.340 μs |  0.32 |    0.00 |  15.6250 |       - |  339.58 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,663.29 μs** |  **9,699.28 μs** |   **531.650 μs** |  **1.00** |    **0.10** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,870.72 μs |  3,380.53 μs |   185.298 μs |  0.28 |    0.03 |        - |       - |    36.4 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,818.47 μs** |  **5,080.97 μs** |   **278.505 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,709.42 μs |  3,754.54 μs |   205.799 μs |  0.52 |    0.02 |  15.6250 |       - |  360.93 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.37 μs** |     **47.42 μs** |     **2.599 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     79.56 μs |    342.84 μs |    18.792 μs |  0.56 |    0.12 |        - |       - |     7.7 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,384.79 μs** |     **79.03 μs** |     **4.332 μs** |  **1.00** |    **0.00** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    831.78 μs |  1,888.69 μs |   103.526 μs |  0.60 |    0.06 |        - |       - |   96.13 KB |        0.29 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,111.43 μs** |    **241.21 μs** |    **13.221 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    227.98 μs |    395.56 μs |    21.682 μs |  0.21 |    0.02 |        - |       - |   94.17 KB |        0.77 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,111.48 μs** | **25,658.72 μs** | **1,406.441 μs** |  **1.01** |    **0.18** |  **74.2188** |       **-** | **1226.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,152.42 μs |  3,628.77 μs |   198.905 μs |  0.32 |    0.04 |        - |       - |  837.61 KB |        0.68 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.16 μs** |     **55.82 μs** |     **3.060 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.80 μs |     20.35 μs |     1.115 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,428.89 μs** |     **56.88 μs** |     **3.118 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.52 μs |     94.24 μs |     5.166 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,423.82 μs** |     **34.33 μs** |     **1.882 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.70 μs |     38.96 μs |     2.135 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.74 μs** |    **363.71 μs** |    **19.936 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,131.03 μs |    423.78 μs |    23.229 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.819 ms** | **20.377 ms** | **1.1169 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.532 ms | 26.582 ms | 1.4570 ms | 0.005 |  600.45 KB |        8.05 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.806 ms** |  **2.949 ms** | **0.1617 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.785 ms | 19.014 ms | 1.0422 ms | 0.004 |  786.66 KB |        3.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.404 ms** |  **5.209 ms** | **0.2855 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.506 ms | 65.569 ms | 3.5941 ms | 0.005 | 1004.57 KB |        1.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.877 ms** |  **5.506 ms** | **0.3018 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.999 ms | 22.610 ms | 1.2394 ms | 0.005 | 2811.77 KB |        1.19 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.407 ms** | **33.882 ms** | **1.8572 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.826 ms | 19.785 ms | 1.0845 ms | 0.002 |  189.72 KB |       78.84 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.694 ms** |  **8.306 ms** | **0.4553 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.690 ms | 40.298 ms | 2.2089 ms | 0.002 |   196.2 KB |       47.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.791 ms** | **28.618 ms** | **1.5687 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.843 ms | 10.586 ms | 0.5803 ms | 0.002 |  185.51 KB |       77.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.478 ms** | **11.639 ms** | **0.6380 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.271 ms | 27.616 ms | 1.5137 ms | 0.002 |   228.5 KB |       54.67 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.634 μs |  0.6654 μs | 0.0365 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.668 μs |  5.9385 μs | 0.3255 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.418 μs |  5.0929 μs | 0.2792 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.866 μs |  6.7600 μs | 0.3705 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.895 μs |  1.5587 μs | 0.0854 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.247 μs |  4.2968 μs | 0.2355 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.471 μs | 54.7852 μs | 3.0030 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.422 μs | 61.9841 μs | 3.3976 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.784 μs |  8.3196 μs | 0.4560 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.780 μs |  9.6922 μs | 0.5313 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,405.5 ns |   3,153.9 ns |   172.88 ns |  0.34 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,462.3 ns |   6,340.9 ns |   347.56 ns |  0.36 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.0 ns |   3,020.8 ns |   165.58 ns |  0.34 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,466.3 ns |   2,689.0 ns |   147.39 ns |  0.60 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    718.7 ns |     526.7 ns |    28.87 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,022.0 ns | 116,194.7 ns | 6,369.03 ns | 10.72 |    1.46 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,115.3 ns |   4,688.1 ns |   256.97 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,801.3 ns |   8,207.7 ns |   449.89 ns |  0.93 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Median       | Allocated |
|------------------------ |-------------:|-----------:|----------:|-------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    18.137 μs | 181.715 μs |  9.960 μs |    12.639 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   519.583 μs | 132.225 μs |  7.248 μs |   523.586 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.983 μs |  19.085 μs |  1.046 μs |     8.482 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,567.410 μs | 229.655 μs | 12.588 μs | 1,567.965 μs |    1280 B |


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