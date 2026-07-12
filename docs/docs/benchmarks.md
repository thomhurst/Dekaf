---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 01:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,300.0 μs** |    **780.15 μs** |    **42.76 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,808.5 μs |  4,781.02 μs |   262.06 μs |  0.29 |    0.04 |        - |       - |    34.7 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,466.6 μs** |    **777.79 μs** |    **42.63 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,592.4 μs |    619.94 μs |    33.98 μs |  0.35 |    0.00 |  15.6250 |       - |   341.3 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,175.5 μs** |    **219.31 μs** |    **12.02 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,853.8 μs |  2,078.23 μs |   113.91 μs |  0.30 |    0.02 |        - |       - |   38.15 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,110.6 μs** |  **4,083.67 μs** |   **223.84 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,897.2 μs | 26,053.40 μs | 1,428.07 μs |  0.68 |    0.09 |  15.6250 |       - |  396.33 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.2 μs** |     **15.55 μs** |     **0.85 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    106.0 μs |    172.41 μs |     9.45 μs |  0.75 |    0.06 |        - |       - |    4.63 KB |        0.14 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,379.1 μs** |    **628.34 μs** |    **34.44 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,234.4 μs |  5,667.28 μs |   310.64 μs |  0.90 |    0.20 |        - |       - |   67.94 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **716.4 μs** |  **8,130.54 μs** |   **445.66 μs** |  **1.50** |    **1.47** |   **7.3242** |       **-** |  **122.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    398.2 μs |    366.21 μs |    20.07 μs |  0.83 |    0.61 |        - |       - |   86.62 KB |        0.71 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,360.9 μs** | **36,041.28 μs** | **1,975.54 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1227.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,963.8 μs | 15,752.14 μs |   863.43 μs |  0.39 |    0.10 |        - |       - |  941.87 KB |        0.77 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,456.3 μs** |     **51.83 μs** |     **2.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,348.3 μs |    319.55 μs |    17.52 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,462.3 μs** |    **101.56 μs** |     **5.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,324.1 μs |    725.85 μs |    39.79 μs |  0.24 |    0.01 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,476.8 μs** |    **244.94 μs** |    **13.43 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,276.4 μs |    167.56 μs |     9.18 μs |  0.23 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,457.8 μs** |    **132.24 μs** |     **7.25 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,189.7 μs |    251.90 μs |    13.81 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **147.7 μs** |    **518.0 μs** |  **28.39 μs** |   **151.5 μs** |  **1.03** |    **0.25** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   201.8 μs |    232.7 μs |  12.75 μs |   195.0 μs |  1.40 |    0.26 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.3 μs** |    **713.3 μs** |  **39.10 μs** |   **129.2 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   222.2 μs |    467.3 μs |  25.61 μs |   210.6 μs |  1.54 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **975.1 μs** |  **3,659.9 μs** | **200.61 μs** |   **862.2 μs** |  **1.03** |    **0.25** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,145.7 μs |    363.6 μs |  19.93 μs | 1,140.4 μs |  1.21 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,486.7 μs** | **12,359.8 μs** | **677.48 μs** | **1,116.1 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,444.7 μs |    315.6 μs |  17.30 μs | 1,435.5 μs |  1.09 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **936.5 ns** |   **889.2 ns** |  **48.74 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,008.4 ns |   335.9 ns |  18.41 ns |  2.15 |    0.10 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,476.4 ns** | **2,115.5 ns** | **115.96 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,259.9 ns | 3,617.1 ns | 198.26 ns |  2.22 |    0.19 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.195 μs |   3.1014 μs |  0.1700 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.252 μs |   1.0240 μs |  0.0561 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 10.136 μs |   2.9085 μs |  0.1594 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.431 μs |   4.2001 μs |  0.2302 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.717 μs |  35.8247 μs |  1.9637 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.577 μs |   7.8623 μs |  0.4310 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.128 μs |   4.8493 μs |  0.2658 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 43.395 μs | 212.4485 μs | 11.6450 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.968 μs |   1.1466 μs |  0.0629 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.198 μs |   0.9585 μs |  0.0525 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 35.503 μs |  97.7522 μs |  5.3581 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 37.388 μs |  97.4440 μs |  5.3412 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.956 μs |   5.1264 μs |  0.2810 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.011 μs |   5.1148 μs |  0.2804 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,620.36 ns | 395.859 ns | 21.698 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.57 ns |   0.698 ns |  0.038 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.63 ns |   0.132 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.48 ns |   1.181 ns |  0.065 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.48 ns |   9.358 ns |  0.513 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.87 ns |   0.897 ns |  0.049 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.59 ns |  23.823 ns |  1.306 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.57 ns |   1.479 ns |  0.081 ns |  0.73 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error     | StdDev    | Allocated |
|------------------------ |-----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.392 μs | 11.062 μs | 0.6064 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 521.638 μs | 53.970 μs | 2.9583 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.474 μs |  2.121 μs | 0.1162 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.534 μs |  8.374 μs | 0.4590 μs |      80 B |


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