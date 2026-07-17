---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 21:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,251.6 μs** |    **517.89 μs** |    **28.39 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,746.3 μs |  1,130.76 μs |    61.98 μs |  0.44 |    0.01 |        - |       - |   35184 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,370.3 μs** |    **777.56 μs** |    **42.62 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,901.1 μs |  2,753.19 μs |   150.91 μs |  0.53 |    0.02 |  15.6250 |       - |  347359 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,465.1 μs** |  **1,451.47 μs** |    **79.56 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **198710 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,439.5 μs |  7,365.58 μs |   403.73 μs |  0.53 |    0.05 |        - |       - |   37478 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,537.8 μs** |  **9,698.76 μs** |   **531.62 μs** |  **1.00** |    **0.05** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,539.2 μs | 28,719.98 μs | 1,574.24 μs |  0.92 |    0.11 |        - |       - |  471016 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.7 μs** |     **19.98 μs** |     **1.09 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    303.1 μs |  3,645.46 μs |   199.82 μs |  2.35 |    1.34 |        - |       - |    4117 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,323.6 μs** |    **120.63 μs** |     **6.61 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,185.8 μs |    663.50 μs |    36.37 μs |  0.90 |    0.02 |        - |       - |   43310 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,009.4 μs** |     **12.05 μs** |     **0.66 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125390 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,077.1 μs |  2,626.23 μs |   143.95 μs |  1.07 |    0.12 |        - |       - |    6403 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,163.1 μs** | **29,014.54 μs** | **1,590.38 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1254636 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,107.4 μs | 13,984.71 μs |   766.55 μs |  1.13 |    0.20 |        - |       - |   62692 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,468.7 μs** |    **156.80 μs** |     **8.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,586.3 μs |     71.29 μs |     3.91 μs |  0.47 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,459.3 μs** |    **135.13 μs** |     **7.41 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,832.4 μs |  7,931.58 μs |   434.76 μs |  0.52 |    0.07 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,478.9 μs** |    **315.13 μs** |    **17.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,586.2 μs |     98.24 μs |     5.39 μs |  0.47 |    0.00 |        - |       - |     793 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,467.1 μs** |    **161.03 μs** |     **8.83 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,597.4 μs |    116.32 μs |     6.38 μs |  0.48 |    0.00 |        - |       - |     792 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **131.3 μs** |    **455.56 μs** |  **24.97 μs** |  **1.02** |    **0.23** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   154.6 μs |     94.14 μs |   5.16 μs |  1.20 |    0.19 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.2 μs** |    **590.54 μs** |  **32.37 μs** |  **1.03** |    **0.26** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   182.0 μs |    233.03 μs |  12.77 μs |  1.25 |    0.22 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,135.1 μs** |  **5,222.29 μs** | **286.25 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,335.6 μs |  2,082.28 μs | 114.14 μs |  1.23 |    0.29 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,652.6 μs** | **10,473.73 μs** | **574.10 μs** |  **1.09** |    **0.48** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,462.8 μs |    247.08 μs |  13.54 μs |  0.96 |    0.30 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **963.1 ns** | **1,484.2 ns** |  **81.35 ns** |  **1.00** |    **0.10** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,120.3 ns | 2,751.2 ns | 150.80 ns |  2.21 |    0.21 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,435.4 ns** | **2,137.7 ns** | **117.17 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,469.4 ns | 2,427.5 ns | 133.06 ns |  2.43 |    0.18 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.386 μs | 4.5031 μs | 0.2468 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.747 μs | 0.6407 μs | 0.0351 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.272 μs | 0.2056 μs | 0.0113 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.285 μs | 1.0158 μs | 0.0557 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.920 μs | 2.3926 μs | 0.1311 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.868 μs | 4.7155 μs | 0.2585 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.108 μs | 2.1867 μs | 0.1199 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.327 μs | 4.3605 μs | 0.2390 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.890 μs | 1.2926 μs | 0.0709 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.318 μs | 2.8690 μs | 0.1573 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.804 μs | 5.7472 μs | 0.3150 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.851 μs | 2.1532 μs | 0.1180 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.929 μs | 0.8440 μs | 0.0463 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 16.765 μs | 4.1408 μs | 0.2270 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,940.80 ns | 1,046.553 ns | 57.365 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.91 ns |     2.008 ns |  0.110 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.24 ns |     0.057 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.18 ns |     2.619 ns |  0.144 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.46 ns |     4.944 ns |  0.271 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.80 ns |     1.599 ns |  0.088 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.40 ns |    22.035 ns |  1.208 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.93 ns |     1.582 ns |  0.087 ns |  0.69 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.92 μs |   2.700 μs |  0.148 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 532.69 μs | 360.704 μs | 19.771 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.05 μs |   3.475 μs |  0.191 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 249.08 μs | 723.721 μs | 39.670 μs |      80 B |


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