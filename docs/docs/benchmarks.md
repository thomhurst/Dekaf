---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 03:47 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,257.3 μs** |    **262.85 μs** |    **14.41 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,792.1 μs |    664.41 μs |    36.42 μs |  0.45 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,436.0 μs** |  **1,215.90 μs** |    **66.65 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,930.4 μs |  1,783.53 μs |    97.76 μs |  0.53 |    0.01 |  15.6250 |       - |  347274 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,188.6 μs** |    **602.09 μs** |    **33.00 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,578.6 μs |  7,307.93 μs |   400.57 μs |  0.58 |    0.06 |        - |       - |   37484 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,613.5 μs** |  **3,265.70 μs** |   **179.00 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,026.2 μs |  5,639.85 μs |   309.14 μs |  1.03 |    0.02 |        - |       - |  469874 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.5 μs** |    **114.90 μs** |     **6.30 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    148.6 μs |     38.17 μs |     2.09 μs |  1.13 |    0.05 |        - |       - |    4106 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,360.9 μs** |    **140.78 μs** |     **7.72 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,446.1 μs |  1,082.32 μs |    59.33 μs |  1.06 |    0.04 |        - |       - |   42755 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,069.7 μs** |     **83.80 μs** |     **4.59 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125476 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    881.8 μs |    278.41 μs |    15.26 μs |  0.82 |    0.01 |        - |       - |    5965 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,747.7 μs** | **32,485.52 μs** | **1,780.64 μs** |  **1.03** |    **0.24** |  **74.2188** |       **-** | **1255483 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,668.0 μs | 11,935.06 μs |   654.20 μs |  1.02 |    0.19 |        - |       - |   61398 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,503.3 μs** |     **27.35 μs** |     **1.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,600.8 μs |    142.40 μs |     7.81 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,502.2 μs** |    **262.11 μs** |    **14.37 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,597.5 μs |    111.99 μs |     6.14 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,503.1 μs** |     **89.24 μs** |     **4.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,620.1 μs |    357.34 μs |    19.59 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,498.9 μs** |     **53.07 μs** |     **2.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,612.4 μs |    115.58 μs |     6.34 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.7 μs** |    **378.3 μs** |    **20.73 μs** |   **120.0 μs** |  **1.02** |    **0.21** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   164.2 μs |    321.3 μs |    17.61 μs |   163.3 μs |  1.38 |    0.24 |   39.98 KB |        0.62 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **183.9 μs** |  **1,853.9 μs** |   **101.62 μs** |   **132.2 μs** |  **1.18** |    **0.74** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   204.2 μs |    721.5 μs |    39.55 μs |   183.4 μs |  1.32 |    0.54 |  215.77 KB |        0.90 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,289.5 μs** |  **6,743.3 μs** |   **369.62 μs** | **1,395.5 μs** |  **1.07** |    **0.41** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,365.3 μs |  2,537.1 μs |   139.07 μs | 1,297.1 μs |  1.13 |    0.34 |  476.66 KB |        0.73 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,795.5 μs** | **21,778.5 μs** | **1,193.75 μs** | **1,146.0 μs** |  **1.27** |    **0.95** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,853.5 μs | 12,586.3 μs |   689.90 μs | 1,455.7 μs |  1.31 |    0.71 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **851.4 ns** |   **612.75 ns** |  **33.59 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,162.3 ns | 2,660.39 ns | 145.83 ns |  2.54 |    0.17 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,459.6 ns** |    **78.84 ns** |   **4.32 ns** |  **1.00** |    **0.00** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,708.4 ns | 3,733.89 ns | 204.67 ns |  2.54 |    0.12 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.041 μs |   4.7203 μs |  0.2587 μs | 25.998 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.642 μs |   2.7947 μs |  0.1532 μs | 10.675 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.286 μs |   1.4515 μs |  0.0796 μs |  8.317 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.146 μs |   2.0068 μs |  0.1100 μs |  8.146 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.854 μs |   1.7053 μs |  0.0935 μs | 10.881 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 23.744 μs | 289.5504 μs | 15.8712 μs | 14.828 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.522 μs |   7.6745 μs |  0.4207 μs | 13.536 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.748 μs | 172.7817 μs |  9.4707 μs | 27.471 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.883 μs |   1.7999 μs |  0.0987 μs |  8.836 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.251 μs |   0.3696 μs |  0.0203 μs | 20.257 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.788 μs |  56.8341 μs |  3.1153 μs | 20.142 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.942 μs |  11.7885 μs |  0.6462 μs | 21.605 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.176 μs |  13.8447 μs |  0.7589 μs |  4.789 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.501 μs |   3.2213 μs |  0.1766 μs | 14.447 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 16,031.52 ns | 346.777 ns | 19.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.82 ns |   0.099 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.24 ns |   0.079 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.90 ns |   5.251 ns |  0.288 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.04 ns |   8.537 ns |  0.468 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.252 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.53 ns |  14.229 ns |  0.780 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.60 ns |   1.913 ns |  0.105 ns |  0.72 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.518 μs |   3.279 μs |  0.1797 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 509.366 μs | 279.730 μs | 15.3330 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.831 μs |  21.586 μs |  1.1832 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 237.830 μs |  65.060 μs |  3.5662 μs |      80 B |


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