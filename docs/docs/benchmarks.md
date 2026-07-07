---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-07 02:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,410.23 μs** |  **1,126.89 μs** |    **61.768 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,850.44 μs |  5,388.29 μs |   295.350 μs |  0.29 |    0.04 |       - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,434.34 μs** |  **1,231.13 μs** |    **67.482 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,421.21 μs |    367.12 μs |    20.123 μs |  0.33 |    0.00 |  7.8125 |       - |  339.72 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,788.31 μs** |    **188.47 μs** |    **10.331 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,708.76 μs |  2,023.51 μs |   110.915 μs |  0.25 |    0.01 |       - |       - |   36.62 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,495.34 μs** |  **1,435.96 μs** |    **78.710 μs** |  **1.00** |    **0.01** | **78.1250** | **46.8750** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,356.54 μs |  5,594.84 μs |   306.672 μs |  0.67 |    0.03 |       - |       - |  357.03 KB |        0.18 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **121.54 μs** |     **55.12 μs** |     **3.021 μs** |  **1.00** |    **0.03** |  **1.3428** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    70.40 μs |    125.15 μs |     6.860 μs |  0.58 |    0.05 |       - |       - |     8.3 KB |        0.25 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,233.88 μs** |    **769.67 μs** |    **42.188 μs** |  **1.00** |    **0.04** | **13.6719** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   785.99 μs |  1,002.56 μs |    54.954 μs |  0.64 |    0.04 |       - |       - |   41.29 KB |        0.12 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **804.67 μs** |     **69.44 μs** |     **3.806 μs** |  **1.00** |    **0.01** |  **4.8828** |       **-** |  **122.12 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   214.81 μs |    120.98 μs |     6.631 μs |  0.27 |    0.01 |       - |       - |  101.19 KB |        0.83 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **8,048.80 μs** |    **807.09 μs** |    **44.239 μs** |  **1.00** |    **0.01** | **48.8281** |       **-** | **1221.75 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 3,788.50 μs | 22,756.98 μs | 1,247.387 μs |  0.47 |    0.13 |       - |       - |  719.89 KB |        0.59 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,578.42 μs** |    **370.91 μs** |    **20.331 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,111.96 μs |     46.82 μs |     2.566 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,517.48 μs** |    **419.09 μs** |    **22.972 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,127.70 μs |    380.93 μs |    20.880 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,536.42 μs** |    **299.84 μs** |    **16.435 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,116.17 μs |     36.58 μs |     2.005 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,503.68 μs** |    **348.80 μs** |    **19.119 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,132.62 μs |    529.27 μs |    29.011 μs |  0.21 |    0.00 |       - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171,372.76 μs** | **34,258.09 μs** | **1,877.802 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,693.64 μs | 28,062.47 μs | 1,538.199 μs | 0.005 |    0.00 |  616400 B |        8.07 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166,503.59 μs** | **14,276.10 μs** |   **782.521 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,997.72 μs | 16,058.43 μs |   880.217 μs | 0.005 |    0.00 |  806336 B |        3.14 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,594.79 μs** |  **1,404.27 μs** |    **76.973 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,278.77 μs | 50,909.05 μs | 2,790.497 μs | 0.006 |    0.00 | 1125352 B |        1.83 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,367.50 μs** | **39,632.29 μs** | **2,172.380 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,252.65 μs | 29,115.60 μs | 1,595.924 μs | 0.005 |    0.00 | 2915032 B |        1.20 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **39.51 μs** |     **96.12 μs** |     **5.269 μs** |  **1.01** |    **0.16** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        43.79 μs |    117.76 μs |     6.455 μs |  1.12 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **46.68 μs** |     **19.68 μs** |     **1.079 μs** |  **1.00** |    **0.03** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        44.81 μs |     63.66 μs |     3.489 μs |  0.96 |    0.07 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **46.25 μs** |    **114.12 μs** |     **6.255 μs** |  **1.01** |    **0.17** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        43.12 μs |    115.42 μs |     6.327 μs |  0.94 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **44.86 μs** |     **91.74 μs** |     **5.028 μs** |  **1.01** |    **0.14** |    **3072 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        46.96 μs |    115.64 μs |     6.339 μs |  1.06 |    0.16 |    2312 B |        0.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.816 μs |   9.791 μs |  0.5367 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.411 μs |  43.705 μs |  2.3956 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.171 μs |  14.246 μs |  0.7809 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.412 μs |   6.389 μs |  0.3502 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.281 μs |  45.223 μs |  2.4788 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.945 μs |  11.239 μs |  0.6161 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.659 μs |   1.642 μs |  0.0900 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 38.639 μs | 249.912 μs | 13.6985 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.010 μs |   2.346 μs |  0.1286 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.394 μs |   1.045 μs |  0.0573 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.073 μs |  17.785 μs |  0.9749 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 39.089 μs |  91.759 μs |  5.0296 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.666 μs |   1.827 μs |  0.1002 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.263 μs |  12.531 μs |  0.6869 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,531.2 ns |  5,304.8 ns | 290.78 ns |  0.36 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,803.2 ns |  4,663.8 ns | 255.64 ns |  0.42 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,404.2 ns |  3,802.6 ns | 208.43 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,586.2 ns |  1,114.7 ns |  61.10 ns |  0.61 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    827.0 ns |  1,895.9 ns | 103.92 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,024.0 ns | 11,782.2 ns | 645.82 ns |  9.66 |    0.58 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,259.8 ns |  5,187.1 ns | 284.32 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,011.3 ns |  1,836.5 ns | 100.66 ns |  0.94 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.949 μs |   3.690 μs |  0.2023 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 914.438 μs | 130.615 μs |  7.1595 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.130 μs |  18.419 μs |  1.0096 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 250.726 μs | 335.681 μs | 18.3998 μs |      80 B |


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