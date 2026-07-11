---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 18:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,638.91 μs** |    **635.35 μs** |  **34.825 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,281.12 μs |  1,034.82 μs |  56.722 μs |  0.23 |    0.01 |   1.9531 |       - |    34.7 KB |        0.33 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,565.86 μs** |    **189.82 μs** |  **10.405 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 1,391.39 μs |  1,071.26 μs |  58.719 μs |  0.21 |    0.01 |  19.5313 |  3.9063 |  341.16 KB |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **5,887.82 μs** |    **293.75 μs** |  **16.102 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,091.84 μs |    123.21 μs |   6.754 μs |  0.19 |    0.00 |   1.9531 |       - |   38.46 KB |        0.20 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,061.01 μs** |  **8,991.83 μs** | **492.872 μs** |  **1.00** |    **0.08** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 3,640.47 μs | 16,742.25 μs | 917.699 μs |  0.52 |    0.12 |  23.4375 |       - |  383.79 KB |        0.20 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **60.28 μs** |     **41.08 μs** |   **2.251 μs** |  **1.00** |    **0.05** |   **2.0142** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    43.26 μs |     61.09 μs |   3.348 μs |  0.72 |    0.05 |   0.2441 |       - |    9.38 KB |        0.28 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **636.85 μs** |    **296.61 μs** |  **16.258 μs** |  **1.00** |    **0.03** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   399.65 μs |    554.56 μs |  30.397 μs |  0.63 |    0.04 |   1.9531 |       - |  135.97 KB |        0.40 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **586.87 μs** |  **2,451.74 μs** | **134.388 μs** |  **1.03** |    **0.28** |   **7.4463** |       **-** |   **121.8 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   147.01 μs |    158.83 μs |   8.706 μs |  0.26 |    0.05 |   0.9766 |  0.4883 |   95.64 KB |        0.79 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **5,092.82 μs** |  **1,647.93 μs** |  **90.329 μs** |  **1.00** |    **0.02** |  **74.2188** |       **-** | **1220.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,864.88 μs |  4,220.22 μs | 231.325 μs |  0.37 |    0.04 |   7.8125 |       - | 1161.25 KB |        0.95 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,215.46 μs** |     **37.57 μs** |   **2.059 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,214.42 μs |    114.86 μs |   6.296 μs |  0.23 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,216.15 μs** |     **15.71 μs** |   **0.861 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,231.32 μs |    149.24 μs |   8.180 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,216.69 μs** |     **89.95 μs** |   **4.931 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,229.66 μs |    241.24 μs |  13.223 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,223.38 μs** |     **74.81 μs** |   **4.101 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,169.12 μs |    163.31 μs |   8.952 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean      | Error        | StdDev     | Median    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |----------:|-------------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |  **75.09 μs** |    **491.41 μs** |  **26.936 μs** |  **65.87 μs** |  **1.08** |    **0.46** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |  97.16 μs |    464.13 μs |  25.440 μs |  97.87 μs |  1.40 |    0.51 |   39.98 KB |        0.62 |
|                      |              |             |           |              |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |  **80.04 μs** |    **486.49 μs** |  **26.666 μs** |  **65.12 μs** |  **1.07** |    **0.41** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        | 103.18 μs |    182.58 μs |  10.008 μs | 101.99 μs |  1.37 |    0.35 |  215.77 KB |        0.90 |
|                      |              |             |           |              |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **591.61 μs** |  **1,391.01 μs** |  **76.246 μs** | **600.35 μs** |  **1.01** |    **0.16** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 507.47 μs |    160.57 μs |   8.801 μs | 508.13 μs |  0.87 |    0.10 |  476.66 KB |        0.73 |
|                      |              |             |           |              |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **860.98 μs** | **10,575.98 μs** | **579.705 μs** | **528.76 μs** |  **1.28** |    **0.96** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 616.06 μs |    194.39 μs |  10.655 μs | 614.95 μs |  0.91 |    0.38 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **472.6 ns** | **1,163.8 ns** | **63.79 ns** |  **1.01** |    **0.17** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         |   947.3 ns | 1,612.9 ns | 88.41 ns |  2.03 |    0.30 |      - |     452 B |        0.70 |
|                      |             |            |            |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        |   **737.6 ns** | **1,174.2 ns** | **64.36 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 1,358.8 ns |   349.6 ns | 19.16 ns |  1.85 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.506 μs | 13.169 μs | 0.7219 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.758 μs | 24.885 μs | 1.3640 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.170 μs |  2.815 μs | 0.1543 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.451 μs |  8.012 μs | 0.4392 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.380 μs |  4.759 μs | 0.2608 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.333 μs |  6.805 μs | 0.3730 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.415 μs |  3.940 μs | 0.2160 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.371 μs |  5.033 μs | 0.2759 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.512 μs |  4.723 μs | 0.2589 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.732 μs |  5.118 μs | 0.2805 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.642 μs | 46.262 μs | 2.5358 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 39.974 μs | 47.353 μs | 2.5956 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.934 μs | 19.252 μs | 1.0553 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.883 μs | 27.910 μs | 1.5298 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,628.32 ns | 650.015 ns | 35.630 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.82 ns |   1.078 ns |  0.059 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     22.00 ns |   6.003 ns |  0.329 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.56 ns |   0.652 ns |  0.036 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.73 ns |   1.280 ns |  0.070 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.01 ns |   0.552 ns |  0.030 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    121.29 ns |  40.987 ns |  2.247 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     80.06 ns |  18.834 ns |  1.032 ns |  0.66 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error       | StdDev    | Allocated |
|------------------------ |----------:|------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.03 μs |    21.04 μs |  1.153 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 543.34 μs |   175.26 μs |  9.607 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  11.29 μs |    18.34 μs |  1.005 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 375.78 μs | 1,574.97 μs | 86.329 μs |      80 B |


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