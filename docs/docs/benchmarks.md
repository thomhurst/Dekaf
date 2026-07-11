---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 23:59 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,112.54 μs** |    **255.45 μs** |    **14.002 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,677.01 μs |  2,153.34 μs |   118.032 μs |  0.27 |    0.02 |        - |       - |    34.7 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,196.77 μs** |    **585.72 μs** |    **32.105 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,452.51 μs |  2,213.28 μs |   121.317 μs |  0.34 |    0.01 |  15.6250 |       - |  341.48 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,630.44 μs** |    **393.28 μs** |    **21.557 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,775.85 μs |  1,873.20 μs |   102.677 μs |  0.27 |    0.01 |        - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,416.48 μs** |  **2,333.88 μs** |   **127.928 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,856.91 μs | 11,580.98 μs |   634.793 μs |  0.60 |    0.05 |  15.6250 |       - |  378.91 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **118.01 μs** |     **57.30 μs** |     **3.141 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     99.20 μs |    190.45 μs |    10.439 μs |  0.84 |    0.08 |        - |       - |    5.85 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,066.25 μs** |  **2,462.11 μs** |   **134.957 μs** |  **1.01** |    **0.16** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    874.22 μs |    913.43 μs |    50.068 μs |  0.83 |    0.11 |        - |       - |    44.2 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **980.25 μs** |     **52.15 μs** |     **2.858 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.41 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    265.37 μs |    361.01 μs |    19.788 μs |  0.27 |    0.02 |   0.9766 |       - |   91.48 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,781.13 μs** | **33,992.99 μs** | **1,863.270 μs** |  **1.04** |    **0.29** |  **74.2188** |       **-** | **1225.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,021.32 μs |  7,568.13 μs |   414.835 μs |  0.36 |    0.09 |   7.8125 |       - |  879.05 KB |        0.72 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.95 μs** |     **64.78 μs** |     **3.551 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,366.18 μs |    119.44 μs |     6.547 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.86 μs** |     **29.80 μs** |     **1.633 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,109.72 μs |    111.75 μs |     6.125 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.53 μs** |    **810.35 μs** |    **44.418 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,107.61 μs |     80.95 μs |     4.437 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,402.77 μs** |     **44.24 μs** |     **2.425 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,105.74 μs |     20.47 μs |     1.122 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **115.5 μs** |    **477.7 μs** |  **26.19 μs** |  **1.03** |    **0.27** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   170.0 μs |    375.4 μs |  20.58 μs |  1.52 |    0.31 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.6 μs** |    **697.0 μs** |  **38.20 μs** |  **1.05** |    **0.34** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   189.3 μs |    283.4 μs |  15.53 μs |  1.30 |    0.33 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **834.5 μs** |  **3,891.5 μs** | **213.31 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,006.7 μs |    389.1 μs |  21.33 μs |  1.25 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,655.5 μs** | **13,568.9 μs** | **743.76 μs** |  **1.14** |    **0.63** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,350.9 μs |    796.7 μs |  43.67 μs |  0.93 |    0.35 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **755.7 ns** |   **356.77 ns** |  **19.56 ns** |  **1.00** |    **0.03** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,675.2 ns |    14.57 ns |   0.80 ns |  2.22 |    0.05 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,310.0 ns** |   **459.81 ns** |  **25.20 ns** |  **1.00** |    **0.02** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,056.5 ns | 2,881.44 ns | 157.94 ns |  2.33 |    0.11 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.143 μs |   1.242 μs |  0.0681 μs | 26.120 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.492 μs |   1.150 μs |  0.0630 μs | 11.472 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 18.114 μs | 296.971 μs | 16.2780 μs |  8.836 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.614 μs |   3.440 μs |  0.1886 μs |  8.521 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.944 μs |  35.613 μs |  1.9521 μs | 11.832 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.412 μs |   4.859 μs |  0.2663 μs | 13.556 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 17.128 μs |  74.971 μs |  4.1094 μs | 19.246 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 40.392 μs | 421.279 μs | 23.0917 μs | 27.181 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.294 μs |   8.501 μs |  0.4660 μs |  9.136 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.389 μs |   4.009 μs |  0.2198 μs | 20.439 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 35.937 μs |  22.664 μs |  1.2423 μs | 36.515 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.051 μs |  37.821 μs |  2.0731 μs | 36.929 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.462 μs |   4.386 μs |  0.2404 μs |  5.365 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.103 μs |  13.985 μs |  0.7666 μs | 12.092 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,438.09 ns | 540.891 ns | 29.648 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.56 ns |   1.146 ns |  0.063 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.31 ns |   0.420 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.68 ns |   6.120 ns |  0.335 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     39.94 ns |   1.599 ns |  0.088 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.283 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    144.77 ns |  70.259 ns |  3.851 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.05 ns |   0.563 ns |  0.031 ns |  0.52 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.878 μs |  35.375 μs | 1.9390 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 509.483 μs | 145.623 μs | 7.9821 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.662 μs |   5.475 μs | 0.3001 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 229.196 μs |   6.486 μs | 0.3555 μs |      80 B |


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