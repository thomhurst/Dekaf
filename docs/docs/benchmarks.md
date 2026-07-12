---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 22:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,341.3 μs** |  **1,143.56 μs** |    **62.68 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,877.7 μs |  3,272.80 μs |   179.39 μs |  0.30 |    0.02 |        - |       - |   35224 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,416.0 μs** |    **731.79 μs** |    **40.11 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,505.9 μs |  6,255.39 μs |   342.88 μs |  0.47 |    0.04 |  15.6250 |       - |  347685 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,550.8 μs** |  **7,297.76 μs** |   **400.01 μs** |  **1.00** |    **0.07** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,706.5 μs |    118.91 μs |     6.52 μs |  0.26 |    0.01 |        - |       - |   37365 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,300.4 μs** |  **1,870.81 μs** |   **102.55 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,312.7 μs | 26,164.23 μs | 1,434.15 μs |  0.84 |    0.10 |        - |       - |  370959 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.2 μs** |     **25.17 μs** |     **1.38 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    142.6 μs |    102.29 μs |     5.61 μs |  1.10 |    0.04 |        - |       - |    4218 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,364.9 μs** |    **428.18 μs** |    **23.47 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,314.6 μs |  2,201.03 μs |   120.65 μs |  0.96 |    0.08 |        - |       - |   42856 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,038.0 μs** |    **124.46 μs** |     **6.82 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125415 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    898.1 μs |    823.58 μs |    45.14 μs |  0.87 |    0.04 |        - |       - |    6659 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,435.0 μs** | **27,869.28 μs** | **1,527.61 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1254781 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,400.4 μs | 27,110.56 μs | 1,486.02 μs |  0.91 |    0.20 |        - |       - |   83951 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,489.2 μs** |    **200.06 μs** |    **10.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,484.1 μs |     55.31 μs |     3.03 μs |  0.27 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,487.8 μs** |    **169.09 μs** |     **9.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,600.6 μs |  2,801.60 μs |   153.57 μs |  0.29 |    0.02 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,490.1 μs** |     **99.68 μs** |     **5.46 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,510.1 μs |    218.65 μs |    11.98 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,483.8 μs** |     **45.70 μs** |     **2.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,514.6 μs |     80.34 μs |     4.40 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.40 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **126.9 μs** |    **472.49 μs** |  **25.90 μs** |   **112.5 μs** |  **1.03** |    **0.24** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   173.6 μs |    586.62 μs |  32.15 μs |   169.4 μs |  1.40 |    0.32 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **180.9 μs** |    **474.49 μs** |  **26.01 μs** |   **194.1 μs** |  **1.02** |    **0.19** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   191.2 μs |    222.03 μs |  12.17 μs |   186.3 μs |  1.07 |    0.16 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **936.9 μs** |  **3,178.41 μs** | **174.22 μs** |   **851.0 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,152.5 μs |    279.75 μs |  15.33 μs | 1,149.5 μs |  1.26 |    0.18 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,524.4 μs** | **13,674.15 μs** | **749.53 μs** | **1,097.1 μs** |  **1.14** |    **0.64** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,448.7 μs |     71.54 μs |   3.92 μs | 1,447.5 μs |  1.09 |    0.36 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **981.5 ns** | **1,202.6 ns** |  **65.92 ns** |  **1.00** |    **0.08** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,993.4 ns |   149.4 ns |   8.19 ns |  2.04 |    0.12 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,454.7 ns** |   **854.6 ns** |  **46.84 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,339.3 ns | 2,870.2 ns | 157.32 ns |  2.30 |    0.11 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.073 μs |  0.2809 μs | 0.0154 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.546 μs | 37.8720 μs | 2.0759 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.586 μs |  2.1048 μs | 0.1154 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.478 μs |  2.0506 μs | 0.1124 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.632 μs |  1.6621 μs | 0.0911 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.624 μs |  3.0473 μs | 0.1670 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 16.781 μs | 62.3877 μs | 3.4197 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.721 μs |  1.8011 μs | 0.0987 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.896 μs |  0.6320 μs | 0.0346 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.222 μs |  2.4187 μs | 0.1326 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.138 μs | 17.2950 μs | 0.9480 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.580 μs | 32.8100 μs | 1.7984 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.211 μs |  0.9362 μs | 0.0513 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.779 μs |  6.4874 μs | 0.3556 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,148.92 ns | 740.812 ns | 40.606 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |   0.176 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.20 ns |   0.139 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.44 ns |   1.409 ns |  0.077 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.42 ns |   8.694 ns |  0.477 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.076 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.53 ns |  30.246 ns |  1.658 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.77 ns |   0.759 ns |  0.042 ns |  0.71 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.338 μs |   1.286 μs |  0.0705 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.069 μs | 416.149 μs | 22.8105 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.979 μs |   4.568 μs |  0.2504 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.371 μs |  22.640 μs |  1.2410 μs |      80 B |


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