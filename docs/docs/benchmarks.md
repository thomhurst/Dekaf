---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 21:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,182.9 μs** |    **227.02 μs** |    **12.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,628.3 μs |  4,560.46 μs |   249.97 μs |  0.26 |    0.04 |        - |       - |   34.72 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,337.6 μs** |  **1,866.40 μs** |   **102.30 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,504.0 μs |  1,498.81 μs |    82.16 μs |  0.34 |    0.01 |  15.6250 |       - |  341.29 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,221.7 μs** |    **393.69 μs** |    **21.58 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,878.1 μs |  2,916.08 μs |   159.84 μs |  0.30 |    0.02 |        - |       - |    38.2 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,595.0 μs** |  **2,674.70 μs** |   **146.61 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,521.3 μs |  9,254.62 μs |   507.28 μs |  0.68 |    0.04 |  15.6250 |       - |   389.9 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.6 μs** |    **222.55 μs** |    **12.20 μs** |  **1.00** |    **0.10** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.6 μs |    407.08 μs |    22.31 μs |  0.77 |    0.15 |        - |       - |    7.31 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,367.7 μs** |    **247.18 μs** |    **13.55 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,019.2 μs |  1,209.10 μs |    66.27 μs |  0.75 |    0.04 |        - |       - |  145.79 KB |        0.43 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,090.6 μs** |     **58.33 μs** |     **3.20 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    492.9 μs |  1,078.76 μs |    59.13 μs |  0.45 |    0.05 |        - |       - |   82.19 KB |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,938.7 μs** | **30,413.06 μs** | **1,667.04 μs** |  **1.02** |    **0.22** |  **74.2188** |       **-** | **1226.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  4,057.7 μs |  3,647.57 μs |   199.94 μs |  0.42 |    0.07 |        - |       - |  891.88 KB |        0.73 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.0 μs** |     **50.26 μs** |     **2.75 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,120.8 μs |    145.29 μs |     7.96 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,427.7 μs** |     **89.11 μs** |     **4.88 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,169.8 μs |    475.26 μs |    26.05 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.92 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,424.9 μs** |     **19.65 μs** |     **1.08 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,312.2 μs |    504.56 μs |    27.66 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,428.7 μs** |    **151.33 μs** |     **8.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,354.4 μs |    173.09 μs |     9.49 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **132.1 μs** |    **777.21 μs** |  **42.60 μs** |   **112.8 μs** |  **1.06** |    **0.40** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   171.0 μs |    496.37 μs |  27.21 μs |   156.5 μs |  1.38 |    0.38 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **185.8 μs** |  **1,073.01 μs** |  **58.82 μs** |   **207.4 μs** |  **1.09** |    **0.47** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   215.7 μs |    368.77 μs |  20.21 μs |   212.7 μs |  1.26 |    0.43 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **954.6 μs** |  **3,304.07 μs** | **181.11 μs** |   **866.4 μs** |  **1.02** |    **0.23** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,244.8 μs |  2,418.56 μs | 132.57 μs | 1,172.6 μs |  1.33 |    0.23 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,476.3 μs** | **12,893.88 μs** | **706.76 μs** | **1,084.9 μs** |  **1.14** |    **0.62** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,362.2 μs |     31.67 μs |   1.74 μs | 1,362.4 μs |  1.05 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **914.9 ns** | **1,189.9 ns** |  **65.22 ns** |  **1.00** |    **0.09** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,999.7 ns |   733.8 ns |  40.22 ns |  2.19 |    0.14 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,435.1 ns** | **2,389.2 ns** | **130.96 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,170.6 ns | 2,136.1 ns | 117.09 ns |  2.22 |    0.18 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 30.477 μs |   0.3740 μs | 0.0205 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.984 μs |   0.9362 μs | 0.0513 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.733 μs |   1.5630 μs | 0.0857 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.404 μs |   1.8365 μs | 0.1007 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.099 μs |   2.0043 μs | 0.1099 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.590 μs |   3.1100 μs | 0.1705 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.287 μs |   7.7532 μs | 0.4250 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.191 μs |   5.8397 μs | 0.3201 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.877 μs |   1.5587 μs | 0.0854 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.318 μs |   3.1196 μs | 0.1710 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.835 μs |  29.0650 μs | 1.5932 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 37.341 μs | 141.0211 μs | 7.7298 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.551 μs |   1.7340 μs | 0.0950 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.145 μs |   1.3798 μs | 0.0756 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,740.13 ns | 720.632 ns | 39.500 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.102 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.20 ns |   0.174 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.01 ns |   2.198 ns |  0.120 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.42 ns |  17.235 ns |  0.945 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.76 ns |   0.341 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.73 ns |  17.803 ns |  0.976 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.62 ns |   1.225 ns |  0.067 ns |  0.70 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.431 μs |   6.827 μs |  0.3742 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 521.437 μs | 226.634 μs | 12.4226 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.824 μs |   2.156 μs |  0.1182 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 231.560 μs |  74.559 μs |  4.0868 μs |      80 B |


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