---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 23:29 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,918.10 μs** |    **160.82 μs** |   **8.815 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,588.86 μs |    848.18 μs |  46.491 μs |  0.44 |    0.01 |        - |       - |   35161 B |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,097.27 μs** |    **258.78 μs** |  **14.184 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,672.52 μs |    910.76 μs |  49.922 μs |  0.52 |    0.01 |  15.6250 |       - |  347194 B |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,720.29 μs** |  **9,122.77 μs** | **500.050 μs** |  **1.00** |    **0.09** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,545.01 μs |  2,274.60 μs | 124.678 μs |  0.38 |    0.03 |        - |       - |   37788 B |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,891.82 μs** |  **2,432.98 μs** | **133.360 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,350.23 μs | 13,762.51 μs | 754.370 μs |  1.39 |    0.08 |        - |       - |  469364 B |        0.24 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **89.69 μs** |     **44.74 μs** |   **2.453 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    132.30 μs |    615.86 μs |  33.757 μs |  1.48 |    0.33 |        - |       - |    4139 B |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **861.11 μs** |    **958.85 μs** |  **52.558 μs** |  **1.00** |    **0.08** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    975.63 μs |  1,019.96 μs |  55.907 μs |  1.14 |    0.08 |        - |       - |   43192 B |        0.13 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **740.93 μs** |    **232.39 μs** |  **12.738 μs** |  **1.00** |    **0.02** |   **7.3242** |       **-** |  **125371 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    823.41 μs |    558.82 μs |  30.631 μs |  1.11 |    0.04 |        - |       - |    5666 B |        0.05 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,501.10 μs** |  **3,049.18 μs** | **167.136 μs** |  **1.00** |    **0.03** |  **74.2188** |       **-** | **1251598 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,659.06 μs |  1,551.77 μs |  85.058 μs |  1.15 |    0.02 |        - |       - |   60594 B |        0.05 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,424.66 μs** |    **242.00 μs** |  **13.265 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,421.20 μs |    169.78 μs |   9.306 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,408.16 μs** |    **502.26 μs** |  **27.530 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,453.59 μs |     36.84 μs |   2.019 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,409.64 μs** |    **239.47 μs** |  **13.126 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,434.37 μs |     82.59 μs |   4.527 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |              |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,398.65 μs** |    **302.36 μs** |  **16.573 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,425.24 μs |    101.44 μs |   5.560 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean        | Error        | StdDev     | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |------------:|-------------:|-----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **90.51 μs** |    **513.17 μs** |  **28.129 μs** |    **76.25 μs** |  **1.06** |    **0.38** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   138.99 μs |    261.69 μs |  14.344 μs |   146.65 μs |  1.62 |    0.40 |   39.98 KB |        0.62 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **126.53 μs** |    **642.09 μs** |  **35.195 μs** |   **137.89 μs** |  **1.06** |    **0.40** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   143.30 μs |     64.06 μs |   3.511 μs |   142.69 μs |  1.20 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **664.26 μs** |  **3,114.10 μs** | **170.695 μs** |   **567.29 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   914.56 μs |  1,960.79 μs | 107.477 μs |   857.70 μs |  1.43 |    0.31 |  476.66 KB |        0.73 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,239.91 μs** | **14,199.29 μs** | **778.311 μs** |   **794.76 μs** |  **1.24** |    **0.87** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,507.48 μs | 11,406.64 μs | 625.237 μs | 1,149.43 μs |  1.51 |    0.83 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **648.6 ns** | **1,516.91 ns** | **83.15 ns** |  **1.01** |    **0.17** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,358.5 ns |    87.06 ns |  4.77 ns |  2.12 |    0.25 |      - |     452 B |        0.70 |
|                      |             |            |             |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,042.7 ns** |   **213.98 ns** | **11.73 ns** |  **1.00** |    **0.01** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,777.0 ns | 1,620.04 ns | 88.80 ns |  2.66 |    0.08 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 34.957 μs | 278.3351 μs | 15.2565 μs | 26.163 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.333 μs |   4.1553 μs |  0.2278 μs | 10.370 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.626 μs |   1.5598 μs |  0.0855 μs |  8.637 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.254 μs |   0.6407 μs |  0.0351 μs |  8.251 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.956 μs |   6.0060 μs |  0.3292 μs | 10.830 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.841 μs |  70.5171 μs |  3.8653 μs | 12.844 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.462 μs |  53.2040 μs |  2.9163 μs | 13.190 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.455 μs |   7.2949 μs |  0.3999 μs | 27.391 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.989 μs |   0.4591 μs |  0.0252 μs |  8.986 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 30.587 μs | 328.1502 μs | 17.9870 μs | 20.297 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.236 μs |  49.1400 μs |  2.6935 μs | 24.715 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.585 μs |  53.3486 μs |  2.9242 μs | 28.218 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 | 11.585 μs | 196.4085 μs | 10.7658 μs |  5.840 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.942 μs |   6.8673 μs |  0.3764 μs | 14.912 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,977.45 ns | 709.808 ns | 38.907 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.60 ns |   2.457 ns |  0.135 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.27 ns |   0.572 ns |  0.031 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.95 ns |   1.505 ns |  0.083 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.98 ns |   8.569 ns |  0.470 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.81 ns |   1.199 ns |  0.066 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.07 ns |  21.410 ns |  1.174 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     74.68 ns |   0.184 ns |  0.010 ns |  0.67 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.960 μs |  10.536 μs |  0.5775 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 514.984 μs | 532.882 μs | 29.2091 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.528 μs |  15.854 μs |  0.8690 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 237.034 μs |  82.510 μs |  4.5226 μs |      80 B |


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