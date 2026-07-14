---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 19:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,160.5 μs** |    **110.30 μs** |     **6.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,395.1 μs |    735.39 μs |    40.31 μs |  0.55 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,362.5 μs** |    **924.42 μs** |    **50.67 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,706.6 μs |  4,148.47 μs |   227.39 μs |  0.50 |    0.03 |  15.6250 |       - |  347437 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,200.3 μs** |    **251.17 μs** |    **13.77 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,343.7 μs |    368.45 μs |    20.20 μs |  0.54 |    0.00 |        - |       - |   38040 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,571.0 μs** |    **335.60 μs** |    **18.40 μs** |  **1.00** |    **0.00** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,728.1 μs | 25,605.37 μs | 1,403.52 μs |  0.85 |    0.10 |  15.6250 |       - |  399067 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.1 μs** |     **59.65 μs** |     **3.27 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    118.6 μs |    148.83 μs |     8.16 μs |  0.89 |    0.06 |        - |       - |    4229 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,340.5 μs** |    **438.10 μs** |    **24.01 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,672.1 μs |  9,281.34 μs |   508.74 μs |  1.25 |    0.33 |        - |       - |   43355 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,087.9 μs** |    **140.85 μs** |     **7.72 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125506 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    757.3 μs |    930.20 μs |    50.99 μs |  0.70 |    0.04 |        - |       - |    8571 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,935.9 μs** | **30,277.34 μs** | **1,659.60 μs** |  **1.02** |    **0.22** |  **74.2188** |       **-** | **1255483 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,206.5 μs |  3,750.37 μs |   205.57 μs |  0.74 |    0.12 |        - |       - |   79990 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.6 μs** |     **30.18 μs** |     **1.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,435.5 μs |    276.19 μs |    15.14 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **6,344.2 μs** | **29,079.96 μs** | **1,593.97 μs** |  **1.04** |    **0.30** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,429.4 μs |    448.88 μs |    24.60 μs |  0.56 |    0.11 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,426.8 μs** |     **99.88 μs** |     **5.47 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,442.4 μs |    113.88 μs |     6.24 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,427.1 μs** |     **90.98 μs** |     **4.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,430.0 μs |    186.77 μs |    10.24 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **144.8 μs** |    **710.5 μs** |  **38.95 μs** |   **157.1 μs** |  **1.06** |    **0.38** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   162.0 μs |    449.3 μs |  24.63 μs |   164.3 μs |  1.18 |    0.36 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.5 μs** |  **1,077.1 μs** |  **59.04 μs** |   **119.4 μs** |  **1.09** |    **0.48** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   198.0 μs |    311.5 μs |  17.07 μs |   199.7 μs |  1.41 |    0.40 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **972.6 μs** |  **2,858.1 μs** | **156.66 μs** |   **893.9 μs** |  **1.02** |    **0.19** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,243.2 μs |  1,942.0 μs | 106.45 μs | 1,199.6 μs |  1.30 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,444.4 μs** | **12,097.1 μs** | **663.08 μs** | **1,064.4 μs** |  **1.12** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,453.3 μs |    495.1 μs |  27.14 μs | 1,445.4 μs |  1.13 |    0.36 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **859.1 ns** |   **746.3 ns** |  **40.91 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,047.7 ns |   503.9 ns |  27.62 ns |  2.39 |    0.10 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,376.0 ns** | **1,249.4 ns** |  **68.48 ns** |  **1.00** |    **0.06** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,280.2 ns | 3,811.7 ns | 208.93 ns |  2.39 |    0.17 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.970 μs |   5.486 μs |  0.3007 μs | 33.133 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.826 μs |   8.012 μs |  0.4392 μs | 10.726 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.297 μs |   1.797 μs |  0.0985 μs |  8.267 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.092 μs |   2.159 μs |  0.1183 μs |  8.121 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.809 μs |   4.568 μs |  0.2504 μs | 10.696 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.471 μs |   5.972 μs |  0.3274 μs | 12.608 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.138 μs |   4.796 μs |  0.2629 μs | 12.028 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.025 μs |   4.051 μs |  0.2220 μs | 33.089 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.992 μs |   6.107 μs |  0.3347 μs |  9.835 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.677 μs |   2.140 μs |  0.1173 μs | 20.701 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 27.109 μs | 214.054 μs | 11.7330 μs | 21.550 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.726 μs |  27.220 μs |  1.4920 μs | 21.961 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.135 μs |   5.109 μs |  0.2801 μs |  4.978 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.892 μs |  12.211 μs |  0.6693 μs | 10.815 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,712.27 ns | 152.407 ns | 8.354 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.79 ns |   0.178 ns | 0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.18 ns |   0.295 ns | 0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.81 ns |   0.215 ns | 0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.21 ns |   0.824 ns | 0.045 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.96 ns |   0.027 ns | 0.001 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.60 ns |  18.121 ns | 0.993 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.90 ns |   0.719 ns | 0.039 ns |  0.75 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.499 μs |  13.992 μs |  0.7669 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 528.381 μs |  51.759 μs |  2.8371 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.501 μs |  15.550 μs |  0.8523 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 237.094 μs | 280.863 μs | 15.3951 μs |      80 B |


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