---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 07:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,256.1 μs** |    **601.37 μs** |    **32.96 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,872.3 μs |  1,546.90 μs |    84.79 μs |  0.30 |    0.01 |        - |       - |   35224 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,423.5 μs** |  **1,284.85 μs** |    **70.43 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,346.7 μs |  2,229.00 μs |   122.18 μs |  0.45 |    0.01 |  15.6250 |       - |  346116 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,837.8 μs** | **12,009.26 μs** |   **658.27 μs** |  **1.01** |    **0.12** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,752.4 μs |    608.44 μs |    33.35 μs |  0.40 |    0.03 |        - |       - |   36124 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,146.4 μs** |  **2,630.87 μs** |   **144.21 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,754.5 μs | 34,407.79 μs | 1,886.01 μs |  0.97 |    0.13 |        - |       - |  368612 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.7 μs** |    **191.63 μs** |    **10.50 μs** |  **1.00** |    **0.10** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.6 μs |     80.62 μs |     4.42 μs |  0.91 |    0.07 |        - |       - |    4179 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,267.6 μs** |    **232.81 μs** |    **12.76 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,395.0 μs |  2,399.00 μs |   131.50 μs |  1.10 |    0.09 |        - |       - |   41994 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,012.5 μs** |     **12.80 μs** |     **0.70 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125401 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,005.4 μs |  5,745.15 μs |   314.91 μs |  0.99 |    0.27 |        - |       - |    8740 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,216.3 μs** | **29,284.93 μs** | **1,605.21 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1254728 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,253.8 μs | 13,343.79 μs |   731.42 μs |  0.92 |    0.17 |        - |       - |   82540 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,524.6 μs** |    **269.47 μs** |    **14.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,461.9 μs |     71.30 μs |     3.91 μs |  0.45 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,495.7 μs** |    **321.60 μs** |    **17.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,475.6 μs |    256.37 μs |    14.05 μs |  0.45 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,480.7 μs** |    **142.39 μs** |     **7.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,467.9 μs |    163.73 μs |     8.97 μs |  0.45 |    0.00 |        - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,459.1 μs** |    **164.78 μs** |     **9.03 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,459.3 μs |     27.48 μs |     1.51 μs |  0.45 |    0.00 |        - |       - |     832 B |        0.40 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.8 μs** |    **350.7 μs** |  **19.22 μs** |  **1.02** |    **0.20** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   167.3 μs |    517.7 μs |  28.37 μs |  1.40 |    0.28 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **163.1 μs** |    **328.9 μs** |  **18.03 μs** |  **1.01** |    **0.13** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   229.0 μs |    721.0 μs |  39.52 μs |  1.41 |    0.25 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **930.8 μs** |  **2,886.7 μs** | **158.23 μs** |  **1.02** |    **0.20** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,224.8 μs |  1,737.4 μs |  95.23 μs |  1.34 |    0.20 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,611.1 μs** | **10,868.6 μs** | **595.75 μs** |  **1.10** |    **0.52** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,187.0 μs | 10,675.3 μs | 585.15 μs |  1.49 |    0.61 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **859.1 ns** |   **160.3 ns** |   **8.79 ns** |  **1.00** |    **0.01** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,112.7 ns | 2,029.3 ns | 111.23 ns |  2.46 |    0.11 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,446.4 ns** | **1,043.1 ns** |  **57.18 ns** |  **1.00** |    **0.05** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,230.9 ns | 1,584.5 ns |  86.85 ns |  2.24 |    0.09 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.157 μs |  8.535 μs | 0.4679 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.300 μs |  5.630 μs | 0.3086 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.013 μs |  2.697 μs | 0.1478 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.948 μs | 49.269 μs | 2.7006 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.604 μs |  4.747 μs | 0.2602 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.680 μs |  6.087 μs | 0.3336 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.028 μs |  5.593 μs | 0.3066 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.889 μs |  5.500 μs | 0.3015 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.165 μs |  2.487 μs | 0.1363 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.699 μs | 72.229 μs | 3.9591 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 35.184 μs | 72.942 μs | 3.9982 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 35.934 μs | 39.709 μs | 2.1766 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.986 μs | 13.667 μs | 0.7492 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.659 μs | 21.847 μs | 1.1975 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,706.85 ns | 72.566 ns | 3.978 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |  0.059 ns | 0.003 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.43 ns |  0.148 ns | 0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.59 ns |  1.023 ns | 0.056 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     36.59 ns | 15.210 ns | 0.834 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |  0.077 ns | 0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    128.68 ns | 43.712 ns | 2.396 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.28 ns |  0.570 ns | 0.031 ns |  0.59 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.772 μs |  16.309 μs |  0.8939 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 525.189 μs | 288.408 μs | 15.8086 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.286 μs |   2.610 μs |  0.1431 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 259.678 μs | 120.665 μs |  6.6141 μs |      80 B |


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