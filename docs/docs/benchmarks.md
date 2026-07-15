---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 10:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,428.3 μs** |    **735.92 μs** |    **40.34 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,804.3 μs |    778.12 μs |    42.65 μs |  0.44 |    0.01 |        - |       - |   35162 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,511.8 μs** |  **1,101.50 μs** |    **60.38 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,880.4 μs |  1,990.98 μs |   109.13 μs |  0.52 |    0.01 |  15.6250 |       - |  347281 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,205.6 μs** |    **758.75 μs** |    **41.59 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,221.0 μs |  4,519.71 μs |   247.74 μs |  0.52 |    0.03 |        - |       - |   37313 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,652.8 μs** |    **839.53 μs** |    **46.02 μs** |  **1.00** |    **0.00** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,191.1 μs | 11,529.73 μs |   631.98 μs |  0.88 |    0.04 |        - |       - |  465368 B |        0.23 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **123.6 μs** |     **87.40 μs** |     **4.79 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    116.7 μs |    194.59 μs |    10.67 μs |  0.94 |    0.08 |        - |       - |    4219 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,342.6 μs** |    **359.67 μs** |    **19.71 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,233.8 μs |  3,647.74 μs |   199.94 μs |  0.92 |    0.13 |        - |       - |   41972 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,058.1 μs** |     **50.04 μs** |     **2.74 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125466 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    870.3 μs |    589.21 μs |    32.30 μs |  0.82 |    0.03 |        - |       - |    8544 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,491.6 μs** | **30,796.70 μs** | **1,688.07 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1255025 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,642.2 μs |  8,175.32 μs |   448.12 μs |  0.72 |    0.13 |        - |       - |   78922 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,478.4 μs** |     **60.38 μs** |     **3.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,595.8 μs |     53.46 μs |     2.93 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,482.1 μs** |     **88.81 μs** |     **4.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,603.2 μs |    143.68 μs |     7.88 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,489.4 μs** |     **65.29 μs** |     **3.58 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,603.1 μs |    128.70 μs |     7.05 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,482.4 μs** |     **46.28 μs** |     **2.54 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,601.0 μs |    126.49 μs |     6.93 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **117.2 μs** |    **148.3 μs** |   **8.13 μs** |   **117.2 μs** |  **1.00** |    **0.09** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   191.0 μs |    419.3 μs |  22.98 μs |   187.9 μs |  1.64 |    0.20 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **195.2 μs** |    **929.4 μs** |  **50.95 μs** |   **223.3 μs** |  **1.06** |    **0.37** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   193.2 μs |    394.4 μs |  21.62 μs |   188.3 μs |  1.05 |    0.30 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,154.7 μs** |  **4,545.0 μs** | **249.12 μs** | **1,133.6 μs** |  **1.03** |    **0.27** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,282.5 μs |    360.0 μs |  19.73 μs | 1,273.9 μs |  1.15 |    0.21 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,467.9 μs** | **11,864.9 μs** | **650.35 μs** | **1,115.3 μs** |  **1.12** |    **0.56** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,867.0 μs | 12,110.2 μs | 663.80 μs | 1,500.8 μs |  1.42 |    0.63 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **913.8 ns** |   **807.0 ns** |  **44.23 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,220.4 ns | 3,116.0 ns | 170.80 ns |  2.43 |    0.19 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,457.9 ns** | **1,364.0 ns** |  **74.76 ns** |  **1.00** |    **0.06** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,650.8 ns | 1,577.4 ns |  86.46 ns |  2.51 |    0.12 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.505 μs |  11.978 μs | 0.6566 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.205 μs |  22.060 μs | 1.2092 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.295 μs |  21.445 μs | 1.1755 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.944 μs |  15.396 μs | 0.8439 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.687 μs |  19.095 μs | 1.0467 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.941 μs |  10.364 μs | 0.5681 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 10.585 μs |  21.592 μs | 1.1835 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.710 μs | 143.942 μs | 7.8899 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.735 μs |  11.299 μs | 0.6194 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 14.263 μs |  43.230 μs | 2.3696 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.888 μs |  49.386 μs | 2.7070 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.830 μs | 134.584 μs | 7.3770 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.994 μs |  13.732 μs | 0.7527 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 23.796 μs | 133.568 μs | 7.3213 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,720.845 ns | 120.4958 ns | 6.6048 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     10.523 ns |   0.6357 ns | 0.0348 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.532 ns |   4.0972 ns | 0.2246 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     25.328 ns |   3.5574 ns | 0.1950 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.705 ns |   1.3334 ns | 0.0731 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      7.134 ns |   0.2840 ns | 0.0156 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |     96.051 ns |  21.7989 ns | 1.1949 ns |  1.00 |    0.02 | 0.0106 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     71.080 ns |   3.6894 ns | 0.2022 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.779 μs |  35.605 μs |  1.952 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 372.706 μs | 240.447 μs | 13.180 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.350 μs |  28.821 μs |  1.580 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 169.680 μs |  92.030 μs |  5.044 μs |      80 B |


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