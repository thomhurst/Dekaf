---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 11:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,267.2 μs** |    **385.13 μs** |    **21.11 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,765.2 μs |  1,152.88 μs |    63.19 μs |  0.44 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,470.7 μs** |  **1,077.59 μs** |    **59.07 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,925.4 μs |  1,494.91 μs |    81.94 μs |  0.53 |    0.01 |  15.6250 |       - |  347181 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,173.1 μs** |  **1,316.03 μs** |    **72.14 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,235.2 μs |  4,399.66 μs |   241.16 μs |  0.52 |    0.03 |        - |       - |   37362 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,837.6 μs** |  **2,759.51 μs** |   **151.26 μs** |  **1.00** |    **0.01** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,354.0 μs |  8,091.45 μs |   443.52 μs |  1.04 |    0.03 |        - |       - |  470351 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **129.7 μs** |     **99.02 μs** |     **5.43 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    151.6 μs |    121.03 μs |     6.63 μs |  1.17 |    0.06 |        - |       - |    4110 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,329.9 μs** |    **182.59 μs** |    **10.01 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,415.4 μs |    925.13 μs |    50.71 μs |  1.06 |    0.03 |        - |       - |   42820 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,070.7 μs** |     **73.71 μs** |     **4.04 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125464 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,101.2 μs |    627.00 μs |    34.37 μs |  1.03 |    0.03 |        - |       - |    6184 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,113.0 μs** | **22,082.99 μs** | **1,210.44 μs** |  **1.01** |    **0.15** |  **74.2188** |       **-** | **1255849 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,607.8 μs |  7,737.80 μs |   424.14 μs |  1.06 |    0.12 |        - |       - |   61082 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,489.0 μs** |    **121.31 μs** |     **6.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,596.4 μs |     64.39 μs |     3.53 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,503.8 μs** |    **354.71 μs** |    **19.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,603.8 μs |    197.31 μs |    10.82 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,505.6 μs** |    **501.45 μs** |    **27.49 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,609.1 μs |    172.16 μs |     9.44 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,486.4 μs** |    **167.67 μs** |     **9.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,599.4 μs |     61.86 μs |     3.39 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **127.7 μs** |    **633.1 μs** |  **34.70 μs** |  **1.05** |    **0.33** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   205.4 μs |    430.4 μs |  23.59 μs |  1.68 |    0.39 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **147.3 μs** |    **557.5 μs** |  **30.56 μs** |  **1.03** |    **0.25** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   179.3 μs |    217.7 μs |  11.93 μs |  1.25 |    0.22 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,166.4 μs** |  **5,844.4 μs** | **320.35 μs** |  **1.05** |    **0.36** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,437.6 μs |  6,640.2 μs | 363.97 μs |  1.30 |    0.42 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,664.9 μs** | **11,053.7 μs** | **605.89 μs** |  **1.10** |    **0.51** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,840.0 μs | 11,758.3 μs | 644.51 μs |  1.21 |    0.55 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean        | Error        | StdDev       | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |------------:|-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |    **847.3 ns** |   **1,130.6 ns** |     **61.97 ns** |   **868.3 ns** |  **1.00** |    **0.09** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         |  2,094.8 ns |     442.3 ns |     24.25 ns | 2,089.8 ns |  2.48 |    0.16 |      - |     452 B |        0.70 |
|                      |             |             |              |              |            |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        |  **1,439.4 ns** |   **1,457.9 ns** |     **79.91 ns** | **1,416.5 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 17,349.4 ns | 429,773.9 ns | 23,557.36 ns | 4,020.2 ns | 12.08 |   14.23 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.102 μs |   2.716 μs |  0.1489 μs | 26.138 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.560 μs |   1.104 μs |  0.0605 μs | 10.559 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.352 μs |   2.061 μs |  0.1129 μs |  8.325 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.340 μs |   2.066 μs |  0.1133 μs |  8.400 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.793 μs |   3.846 μs |  0.2108 μs | 10.779 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.702 μs |   7.672 μs |  0.4205 μs | 12.539 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.067 μs |   7.337 μs |  0.4022 μs | 13.294 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.845 μs | 247.727 μs | 13.5788 μs | 27.050 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 23.780 μs | 268.631 μs | 14.7246 μs | 15.333 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.264 μs |   1.178 μs |  0.0646 μs | 20.267 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.027 μs |   9.814 μs |  0.5379 μs | 19.898 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.377 μs | 275.135 μs | 15.0811 μs | 27.841 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.439 μs |   8.188 μs |  0.4488 μs |  5.376 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.774 μs |  14.349 μs |  0.7865 μs | 15.107 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,916.80 ns | 2,127.323 ns | 116.606 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |     0.410 ns |   0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.16 ns |     1.770 ns |   0.097 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.67 ns |     1.719 ns |   0.094 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.34 ns |     4.665 ns |   0.256 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |     0.222 ns |   0.012 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.72 ns |    26.885 ns |   1.474 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.81 ns |     1.019 ns |   0.056 ns |  0.68 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.495 μs |  13.204 μs |  0.7237 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.254 μs | 159.607 μs |  8.7486 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.971 μs |  25.511 μs |  1.3983 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 252.125 μs | 213.892 μs | 11.7242 μs |      80 B |


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