---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 10:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,294.17 μs** |    **858.11 μs** |    **47.036 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,651.23 μs |  2,861.35 μs |   156.840 μs |  0.26 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,385.20 μs** |  **2,053.09 μs** |   **112.537 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,605.80 μs |    458.98 μs |    25.158 μs |  0.35 |    0.01 |  15.6250 |       - |  341.29 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,186.48 μs** |    **399.20 μs** |    **21.882 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,807.33 μs |  1,070.04 μs |    58.652 μs |  0.29 |    0.01 |        - |       - |   38.33 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,827.76 μs** |    **438.54 μs** |    **24.038 μs** |  **1.00** |    **0.00** | **109.3750** | **46.8750** | **1937.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,708.28 μs |  7,189.40 μs |   394.075 μs |  0.60 |    0.03 |  15.6250 |       - |  394.77 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.45 μs** |     **27.46 μs** |     **1.505 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     98.29 μs |    174.63 μs |     9.572 μs |  0.73 |    0.06 |        - |       - |    5.28 KB |        0.16 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,370.63 μs** |    **371.36 μs** |    **20.356 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,010.85 μs |  1,873.90 μs |   102.715 μs |  0.74 |    0.07 |        - |       - |  117.03 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,132.52 μs** |    **139.42 μs** |     **7.642 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    394.17 μs |    810.19 μs |    44.409 μs |  0.35 |    0.03 |        - |       - |   95.49 KB |        0.78 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,140.57 μs** | **31,843.80 μs** | **1,745.466 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1226.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,783.99 μs |  9,771.88 μs |   535.630 μs |  0.38 |    0.08 |        - |       - | 1002.19 KB |        0.82 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,546.92 μs** |    **893.53 μs** |    **48.977 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,125.71 μs |    214.82 μs |    11.775 μs |  0.20 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,435.54 μs** |    **132.59 μs** |     **7.268 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,118.11 μs |     43.04 μs |     2.359 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,444.47 μs** |    **207.19 μs** |    **11.357 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,425.78 μs |    433.20 μs |    23.745 μs |  0.26 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,469.76 μs** |    **304.23 μs** |    **16.676 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,342.03 μs |    711.29 μs |    38.988 μs |  0.25 |    0.01 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **153.9 μs** |    **459.2 μs** |  **25.17 μs** |  **1.02** |    **0.21** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   214.9 μs |    501.5 μs |  27.49 μs |  1.42 |    0.27 |    42.4 KB |        0.65 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.6 μs** |    **729.1 μs** |  **39.97 μs** |  **1.04** |    **0.33** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   261.3 μs |  1,317.5 μs |  72.22 μs |  1.80 |    0.58 |  218.18 KB |        0.91 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **973.9 μs** |  **2,621.5 μs** | **143.69 μs** |  **1.01** |    **0.18** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,551.5 μs |  7,987.6 μs | 437.83 μs |  1.61 |    0.44 |  429.68 KB |        0.66 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,675.6 μs** | **11,106.3 μs** | **608.78 μs** |  **1.10** |    **0.51** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,423.1 μs |    571.7 μs |  31.34 μs |  0.93 |    0.30 | 2187.49 KB |        0.91 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **890.2 ns** |  **1,389.1 ns** |  **76.14 ns** |  **1.01** |    **0.11** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 3,806.6 ns | 10,618.0 ns | 582.01 ns |  4.30 |    0.66 |      - |     496 B |        0.77 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,482.8 ns** |  **2,675.7 ns** | **146.66 ns** |  **1.01** |    **0.12** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 4,739.7 ns |  4,854.9 ns | 266.11 ns |  3.22 |    0.32 | 0.1000 |    2300 B |        0.94 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 30.200 μs |   0.1110 μs | 0.0061 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.288 μs |   3.2302 μs | 0.1771 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.863 μs |   2.0184 μs | 0.1106 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 11.331 μs |  46.0663 μs | 2.5250 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.351 μs |   2.8544 μs | 0.1565 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.688 μs |   5.7524 μs | 0.3153 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.989 μs |   4.6763 μs | 0.2563 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.622 μs | 123.0590 μs | 6.7453 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.980 μs |   0.1053 μs | 0.0058 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.408 μs |   1.7451 μs | 0.0957 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.275 μs |  34.7912 μs | 1.9070 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.965 μs |  69.2048 μs | 3.7933 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.584 μs |  11.6392 μs | 0.6380 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.419 μs |  21.3313 μs | 1.1692 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,444.17 ns | 264.671 ns | 14.508 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.56 ns |   0.996 ns |  0.055 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.63 ns |   0.142 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.57 ns |   1.425 ns |  0.078 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.50 ns |   8.837 ns |  0.484 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.325 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.12 ns |  39.076 ns |  2.142 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.11 ns |   0.473 ns |  0.026 ns |  0.69 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.381 μs |   7.954 μs |  0.4360 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 504.581 μs | 243.526 μs | 13.3485 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.744 μs |   3.224 μs |  0.1767 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 234.919 μs | 178.284 μs |  9.7724 μs |      80 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*