---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 22:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,316.2 μs** |  **1,195.97 μs** |    **65.56 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,630.9 μs |  3,376.99 μs |   185.10 μs |  0.26 |    0.03 |        - |       - |    34.7 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,391.1 μs** |    **165.05 μs** |     **9.05 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,456.9 μs |  1,127.73 μs |    61.81 μs |  0.33 |    0.01 |  15.6250 |       - |  341.19 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,155.8 μs** |    **790.31 μs** |    **43.32 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,803.6 μs |    259.87 μs |    14.24 μs |  0.29 |    0.00 |        - |       - |   38.16 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,424.3 μs** | **16,326.46 μs** |   **894.91 μs** |  **1.00** |    **0.08** | **109.3750** | **31.2500** | **1937.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,807.1 μs |  2,437.88 μs |   133.63 μs |  0.58 |    0.03 |  15.6250 |       - |  396.18 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.6 μs** |     **30.62 μs** |     **1.68 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    113.6 μs |     66.86 μs |     3.67 μs |  0.84 |    0.03 |        - |       - |    4.37 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,359.4 μs** |    **774.67 μs** |    **42.46 μs** |  **1.00** |    **0.04** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    927.4 μs |  1,227.22 μs |    67.27 μs |  0.68 |    0.05 |        - |       - |    85.2 KB |        0.25 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,072.8 μs** |     **60.86 μs** |     **3.34 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.53 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    422.8 μs |    750.95 μs |    41.16 μs |  0.39 |    0.03 |        - |       - |    74.7 KB |        0.61 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,697.7 μs** | **32,832.63 μs** | **1,799.67 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1226.12 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,986.0 μs | 12,052.49 μs |   660.64 μs |  0.42 |    0.10 |        - |       - |  979.55 KB |        0.80 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,421.5 μs** |     **46.23 μs** |     **2.53 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,158.9 μs |    194.26 μs |    10.65 μs |  0.21 |    0.00 |        - |       - |    1.08 KB |        0.92 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,431.6 μs** |    **100.41 μs** |     **5.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,168.2 μs |    117.14 μs |     6.42 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,437.5 μs** |    **261.66 μs** |    **14.34 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,147.4 μs |     97.27 μs |     5.33 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,433.9 μs** |     **19.73 μs** |     **1.08 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,142.1 μs |    161.08 μs |     8.83 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **148.9 μs** |    **176.8 μs** |   **9.69 μs** |   **144.2 μs** |  **1.00** |    **0.08** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   173.8 μs |    356.1 μs |  19.52 μs |   184.1 μs |  1.17 |    0.13 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **160.0 μs** |    **571.0 μs** |  **31.30 μs** |   **143.0 μs** |  **1.02** |    **0.23** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   204.2 μs |    478.6 μs |  26.23 μs |   190.0 μs |  1.31 |    0.25 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,117.1 μs** |  **4,792.4 μs** | **262.69 μs** | **1,134.1 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,261.9 μs |  1,968.3 μs | 107.89 μs | 1,227.6 μs |  1.17 |    0.27 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,505.3 μs** | **12,298.1 μs** | **674.10 μs** | **1,134.6 μs** |  **1.12** |    **0.57** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,440.8 μs |    389.4 μs |  21.34 μs | 1,449.4 μs |  1.07 |    0.33 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **888.7 ns** |   **708.4 ns** |  **38.83 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,122.4 ns | 2,946.5 ns | 161.51 ns |  2.39 |    0.18 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,544.6 ns** | **1,332.2 ns** |  **73.02 ns** |  **1.00** |    **0.06** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,324.4 ns | 2,759.4 ns | 151.25 ns |  2.16 |    0.12 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.095 μs | 50.0428 μs | 2.7430 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.038 μs |  3.2244 μs | 0.1767 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.550 μs |  0.8220 μs | 0.0451 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.410 μs |  0.2161 μs | 0.0118 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.642 μs |  2.4544 μs | 0.1345 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.548 μs |  3.5550 μs | 0.1949 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.978 μs | 23.2758 μs | 1.2758 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.014 μs |  4.2901 μs | 0.2352 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.850 μs |  1.1745 μs | 0.0644 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.065 μs | 76.1993 μs | 4.1767 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.360 μs | 82.7081 μs | 4.5335 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.273 μs | 19.8728 μs | 1.0893 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.929 μs | 11.4323 μs | 0.6266 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.857 μs |  5.5973 μs | 0.3068 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,929.25 ns | 481.166 ns | 26.374 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |   0.205 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.50 ns |   1.118 ns |  0.061 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.28 ns |   6.113 ns |  0.335 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.69 ns |  15.567 ns |  0.853 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |   0.166 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    126.65 ns |  67.028 ns |  3.674 ns |  1.00 |    0.04 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.04 ns |   7.046 ns |  0.386 ns |  0.61 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.50 μs |  24.960 μs |  1.368 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 510.22 μs | 384.261 μs | 21.063 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.02 μs |   0.547 μs |  0.030 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.16 μs |   1.045 μs |  0.057 μs |      80 B |


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