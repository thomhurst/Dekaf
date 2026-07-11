---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 16:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,571.90 μs** |  **1,748.01 μs** |    **95.814 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,275.18 μs |  2,061.94 μs |   113.022 μs |  0.35 |    0.02 |        - |       - |   34.72 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,398.40 μs** |  **1,262.13 μs** |    **69.182 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,486.98 μs |  3,017.62 μs |   165.406 μs |  0.34 |    0.02 |  15.6250 |       - |  341.44 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,513.67 μs** |    **618.64 μs** |    **33.910 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,735.80 μs |    432.82 μs |    23.724 μs |  0.27 |    0.00 |        - |       - |   38.18 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,528.68 μs** |  **3,235.78 μs** |   **177.364 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,644.09 μs |  7,672.89 μs |   420.577 μs |  0.66 |    0.03 |  15.6250 |       - |  395.34 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.96 μs** |    **110.79 μs** |     **6.073 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     84.27 μs |    171.84 μs |     9.419 μs |  0.66 |    0.07 |        - |       - |    6.05 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,228.82 μs** |    **636.87 μs** |    **34.909 μs** |  **1.00** |    **0.04** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    763.67 μs |  1,924.82 μs |   105.506 μs |  0.62 |    0.08 |        - |       - |   60.05 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **986.80 μs** |     **75.09 μs** |     **4.116 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.43 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    343.11 μs |  1,532.98 μs |    84.028 μs |  0.35 |    0.07 |        - |       - |  101.97 KB |        0.83 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,796.53 μs** | **34,261.39 μs** | **1,877.982 μs** |  **1.04** |    **0.29** |  **74.2188** |       **-** | **1224.91 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,377.90 μs |    386.34 μs |    21.177 μs |  0.40 |    0.08 |        - |       - |  832.25 KB |        0.68 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,405.49 μs** |     **99.12 μs** |     **5.433 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,305.12 μs |    452.70 μs |    24.814 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,404.41 μs** |    **262.60 μs** |    **14.394 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,316.05 μs |    313.64 μs |    17.192 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.51 μs** |    **233.44 μs** |    **12.796 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,312.81 μs |    244.17 μs |    13.384 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,473.97 μs** |    **777.89 μs** |    **42.639 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,325.14 μs |    503.97 μs |    27.624 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **136.1 μs** |    **880.6 μs** |  **48.27 μs** |   **132.9 μs** |  **1.09** |    **0.49** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   189.5 μs |    815.3 μs |  44.69 μs |   211.4 μs |  1.52 |    0.58 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **166.5 μs** |    **819.0 μs** |  **44.89 μs** |   **184.2 μs** |  **1.06** |    **0.39** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   237.7 μs |  1,176.1 μs |  64.46 μs |   205.4 μs |  1.51 |    0.55 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **877.5 μs** |  **4,192.9 μs** | **229.83 μs** |   **807.9 μs** |  **1.04** |    **0.32** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,136.5 μs |  2,712.1 μs | 148.66 μs | 1,071.2 μs |  1.35 |    0.32 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,542.4 μs** | **16,151.4 μs** | **885.31 μs** | **1,041.3 μs** |  **1.20** |    **0.77** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,378.5 μs |    359.0 μs |  19.68 μs | 1,385.3 μs |  1.07 |    0.40 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **876.3 ns** |   **934.7 ns** |  **51.23 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,891.1 ns | 3,067.9 ns | 168.16 ns |  2.16 |    0.20 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,408.2 ns** | **1,449.6 ns** |  **79.46 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,286.5 ns | 5,934.0 ns | 325.26 ns |  2.34 |    0.23 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 35.450 μs | 289.9528 μs | 15.8933 μs | 26.440 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.384 μs |   8.9873 μs |  0.4926 μs | 11.140 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.131 μs |   8.5592 μs |  0.4692 μs |  8.917 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.440 μs |   1.8570 μs |  0.1018 μs |  8.396 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.375 μs |   5.4127 μs |  0.2967 μs | 11.352 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 23.253 μs | 319.4382 μs | 17.5095 μs | 13.224 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.837 μs |   2.7325 μs |  0.1498 μs | 12.794 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.168 μs |   2.8285 μs |  0.1550 μs | 27.172 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.943 μs |   0.4605 μs |  0.0252 μs |  8.947 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.383 μs |  37.7824 μs |  2.0710 μs | 20.197 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.108 μs |  23.0213 μs |  1.2619 μs | 31.514 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 40.286 μs | 176.6653 μs |  9.6836 μs | 35.391 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.815 μs |   4.1721 μs |  0.2287 μs |  4.688 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.678 μs |  55.2639 μs |  3.0292 μs | 10.995 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,548.86 ns | 959.474 ns | 52.592 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.160 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.26 ns |   2.041 ns |  0.112 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.66 ns |   1.582 ns |  0.087 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.88 ns |   6.687 ns |  0.367 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.284 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.95 ns |  25.026 ns |  1.372 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.89 ns |   1.132 ns |  0.062 ns |  0.71 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.607 μs |  34.977 μs |  1.9172 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 530.721 μs | 181.360 μs |  9.9409 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.167 μs |  14.990 μs |  0.8217 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 256.533 μs | 319.749 μs | 17.5265 μs |      80 B |


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