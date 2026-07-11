---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 12:02 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,157.2 μs** |   **392.58 μs** |  **21.52 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,664.9 μs | 3,413.58 μs | 187.11 μs |  0.27 |    0.03 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,391.0 μs** |   **204.00 μs** |  **11.18 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,647.3 μs | 4,086.87 μs | 224.02 μs |  0.36 |    0.03 |  15.6250 |       - |  341.64 KB |        0.32 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,279.2 μs** |   **426.47 μs** |  **23.38 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,942.2 μs | 3,167.47 μs | 173.62 μs |  0.31 |    0.02 |        - |       - |   38.39 KB |        0.20 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,475.1 μs** | **3,327.82 μs** | **182.41 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,670.1 μs | 6,996.55 μs | 383.50 μs |  0.61 |    0.03 |  15.6250 |       - |  393.56 KB |        0.20 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.8 μs** |   **117.55 μs** |   **6.44 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.6 μs |   210.86 μs |  11.56 μs |  0.77 |    0.08 |        - |       - |    4.71 KB |        0.14 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,347.1 μs** |   **662.90 μs** |  **36.34 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    968.0 μs | 2,809.94 μs | 154.02 μs |  0.72 |    0.10 |        - |       - |  111.95 KB |        0.33 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,083.9 μs** |    **55.23 μs** |   **3.03 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    417.4 μs | 1,419.44 μs |  77.80 μs |  0.39 |    0.06 |        - |       - |    83.1 KB |        0.68 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,247.4 μs** | **1,612.16 μs** |  **88.37 μs** |  **1.00** |    **0.05** |  **70.3125** |       **-** | **1210.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,879.5 μs | 8,344.53 μs | 457.39 μs |  1.73 |    0.19 |        - |       - |  912.42 KB |        0.75 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,417.3 μs** |    **67.91 μs** |   **3.72 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,121.0 μs |   163.11 μs |   8.94 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,425.4 μs** |   **248.10 μs** |  **13.60 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,115.2 μs |    34.44 μs |   1.89 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.7 μs** |   **182.92 μs** |  **10.03 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.24 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.2 μs |    19.42 μs |   1.06 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.49 |
|                         |               |             |           |             |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,416.0 μs** |    **14.69 μs** |   **0.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,197.9 μs |   450.86 μs |  24.71 μs |  0.22 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **109.0 μs** |    **271.7 μs** |  **14.89 μs** |   **108.6 μs** |  **1.01** |    **0.17** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   202.3 μs |    806.6 μs |  44.21 μs |   202.8 μs |  1.88 |    0.42 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **163.9 μs** |    **585.3 μs** |  **32.08 μs** |   **156.4 μs** |  **1.02** |    **0.24** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   226.5 μs |  1,152.2 μs |  63.16 μs |   193.8 μs |  1.42 |    0.41 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **935.3 μs** |  **3,083.3 μs** | **169.01 μs** |   **842.6 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,504.9 μs |  2,907.1 μs | 159.35 μs | 1,537.3 μs |  1.64 |    0.28 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,432.9 μs** | **12,860.7 μs** | **704.94 μs** | **1,037.8 μs** |  **1.14** |    **0.64** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,984.2 μs | 14,780.8 μs | 810.18 μs | 1,530.4 μs |  1.58 |    0.79 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **923.4 ns** |   **528.9 ns** |  **28.99 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 3,305.7 ns |   735.5 ns |  40.32 ns |  3.58 |    0.11 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,418.2 ns** |   **430.4 ns** |  **23.59 ns** |  **1.00** |    **0.02** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 4,591.1 ns | 3,991.8 ns | 218.81 ns |  3.24 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.270 μs |  3.529 μs | 0.1934 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.493 μs | 33.737 μs | 1.8492 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.788 μs |  1.934 μs | 0.1060 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.021 μs | 40.303 μs | 2.2091 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.435 μs |  3.326 μs | 0.1823 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.743 μs |  6.352 μs | 0.3482 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.864 μs |  6.408 μs | 0.3513 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.407 μs | 12.997 μs | 0.7124 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.047 μs | 63.407 μs | 3.4755 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.265 μs |  1.917 μs | 0.1051 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.578 μs | 33.886 μs | 1.8574 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.227 μs | 43.908 μs | 2.4068 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.276 μs |  1.417 μs | 0.0777 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.908 μs | 19.703 μs | 1.0800 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,773.74 ns | 1,128.007 ns | 61.830 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |     0.092 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.21 ns |     0.611 ns |  0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.66 ns |     1.926 ns |  0.106 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     37.28 ns |    17.237 ns |  0.945 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |     0.531 ns |  0.029 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    127.52 ns |    70.727 ns |  3.877 ns |  1.00 |    0.04 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.30 ns |     1.300 ns |  0.071 ns |  0.59 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.847 μs |  29.242 μs |  1.603 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 522.563 μs | 321.722 μs | 17.635 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.301 μs |  18.388 μs |  1.008 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 256.062 μs | 142.770 μs |  7.826 μs |      80 B |


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