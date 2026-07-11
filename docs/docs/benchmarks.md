---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 14:29 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,235.8 μs** |    **192.84 μs** |    **10.57 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,760.3 μs |  4,791.83 μs |   262.66 μs |  0.28 |    0.04 |       - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,378.4 μs** |    **652.47 μs** |    **35.76 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,410.7 μs |    589.19 μs |    32.30 μs |  0.33 |    0.00 | 15.6250 |       - |  341.24 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,103.3 μs** |    **219.18 μs** |    **12.01 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,828.5 μs |  1,911.38 μs |   104.77 μs |  0.30 |    0.01 |       - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,088.6 μs** |    **478.95 μs** |    **26.25 μs** |  **1.00** |    **0.00** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,912.8 μs |    749.49 μs |    41.08 μs |  0.60 |    0.00 | 15.6250 |       - |  390.32 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.4 μs** |     **46.98 μs** |     **2.58 μs** |  **1.00** |    **0.02** |  **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    106.5 μs |    192.49 μs |    10.55 μs |  0.79 |    0.07 |       - |       - |    7.49 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,383.4 μs** |    **469.75 μs** |    **25.75 μs** |  **1.00** |    **0.02** | **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    976.7 μs |  2,971.31 μs |   162.87 μs |  0.71 |    0.10 |       - |       - |   63.93 KB |        0.19 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **764.5 μs** |  **8,629.70 μs** |   **473.02 μs** |  **1.59** |    **1.65** |  **7.3242** |       **-** |  **122.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    424.7 μs |  1,407.23 μs |    77.13 μs |  0.88 |    0.72 |       - |       - |    90.2 KB |        0.74 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,262.0 μs** | **30,068.11 μs** | **1,648.13 μs** |  **1.02** |    **0.21** | **74.2188** |       **-** | **1227.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,838.2 μs |  8,746.83 μs |   479.44 μs |  0.38 |    0.07 |       - |       - |  914.75 KB |        0.74 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,432.3 μs** |     **17.54 μs** |     **0.96 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,119.5 μs |      5.93 μs |     0.32 μs |  0.21 |    0.00 |       - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,444.9 μs** |    **245.56 μs** |    **13.46 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.3 μs |     23.79 μs |     1.30 μs |  0.21 |    0.00 |       - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,438.4 μs** |     **93.61 μs** |     **5.13 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,118.0 μs |     37.63 μs |     2.06 μs |  0.21 |    0.00 |       - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.3 μs** |    **127.99 μs** |     **7.02 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.3 μs |     13.40 μs |     0.73 μs |  0.21 |    0.00 |       - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **152.7 μs** |    **518.54 μs** |  **28.42 μs** |   **156.0 μs** |  **1.02** |    **0.24** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   179.0 μs |    323.17 μs |  17.71 μs |   174.5 μs |  1.20 |    0.23 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **142.8 μs** |    **798.07 μs** |  **43.74 μs** |   **118.6 μs** |  **1.06** |    **0.37** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   201.7 μs |    341.09 μs |  18.70 μs |   209.9 μs |  1.49 |    0.36 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,121.2 μs** |  **5,473.11 μs** | **300.00 μs** | **1,101.5 μs** |  **1.05** |    **0.35** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,245.0 μs |  2,543.71 μs | 139.43 μs | 1,176.3 μs |  1.17 |    0.30 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,474.6 μs** | **12,455.67 μs** | **682.74 μs** | **1,094.6 μs** |  **1.13** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,418.5 μs |     87.19 μs |   4.78 μs | 1,420.1 μs |  1.08 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **950.4 ns** |  **2,173.2 ns** | **119.12 ns** |  **1.01** |    **0.16** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 3,886.5 ns | 15,855.9 ns | 869.11 ns |  4.14 |    0.94 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,464.2 ns** |  **1,798.3 ns** |  **98.57 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 5,046.7 ns |  9,203.0 ns | 504.45 ns |  3.46 |    0.36 | 0.1000 |    2464 B |        1.01 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.169 μs |  0.9122 μs | 0.0500 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.102 μs |  2.2069 μs | 0.1210 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.865 μs |  1.9336 μs | 0.1060 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.047 μs |  5.9811 μs | 0.3278 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.536 μs |  2.0616 μs | 0.1130 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.988 μs |  5.1871 μs | 0.2843 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 15.028 μs | 59.8270 μs | 3.2793 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.739 μs |  6.4908 μs | 0.3558 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.384 μs | 62.4778 μs | 3.4246 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.388 μs |  1.4956 μs | 0.0820 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.636 μs | 55.3322 μs | 3.0329 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.325 μs | 29.7896 μs | 1.6329 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.920 μs |  6.1167 μs | 0.3353 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.695 μs | 16.7478 μs | 0.9180 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,191.02 ns | 295.289 ns | 16.186 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.58 ns |   1.899 ns |  0.104 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.30 ns |   2.927 ns |  0.160 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.61 ns |   1.086 ns |  0.060 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.22 ns |  33.203 ns |  1.820 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.83 ns |   2.167 ns |  0.119 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    119.57 ns |  59.885 ns |  3.282 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.18 ns |   0.333 ns |  0.018 ns |  0.63 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.280 μs |   3.749 μs |  0.2055 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 519.445 μs | 392.377 μs | 21.5075 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.132 μs |   1.472 μs |  0.0807 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 253.598 μs | 430.521 μs | 23.5983 μs |      80 B |


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