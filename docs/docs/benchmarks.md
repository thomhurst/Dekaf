---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 18:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,268.6 μs** |    **382.94 μs** |    **20.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,787.1 μs |  1,370.39 μs |    75.12 μs |  0.44 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,454.0 μs** |    **723.45 μs** |    **39.65 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,938.8 μs |  3,125.63 μs |   171.33 μs |  0.53 |    0.02 |  15.6250 |       - |  347233 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,220.1 μs** |  **1,056.60 μs** |    **57.92 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,370.7 μs |  7,449.19 μs |   408.32 μs |  0.54 |    0.06 |        - |       - |   37359 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,776.8 μs** |  **4,002.08 μs** |   **219.37 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,859.1 μs | 12,014.83 μs |   658.57 μs |  1.01 |    0.05 |  15.6250 |       - |  468574 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **152.5 μs** |     **73.07 μs** |     **4.00 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    151.3 μs |     72.19 μs |     3.96 μs |  0.99 |    0.03 |        - |       - |    4101 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,264.3 μs** |  **2,577.30 μs** |   **141.27 μs** |  **1.01** |    **0.14** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,260.6 μs |  2,775.06 μs |   152.11 μs |  1.01 |    0.14 |        - |       - |   44366 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,095.1 μs** |    **103.90 μs** |     **5.69 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125545 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    910.1 μs |    585.53 μs |    32.09 μs |  0.83 |    0.03 |        - |       - |    8080 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,304.6 μs** | **23,363.61 μs** | **1,280.64 μs** |  **1.01** |    **0.16** |  **74.2188** |       **-** | **1255963 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,762.7 μs |  6,130.31 μs |   336.02 μs |  0.96 |    0.11 |        - |       - |   61161 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,497.3 μs** |     **63.23 μs** |     **3.47 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,607.2 μs |    225.16 μs |    12.34 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,498.2 μs** |    **112.09 μs** |     **6.14 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,604.2 μs |    131.32 μs |     7.20 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,515.3 μs** |    **247.36 μs** |    **13.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,605.0 μs |     36.43 μs |     2.00 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,506.2 μs** |    **140.24 μs** |     **7.69 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,624.1 μs |    116.75 μs |     6.40 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **131.7 μs** |  **1,043.9 μs** |  **57.22 μs** |   **102.7 μs** |  **1.11** |    **0.55** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   193.8 μs |    215.1 μs |  11.79 μs |   190.6 μs |  1.64 |    0.51 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.3 μs** |    **652.1 μs** |  **35.74 μs** |   **146.3 μs** |  **1.04** |    **0.30** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.6 μs |    205.5 μs |  11.27 μs |   173.6 μs |  1.21 |    0.25 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,176.1 μs** |  **3,680.0 μs** | **201.71 μs** | **1,168.2 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,296.5 μs |  2,110.8 μs | 115.70 μs | 1,241.2 μs |  1.12 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,561.9 μs** | **10,882.4 μs** | **596.50 μs** | **1,408.4 μs** |  **1.10** |    **0.50** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,221.5 μs |    852.6 μs |  46.73 μs | 2,214.1 μs |  1.56 |    0.48 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **902.1 ns** |   **815.4 ns** |  **44.70 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,261.0 ns | 2,534.7 ns | 138.94 ns |  2.51 |    0.17 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,411.9 ns** |   **389.9 ns** |  **21.37 ns** |  **1.00** |    **0.02** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,430.8 ns |   440.9 ns |  24.17 ns |  2.43 |    0.04 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.948 μs |  48.911 μs |  2.6810 μs | 26.690 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.543 μs |   2.027 μs |  0.1111 μs | 10.499 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 11.351 μs |  50.664 μs |  2.7771 μs | 12.883 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.300 μs |   1.114 μs |  0.0611 μs |  8.270 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.477 μs |   1.956 μs |  0.1072 μs | 11.421 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.165 μs |   4.806 μs |  0.2634 μs | 13.055 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.000 μs |   8.074 μs |  0.4425 μs | 13.203 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.356 μs |   9.108 μs |  0.4992 μs | 35.286 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.904 μs |   2.381 μs |  0.1305 μs |  8.917 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.392 μs |  70.445 μs |  3.8613 μs | 20.178 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.071 μs |   5.826 μs |  0.3193 μs | 20.128 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.413 μs |   4.167 μs |  0.2284 μs | 22.346 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.950 μs |   3.187 μs |  0.1747 μs |  4.870 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 21.687 μs | 218.502 μs | 11.9768 μs | 14.888 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,607.45 ns | 99.082 ns | 5.431 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |  0.708 ns | 0.039 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.27 ns |  0.243 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.11 ns |  0.971 ns | 0.053 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.29 ns | 10.558 ns | 0.579 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.80 ns |  0.950 ns | 0.052 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    113.59 ns | 42.967 ns | 2.355 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.26 ns | 10.279 ns | 0.563 ns |  0.68 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error       | StdDev     | Allocated |
|------------------------ |-----------:|------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.191 μs |   0.9585 μs |  0.0525 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 508.995 μs | 373.6921 μs | 20.4833 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.283 μs |   2.7158 μs |  0.1489 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.899 μs |  26.7704 μs |  1.4674 μs |      80 B |


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