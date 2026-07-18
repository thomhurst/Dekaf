---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-18 00:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,021.31 μs** |   **264.85 μs** |  **14.517 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109100 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,565.67 μs |   583.11 μs |  31.962 μs |  0.43 |    0.00 |        - |       - |   35184 B |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,134.97 μs** |   **916.04 μs** |  **50.211 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,731.65 μs | 2,584.11 μs | 141.644 μs |  0.52 |    0.02 |  15.6250 |       - |  347350 B |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,562.37 μs** | **1,941.63 μs** | **106.427 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,809.47 μs |   919.20 μs |  50.385 μs |  0.43 |    0.01 |        - |       - |   37956 B |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **9,152.13 μs** |   **477.54 μs** |  **26.176 μs** |  **1.00** |    **0.00** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,495.71 μs | 5,326.52 μs | 291.965 μs |  1.26 |    0.03 |  15.6250 |       - |  470279 B |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **89.21 μs** |    **53.91 μs** |   **2.955 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     95.87 μs |   110.35 μs |   6.049 μs |  1.08 |    0.07 |        - |       - |    4158 B |        0.12 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **824.44 μs** |   **363.15 μs** |  **19.906 μs** |  **1.00** |    **0.03** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,099.80 μs | 1,483.18 μs |  81.298 μs |  1.33 |    0.09 |        - |       - |   42140 B |        0.12 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **766.45 μs** |   **381.13 μs** |  **20.891 μs** |  **1.00** |    **0.03** |   **7.3242** |       **-** |  **125516 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    932.44 μs |   396.86 μs |  21.753 μs |  1.22 |    0.04 |        - |       - |    5750 B |        0.05 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,579.40 μs** | **3,179.44 μs** | **174.276 μs** |  **1.00** |    **0.03** |  **74.2188** |       **-** | **1252036 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,304.80 μs | 7,237.79 μs | 396.728 μs |  1.10 |    0.05 |        - |       - |   59400 B |        0.05 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,409.16 μs** |   **284.40 μs** |  **15.589 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,429.26 μs |   207.93 μs |  11.397 μs |  0.45 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.19 μs** |    **39.00 μs** |   **2.138 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,425.99 μs |    90.84 μs |   4.979 μs |  0.45 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.25 μs** |   **332.54 μs** |  **18.228 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,481.96 μs |   406.41 μs |  22.277 μs |  0.46 |    0.00 |        - |       - |     792 B |        0.38 |
|                         |               |             |           |              |             |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,406.82 μs** |   **159.58 μs** |   **8.747 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,523.18 μs | 2,524.37 μs | 138.369 μs |  0.47 |    0.02 |        - |       - |     802 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **102.1 μs** |    **489.3 μs** |  **26.82 μs** |   **115.84 μs** |  **1.06** |    **0.38** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   132.3 μs |    200.0 μs |  10.96 μs |   134.44 μs |  1.37 |    0.38 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **126.1 μs** |    **920.0 μs** |  **50.43 μs** |    **98.75 μs** |  **1.10** |    **0.50** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   199.9 μs |  1,145.3 μs |  62.78 μs |   170.78 μs |  1.74 |    0.69 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **681.6 μs** |  **3,249.4 μs** | **178.11 μs** |   **593.56 μs** |  **1.04** |    **0.32** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   905.6 μs |  1,907.7 μs | 104.57 μs |   845.36 μs |  1.38 |    0.31 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,219.5 μs** | **12,326.0 μs** | **675.63 μs** |   **831.14 μs** |  **1.18** |    **0.74** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,574.8 μs | 11,185.2 μs | 613.10 μs | 1,233.06 μs |  1.53 |    0.78 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **559.6 ns** |   **948.8 ns** |  **52.01 ns** |  **1.01** |    **0.12** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,596.6 ns | 2,338.2 ns | 128.17 ns |  2.87 |    0.31 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,161.6 ns** | **2,761.0 ns** | **151.34 ns** |  **1.01** |    **0.16** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,934.6 ns | 2,109.5 ns | 115.63 ns |  2.55 |    0.28 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.016 μs | 216.246 μs | 11.8532 μs | 26.480 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.691 μs |   5.437 μs |  0.2980 μs | 10.595 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.223 μs |   1.705 μs |  0.0935 μs |  8.196 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.132 μs |   3.395 μs |  0.1861 μs |  8.045 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.680 μs |   1.602 μs |  0.0878 μs | 14.703 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.031 μs |   8.944 μs |  0.4903 μs | 12.784 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.761 μs |  47.602 μs |  2.6092 μs | 13.284 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.429 μs |   6.642 μs |  0.3641 μs | 27.316 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 22.281 μs | 208.544 μs | 11.4310 μs | 16.010 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.728 μs |  68.919 μs |  3.7777 μs | 20.567 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.654 μs |  36.809 μs |  2.0176 μs | 21.010 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.319 μs |   5.712 μs |  0.3131 μs | 24.395 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 | 13.578 μs | 236.483 μs | 12.9624 μs |  6.371 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 18.995 μs |   7.477 μs |  0.4099 μs | 18.805 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 16,478.22 ns | 182.451 ns | 10.001 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.83 ns |   0.920 ns |  0.050 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.25 ns |   0.334 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.55 ns |   0.236 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.04 ns |  11.487 ns |  0.630 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.412 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    123.48 ns |  30.489 ns |  1.671 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.05 ns |   1.318 ns |  0.072 ns |  0.64 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.297 μs |   6.846 μs |  0.3753 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 524.476 μs | 283.606 μs | 15.5454 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.913 μs |  24.638 μs |  1.3505 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 243.708 μs | 242.156 μs | 13.2734 μs |      80 B |


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