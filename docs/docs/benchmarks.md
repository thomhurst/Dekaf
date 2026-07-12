---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 18:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,229.4 μs** |    **261.74 μs** |    **14.35 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,679.1 μs |  2,255.46 μs |   123.63 μs |  0.27 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,396.9 μs** |    **845.50 μs** |    **46.34 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,534.1 μs |  2,689.26 μs |   147.41 μs |  0.34 |    0.02 |  15.6250 |       - |  341.31 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,199.6 μs** |  **1,384.97 μs** |    **75.92 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,779.5 μs |    318.21 μs |    17.44 μs |  0.29 |    0.00 |        - |       - |   38.18 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,700.6 μs** |  **2,489.99 μs** |   **136.48 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,020.8 μs | 16,184.59 μs |   887.13 μs |  0.71 |    0.06 |  15.6250 |       - |  429.16 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.4 μs** |      **2.76 μs** |     **0.15 μs** |  **1.00** |    **0.00** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    114.0 μs |    108.61 μs |     5.95 μs |  0.84 |    0.04 |        - |       - |    4.29 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,437.9 μs** |    **592.08 μs** |    **32.45 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,029.0 μs |    704.47 μs |    38.61 μs |  0.72 |    0.03 |        - |       - |   43.73 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,094.4 μs** |     **82.07 μs** |     **4.50 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    816.7 μs |  1,660.03 μs |    90.99 μs |  0.75 |    0.07 |        - |       - |    7.86 KB |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,938.1 μs** | **36,091.06 μs** | **1,978.27 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** | **1226.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,777.9 μs | 16,430.42 μs |   900.61 μs |  0.81 |    0.18 |        - |       - |   95.24 KB |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,447.9 μs** |    **123.50 μs** |     **6.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,118.8 μs |     22.66 μs |     1.24 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,460.4 μs** |    **262.29 μs** |    **14.38 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,158.7 μs |  1,009.23 μs |    55.32 μs |  0.21 |    0.01 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,450.1 μs** |    **268.31 μs** |    **14.71 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,217.6 μs |  1,472.75 μs |    80.73 μs |  0.22 |    0.01 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,456.3 μs** |    **100.98 μs** |     **5.53 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,419.2 μs |    358.32 μs |    19.64 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **115.9 μs** |    **517.9 μs** |  **28.39 μs** |   **103.6 μs** |  **1.04** |    **0.30** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   167.0 μs |    471.1 μs |  25.82 μs |   155.7 μs |  1.49 |    0.35 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **156.1 μs** |    **526.8 μs** |  **28.88 μs** |   **143.5 μs** |  **1.02** |    **0.22** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.1 μs |    124.1 μs |   6.80 μs |   177.0 μs |  1.14 |    0.17 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **971.2 μs** |  **2,462.4 μs** | **134.97 μs** |   **904.6 μs** |  **1.01** |    **0.17** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,343.4 μs |  6,369.8 μs | 349.15 μs | 1,144.3 μs |  1.40 |    0.35 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,517.7 μs** | **12,803.7 μs** | **701.81 μs** | **1,139.2 μs** |  **1.13** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,415.2 μs |    621.6 μs |  34.07 μs | 1,433.0 μs |  1.05 |    0.33 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **962.3 ns** |   **971.4 ns** |  **53.24 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,972.7 ns |   647.7 ns |  35.50 ns |  2.05 |    0.10 |      - |     466 B |        0.72 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,511.8 ns** | **3,241.8 ns** | **177.70 ns** |  **1.01** |    **0.14** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,202.4 ns | 2,015.1 ns | 110.45 ns |  2.14 |    0.21 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 31.279 μs |   0.4862 μs |  0.0267 μs | 31.290 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.594 μs |  52.2488 μs |  2.8639 μs | 11.011 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.284 μs |   2.7457 μs |  0.1505 μs | 13.284 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.868 μs |  43.3784 μs |  2.3777 μs |  8.525 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.823 μs |   6.0793 μs |  0.3332 μs | 11.683 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 23.970 μs | 251.7401 μs | 13.7987 μs | 19.541 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 18.882 μs |   8.7236 μs |  0.4782 μs | 19.047 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.022 μs |  32.8748 μs |  1.8020 μs | 27.130 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.969 μs |   2.3101 μs |  0.1266 μs |  9.016 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 29.529 μs | 276.5273 μs | 15.1574 μs | 21.040 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.992 μs |  33.8486 μs |  1.8554 μs | 30.197 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 35.423 μs |  30.0115 μs |  1.6450 μs | 36.068 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.933 μs |   9.3140 μs |  0.5105 μs |  4.939 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.746 μs |   1.8280 μs |  0.1002 μs | 10.739 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,109.97 ns | 509.164 ns | 27.909 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.204 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.21 ns |   0.221 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.66 ns |   0.330 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.97 ns |   7.420 ns |  0.407 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.78 ns |   0.071 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    125.16 ns |  64.303 ns |  3.525 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.47 ns |   0.409 ns |  0.022 ns |  0.61 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.073 μs |   7.399 μs |  0.4056 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 537.340 μs | 882.504 μs | 48.3730 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.847 μs |  17.996 μs |  0.9864 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 237.083 μs | 160.099 μs |  8.7756 μs |      80 B |


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