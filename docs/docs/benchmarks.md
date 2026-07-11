---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 19:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,201.9 μs** |    **465.90 μs** |    **25.54 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,707.7 μs |  2,930.68 μs |   160.64 μs |  0.28 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,366.9 μs** |     **76.08 μs** |     **4.17 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,596.3 μs |  1,468.64 μs |    80.50 μs |  0.35 |    0.01 |  15.6250 |       - |  341.31 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,717.8 μs** | **11,822.51 μs** |   **648.03 μs** |  **1.01** |    **0.12** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,757.2 μs |    734.58 μs |    40.26 μs |  0.26 |    0.02 |        - |       - |   38.17 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,327.8 μs** |  **1,557.95 μs** |    **85.40 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,825.1 μs |  4,634.31 μs |   254.02 μs |  0.63 |    0.02 |  15.6250 |       - |  392.24 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.5 μs** |     **24.31 μs** |     **1.33 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.6 μs |    413.06 μs |    22.64 μs |  0.91 |    0.15 |        - |       - |    5.01 KB |        0.15 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,386.6 μs** |    **328.89 μs** |    **18.03 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    920.4 μs |  2,885.94 μs |   158.19 μs |  0.66 |    0.10 |        - |       - |  113.67 KB |        0.34 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **600.7 μs** |  **7,979.50 μs** |   **437.38 μs** |  **1.45** |    **1.37** |   **7.3242** |       **-** |  **122.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    350.9 μs |    611.20 μs |    33.50 μs |  0.85 |    0.52 |        - |       - |   97.63 KB |        0.80 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,823.9 μs** | **51,442.55 μs** | **2,819.74 μs** |  **1.05** |    **0.35** |  **74.2188** |       **-** |  **1226.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,118.3 μs | 13,600.76 μs |   745.50 μs |  0.30 |    0.10 |        - |       - |   945.8 KB |        0.77 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,436.8 μs** |    **141.66 μs** |     **7.76 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,408.5 μs |    231.19 μs |    12.67 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,444.6 μs** |    **444.66 μs** |    **24.37 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,416.5 μs |    228.04 μs |    12.50 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,495.5 μs** |    **504.98 μs** |    **27.68 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,402.5 μs |    257.12 μs |    14.09 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,493.5 μs** |    **234.67 μs** |    **12.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,402.5 μs |    534.30 μs |    29.29 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **142.4 μs** |    **660.67 μs** |  **36.21 μs** |   **149.1 μs** |  **1.05** |    **0.35** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   184.7 μs |    124.10 μs |   6.80 μs |   186.6 μs |  1.36 |    0.33 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **183.1 μs** |    **862.63 μs** |  **47.28 μs** |   **175.7 μs** |  **1.04** |    **0.33** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.5 μs |    205.41 μs |  11.26 μs |   174.9 μs |  1.01 |    0.23 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **971.6 μs** |  **2,733.75 μs** | **149.85 μs** |   **906.7 μs** |  **1.01** |    **0.19** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,170.8 μs |    140.06 μs |   7.68 μs | 1,168.3 μs |  1.22 |    0.15 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,475.0 μs** | **12,610.65 μs** | **691.23 μs** | **1,085.6 μs** |  **1.13** |    **0.60** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,416.9 μs |     61.94 μs |   3.40 μs | 1,417.1 μs |  1.09 |    0.35 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **862.6 ns** |   **897.5 ns** |  **49.19 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,097.4 ns | 2,285.6 ns | 125.28 ns |  2.44 |    0.18 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,428.4 ns** | **1,443.9 ns** |  **79.15 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,407.0 ns | 8,045.6 ns | 441.01 ns |  2.39 |    0.29 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.320 μs | 49.348 μs | 2.7049 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 14.885 μs |  3.745 μs | 0.2053 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.670 μs |  1.281 μs | 0.0702 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.796 μs |  4.399 μs | 0.2411 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.514 μs | 34.230 μs | 1.8762 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.699 μs | 11.841 μs | 0.6490 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.271 μs | 12.242 μs | 0.6710 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.252 μs |  1.558 μs | 0.0854 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.779 μs | 28.203 μs | 1.5459 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.114 μs |  1.475 μs | 0.0808 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.829 μs | 60.587 μs | 3.3210 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.884 μs | 26.482 μs | 1.4516 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.967 μs |  8.323 μs | 0.4562 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.952 μs | 23.675 μs | 1.2977 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,325.20 ns | 225.053 ns | 12.336 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.80 ns |   4.903 ns |  0.269 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.21 ns |   0.123 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.95 ns |   0.511 ns |  0.028 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.73 ns |  15.831 ns |  0.868 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |   1.045 ns |  0.057 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    121.17 ns |  88.493 ns |  4.851 ns |  1.00 |    0.05 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.47 ns |   4.195 ns |  0.230 ns |  0.63 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.660 μs |  10.155 μs |  0.5566 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 513.023 μs | 354.449 μs | 19.4285 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.992 μs |   1.169 μs |  0.0641 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 234.144 μs | 183.966 μs | 10.0838 μs |      80 B |


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