---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 18:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,538.20 μs** |  **2,093.45 μs** |   **114.749 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,732.44 μs |  4,973.22 μs |   272.599 μs |  0.27 |    0.04 |        - |       - |   34.72 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,608.65 μs** |  **1,895.66 μs** |   **103.908 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,675.05 μs |  5,528.22 μs |   303.020 μs |  0.35 |    0.03 |  15.6250 |       - |  341.44 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,199.94 μs** |    **637.68 μs** |    **34.953 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,831.70 μs |  1,326.28 μs |    72.698 μs |  0.30 |    0.01 |        - |       - |   38.17 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,021.74 μs** |  **2,358.33 μs** |   **129.268 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,210.95 μs |  3,797.48 μs |   208.153 μs |  0.63 |    0.01 |  15.6250 |       - |  398.32 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.35 μs** |     **75.67 μs** |     **4.148 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     90.09 μs |     64.62 μs |     3.542 μs |  0.64 |    0.03 |        - |       - |    6.51 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,402.62 μs** |    **669.39 μs** |    **36.692 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,003.18 μs |  1,610.43 μs |    88.273 μs |  0.72 |    0.06 |        - |       - |  120.33 KB |        0.36 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **668.53 μs** |  **7,745.62 μs** |   **424.563 μs** |  **1.32** |    **1.08** |   **7.3242** |       **-** |   **122.6 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    456.04 μs |    885.40 μs |    48.532 μs |  0.90 |    0.49 |        - |       - |   85.64 KB |        0.70 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,494.20 μs** | **34,852.10 μs** | **1,910.361 μs** |  **1.03** |    **0.24** |  **74.2188** |       **-** | **1227.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,461.56 μs |  4,721.54 μs |   258.803 μs |  0.34 |    0.06 |        - |       - |  834.89 KB |        0.68 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,547.67 μs** |  **1,072.08 μs** |    **58.764 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,136.04 μs |    243.16 μs |    13.329 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,513.86 μs** |    **235.68 μs** |    **12.919 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,140.27 μs |     79.81 μs |     4.375 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,508.63 μs** |     **95.20 μs** |     **5.218 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,128.67 μs |    151.90 μs |     8.326 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,491.71 μs** |    **330.68 μs** |    **18.126 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,130.28 μs |     77.22 μs |     4.233 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **150.0 μs** |    **838.2 μs** |  **45.94 μs** |  **1.08** |    **0.46** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   187.8 μs |    500.1 μs |  27.41 μs |  1.35 |    0.47 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **145.9 μs** |    **825.5 μs** |  **45.25 μs** |  **1.06** |    **0.38** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   226.5 μs |    340.3 μs |  18.65 μs |  1.64 |    0.39 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **923.9 μs** |  **3,063.0 μs** | **167.89 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,216.9 μs |    392.8 μs |  21.53 μs |  1.34 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,609.5 μs** | **10,869.3 μs** | **595.78 μs** |  **1.10** |    **0.51** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,421.4 μs |    643.1 μs |  35.25 μs |  0.97 |    0.31 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **869.6 ns** |   **704.1 ns** |  **38.59 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,096.5 ns | 2,539.0 ns | 139.17 ns |  2.41 |    0.17 |      - |     466 B |        0.72 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,497.9 ns** | **1,656.0 ns** |  **90.77 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,476.0 ns | 1,285.5 ns |  70.46 ns |  2.33 |    0.13 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.548 μs |  3.352 μs | 0.1837 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.898 μs |  2.903 μs | 0.1591 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.703 μs |  8.544 μs | 0.4683 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  6.135 μs |  4.714 μs | 0.2584 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.903 μs |  2.641 μs | 0.1448 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 10.733 μs |  6.917 μs | 0.3792 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  9.829 μs |  4.598 μs | 0.2520 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.442 μs |  2.329 μs | 0.1277 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.830 μs |  1.906 μs | 0.1045 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 16.169 μs |  2.737 μs | 0.1500 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.932 μs | 44.098 μs | 2.4171 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.411 μs | 49.425 μs | 2.7092 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.546 μs | 12.904 μs | 0.7073 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.773 μs | 10.975 μs | 0.6016 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,775.014 ns | 80.3571 ns | 4.4046 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.207 ns |  3.3306 ns | 0.1826 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    15.206 ns |  0.6601 ns | 0.0362 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    29.961 ns |  0.3687 ns | 0.0202 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    26.878 ns |  5.7543 ns | 0.3154 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.279 ns |  0.1137 ns | 0.0062 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    88.428 ns | 42.4684 ns | 2.3278 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    61.942 ns |  1.4600 ns | 0.0800 ns |  0.70 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   7.888 μs |  14.632 μs |  0.8020 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 419.862 μs | 558.583 μs | 30.6178 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.312 μs |   6.477 μs |  0.3550 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 184.727 μs | 547.271 μs | 29.9978 μs |      80 B |


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