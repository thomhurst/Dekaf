---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 11:04 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,085.38 μs** |    **813.723 μs** |    **44.603 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,598.70 μs |  2,957.109 μs |   162.089 μs |  0.26 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,301.87 μs** |  **1,232.436 μs** |    **67.554 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,404.39 μs |  1,642.376 μs |    90.024 μs |  0.33 |    0.01 |  15.6250 |       - |  339.75 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,475.98 μs** |    **403.858 μs** |    **22.137 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,733.22 μs |  1,334.318 μs |    73.138 μs |  0.27 |    0.01 |        - |       - |   36.61 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,652.85 μs** |  **5,269.324 μs** |   **288.829 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,763.39 μs | 11,548.294 μs |   633.001 μs |  0.53 |    0.04 |  15.6250 |       - |  358.89 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.91 μs** |     **57.737 μs** |     **3.165 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     79.15 μs |     76.153 μs |     4.174 μs |  0.62 |    0.03 |        - |       - |   12.84 KB |        0.38 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,285.02 μs** |  **2,472.270 μs** |   **135.513 μs** |  **1.01** |    **0.13** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    913.38 μs |  1,246.684 μs |    68.335 μs |  0.72 |    0.08 |        - |       - |   41.85 KB |        0.12 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,045.91 μs** |     **91.907 μs** |     **5.038 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    256.47 μs |    355.596 μs |    19.491 μs |  0.25 |    0.02 |        - |       - |   94.07 KB |        0.77 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,749.40 μs** | **22,792.599 μs** | **1,249.339 μs** |  **1.01** |    **0.17** |  **74.2188** |       **-** | **1230.35 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,200.88 μs |  7,947.788 μs |   435.645 μs |  0.33 |    0.06 |        - |       - |  968.56 KB |        0.79 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,403.01 μs** |    **153.287 μs** |     **8.402 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.92 μs |     21.423 μs |     1.174 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,407.78 μs** |     **51.934 μs** |     **2.847 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,112.47 μs |     39.104 μs |     2.143 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.35 μs** |     **56.990 μs** |     **3.124 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,111.34 μs |      9.985 μs |     0.547 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,400.33 μs** |     **76.769 μs** |     **4.208 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,143.48 μs |    142.789 μs |     7.827 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,600.51 μs** | **26,241.84 μs** | **1,438.404 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,767.03 μs | 32,238.69 μs | 1,767.111 μs | 0.005 |    0.00 |  618744 B |        8.10 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,246.56 μs** |  **5,097.72 μs** |   **279.423 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,745.24 μs | 39,942.54 μs | 2,189.386 μs | 0.005 |    0.00 |  806560 B |        3.15 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,789.73 μs** |  **9,532.47 μs** |   **522.507 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15,268.15 μs | 33,274.97 μs | 1,823.913 μs | 0.005 |    0.00 | 1030136 B |        1.67 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,141.02 μs** | **22,983.98 μs** | **1,259.830 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,671.51 μs | 47,796.10 μs | 2,619.866 μs | 0.006 |    0.00 | 2843664 B |        1.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **34.91 μs** |     **73.97 μs** |     **4.054 μs** |  **1.01** |    **0.15** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        25.62 μs |     84.60 μs |     4.637 μs |  0.74 |    0.14 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **31.38 μs** |     **67.86 μs** |     **3.720 μs** |  **1.01** |    **0.15** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        26.91 μs |     45.55 μs |     2.497 μs |  0.87 |    0.12 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **40.73 μs** |    **211.16 μs** |    **11.575 μs** |  **1.05** |    **0.34** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        26.60 μs |    115.17 μs |     6.313 μs |  0.68 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **33.71 μs** |     **93.88 μs** |     **5.146 μs** |  **1.02** |    **0.19** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        27.28 μs |     71.82 μs |     3.937 μs |  0.82 |    0.15 |    2440 B |        0.91 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.312 μs |  0.4676 μs | 0.0256 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.111 μs |  2.9463 μs | 0.1615 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.057 μs |  4.3442 μs | 0.2381 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.555 μs |  2.2119 μs | 0.1212 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.802 μs | 42.5222 μs | 2.3308 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.081 μs |  5.9403 μs | 0.3256 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.041 μs |  3.9753 μs | 0.2179 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.380 μs |  3.6520 μs | 0.2002 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.848 μs |  1.2814 μs | 0.0702 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.436 μs |  2.3278 μs | 0.1276 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 33.465 μs | 15.2874 μs | 0.8380 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.845 μs | 21.4575 μs | 1.1762 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.548 μs |  2.8322 μs | 0.1552 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.790 μs |  2.4544 μs | 0.1345 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,345.3 ns |  1,099.7 ns |    60.28 ns |  1,352.0 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,378.7 ns |    822.7 ns |    45.09 ns |  1,382.0 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,458.7 ns |  1,695.1 ns |    92.92 ns |  1,432.0 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,609.7 ns | 32,710.3 ns | 1,792.96 ns |  2,575.0 ns |  0.86 |    0.37 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    751.7 ns |    489.6 ns |    26.84 ns |    742.0 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,903.3 ns |  5,026.1 ns |   275.50 ns | 39,843.0 ns |  9.47 |    0.35 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,219.5 ns |  3,326.0 ns |   182.31 ns |  4,162.5 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,020.0 ns |  2,803.1 ns |   153.65 ns |  4,077.0 ns |  0.95 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.551 μs |  10.499 μs |  0.5755 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 509.925 μs | 350.851 μs | 19.2313 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.539 μs |   3.777 μs |  0.2071 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 228.466 μs |  30.151 μs |  1.6527 μs |      80 B |


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