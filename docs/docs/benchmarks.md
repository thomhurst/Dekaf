---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 17:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,097.6 μs** |    **390.53 μs** |    **21.41 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,697.4 μs |    660.13 μs |    36.18 μs |  0.44 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,171.9 μs** |    **847.74 μs** |    **46.47 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,843.2 μs |    632.26 μs |    34.66 μs |  0.54 |    0.01 |  15.6250 |       - |  347185 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,640.1 μs** |    **128.80 μs** |     **7.06 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,095.4 μs |  4,561.61 μs |   250.04 μs |  0.47 |    0.03 |        - |       - |   37819 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,476.6 μs** |  **3,548.05 μs** |   **194.48 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 14,743.3 μs | 22,747.15 μs | 1,246.85 μs |  1.28 |    0.10 |        - |       - |  470072 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **109.6 μs** |    **164.44 μs** |     **9.01 μs** |  **1.00** |    **0.10** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    131.9 μs |    208.54 μs |    11.43 μs |  1.21 |    0.13 |        - |       - |    4107 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,134.7 μs** |    **380.61 μs** |    **20.86 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,264.6 μs |    757.89 μs |    41.54 μs |  1.11 |    0.04 |        - |       - |   41770 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **935.6 μs** |    **139.55 μs** |     **7.65 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125292 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    985.3 μs |  1,384.33 μs |    75.88 μs |  1.05 |    0.07 |        - |       - |    6088 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,115.5 μs** | **34,538.60 μs** | **1,893.18 μs** |  **1.04** |    **0.33** |  **74.2188** |       **-** | **1253492 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,256.8 μs | 13,826.76 μs |   757.89 μs |  1.32 |    0.32 |        - |       - |   61199 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,460.4 μs** |    **349.76 μs** |    **19.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,511.3 μs |    284.98 μs |    15.62 μs |  0.46 |    0.00 |        - |       - |     769 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,459.5 μs** |    **273.14 μs** |    **14.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,486.2 μs |    101.82 μs |     5.58 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,478.5 μs** |    **255.71 μs** |    **14.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,486.5 μs |     36.37 μs |     1.99 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,478.2 μs** |     **65.57 μs** |     **3.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,504.7 μs |    167.00 μs |     9.15 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **123.9 μs** |    **789.5 μs** |  **43.27 μs** |   **112.8 μs** |  **1.08** |    **0.45** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   147.0 μs |    161.9 μs |   8.87 μs |   148.1 μs |  1.28 |    0.37 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **167.4 μs** |    **826.8 μs** |  **45.32 μs** |   **165.1 μs** |  **1.05** |    **0.36** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   210.4 μs |    641.3 μs |  35.15 μs |   197.3 μs |  1.32 |    0.37 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **996.2 μs** |  **4,184.7 μs** | **229.38 μs** | **1,083.4 μs** |  **1.04** |    **0.32** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,162.0 μs |  2,997.4 μs | 164.30 μs | 1,071.4 μs |  1.22 |    0.31 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,510.4 μs** | **15,646.4 μs** | **857.63 μs** | **1,028.5 μs** |  **1.19** |    **0.76** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,656.6 μs |  6,561.4 μs | 359.65 μs | 1,459.9 μs |  1.31 |    0.55 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **805.3 ns** |  **2,596.0 ns** | **142.30 ns** |  **1.02** |    **0.23** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,917.5 ns |  2,909.2 ns | 159.46 ns |  2.44 |    0.44 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,320.2 ns** |  **1,732.1 ns** |  **94.94 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,594.5 ns | 11,422.6 ns | 626.11 ns |  2.73 |    0.45 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 34.714 μs | 260.3925 μs | 14.2730 μs | 26.569 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.573 μs |   2.4727 μs |  0.1355 μs | 10.569 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 11.159 μs |  42.1594 μs |  2.3109 μs | 12.477 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.730 μs |   8.0344 μs |  0.4404 μs |  8.496 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.870 μs |   2.6311 μs |  0.1442 μs | 10.910 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.992 μs |   3.4993 μs |  0.1918 μs | 13.049 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.693 μs |   4.2719 μs |  0.2342 μs | 12.562 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.468 μs |   4.4401 μs |  0.2434 μs | 32.421 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.130 μs |   7.3990 μs |  0.4056 μs |  9.026 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.482 μs |  71.8653 μs |  3.9392 μs | 20.248 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.035 μs |  17.2947 μs |  0.9480 μs | 20.453 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.382 μs |   4.2995 μs |  0.2357 μs | 22.372 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.953 μs |   0.7373 μs |  0.0404 μs |  4.960 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.510 μs |   5.1520 μs |  0.2824 μs | 14.416 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,716.45 ns | 247.161 ns | 13.548 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.51 ns |   0.028 ns |  0.002 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |   0.746 ns |  0.041 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.11 ns |   2.808 ns |  0.154 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.42 ns |  10.565 ns |  0.579 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.56 ns |   1.429 ns |  0.078 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.66 ns |  14.197 ns |  0.778 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.65 ns |   0.210 ns |  0.011 ns |  0.70 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.083 μs |  18.488 μs |  1.0134 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 571.865 μs | 437.904 μs | 24.0030 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.378 μs |   9.994 μs |  0.5478 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 245.525 μs | 386.767 μs | 21.2000 μs |      80 B |


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