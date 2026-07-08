---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 09:33 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,263.85 μs** |  **1,085.60 μs** |    **59.505 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,749.84 μs |  5,354.30 μs |   293.487 μs |  0.28 |    0.04 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,501.06 μs** |    **797.88 μs** |    **43.734 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,387.98 μs |    668.57 μs |    36.647 μs |  0.32 |    0.00 |  15.6250 |       - |  340.01 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,131.64 μs** |    **815.17 μs** |    **44.682 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,823.57 μs |  2,866.29 μs |   157.111 μs |  0.30 |    0.02 |        - |       - |    36.6 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,136.11 μs** |  **2,421.44 μs** |   **132.727 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,422.07 μs |  5,832.77 μs |   319.714 μs |  0.57 |    0.02 |  15.6250 |       - |  359.44 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.85 μs** |     **41.58 μs** |     **2.279 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     86.33 μs |     95.19 μs |     5.217 μs |  0.63 |    0.03 |        - |       - |    5.63 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,358.36 μs** |    **231.46 μs** |    **12.687 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    876.33 μs |  1,879.14 μs |   103.002 μs |  0.65 |    0.07 |        - |       - |   88.33 KB |        0.26 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **641.96 μs** |  **8,018.24 μs** |   **439.507 μs** |  **1.47** |    **1.41** |   **7.3242** |       **-** |  **122.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    376.54 μs |  1,143.76 μs |    62.693 μs |  0.86 |    0.58 |        - |       - |   77.13 KB |        0.63 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,870.07 μs** | **25,213.69 μs** | **1,382.048 μs** |  **1.01** |    **0.16** |  **74.2188** |       **-** | **1226.89 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,107.22 μs |  5,981.57 μs |   327.870 μs |  0.29 |    0.04 |        - |       - |  958.52 KB |        0.78 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,466.37 μs** |    **501.00 μs** |    **27.461 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,371.14 μs |    437.07 μs |    23.957 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,441.08 μs** |    **152.09 μs** |     **8.337 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,339.84 μs |     62.86 μs |     3.445 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **6,328.16 μs** | **28,276.42 μs** | **1,549.926 μs** |  **1.04** |    **0.29** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,301.15 μs |    292.66 μs |    16.042 μs |  0.21 |    0.04 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,433.54 μs** |     **56.55 μs** |     **3.100 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,187.71 μs |    180.10 μs |     9.872 μs |  0.22 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,426.11 μs** | **34,974.73 μs** | **1,917.083 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,485.56 μs | 25,114.97 μs | 1,376.636 μs | 0.006 |    0.00 |  617920 B |        8.09 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166,314.65 μs** |  **8,520.06 μs** |   **467.013 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,664.04 μs | 25,960.05 μs | 1,422.958 μs | 0.005 |    0.00 |  818576 B |        3.19 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,447.59 μs** | **13,049.25 μs** |   **715.273 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20,168.34 μs | 49,249.14 μs | 2,699.511 μs | 0.006 |    0.00 | 1035664 B |        1.68 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167,005.08 μs** | **11,927.82 μs** |   **653.804 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,679.14 μs | 57,402.99 μs | 3,146.452 μs | 0.006 |    0.00 | 2917400 B |        1.20 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **34.01 μs** |    **139.45 μs** |     **7.644 μs** |  **1.03** |    **0.27** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        28.49 μs |     82.89 μs |     4.544 μs |  0.86 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **37.08 μs** |     **28.49 μs** |     **1.562 μs** |  **1.00** |    **0.05** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        29.48 μs |     61.55 μs |     3.374 μs |  0.80 |    0.08 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **42.74 μs** |     **92.68 μs** |     **5.080 μs** |  **1.01** |    **0.15** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        27.99 μs |    100.50 μs |     5.509 μs |  0.66 |    0.13 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **40.10 μs** |    **123.22 μs** |     **6.754 μs** |  **1.02** |    **0.22** |    **2704 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        29.80 μs |     53.17 μs |     2.914 μs |  0.76 |    0.14 |    2440 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.577 μs |   4.1800 μs | 0.2291 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.331 μs |   7.5342 μs | 0.4130 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.916 μs |   3.8624 μs | 0.2117 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.519 μs |   0.2107 μs | 0.0115 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.773 μs |  31.2677 μs | 1.7139 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.737 μs |   5.9680 μs | 0.3271 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.925 μs |   7.5574 μs | 0.4142 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.584 μs | 161.2077 μs | 8.8363 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.418 μs |  68.8667 μs | 3.7748 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.496 μs |   2.4107 μs | 0.1321 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.571 μs |  19.1671 μs | 1.0506 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.291 μs |  35.3804 μs | 1.9393 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.787 μs |   2.7382 μs | 0.1501 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.534 μs |  14.6150 μs | 0.8011 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,284.2 ns |  2,281.1 ns | 125.03 ns |  0.28 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,327.2 ns |  3,229.2 ns | 177.00 ns |  0.29 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,319.7 ns |  1,519.1 ns |  83.27 ns |  0.29 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,849.3 ns |  9,018.6 ns | 494.34 ns |  0.62 |    0.13 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    842.3 ns |  2,877.1 ns | 157.70 ns |  0.18 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,774.3 ns | 10,520.3 ns | 576.65 ns |  8.71 |    1.30 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,661.7 ns | 15,236.3 ns | 835.15 ns |  1.02 |    0.22 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,692.2 ns |  4,668.4 ns | 255.89 ns |  1.03 |    0.16 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.063 μs |   4.091 μs |  0.2243 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 517.677 μs | 398.139 μs | 21.8234 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.897 μs |   3.875 μs |  0.2124 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 240.924 μs | 440.200 μs | 24.1289 μs |      80 B |


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