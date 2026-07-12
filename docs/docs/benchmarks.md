---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 13:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,253.53 μs** |  **5,580.71 μs** |   **305.898 μs** | **6,082.24 μs** |  **1.00** |    **0.06** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,568.62 μs |  3,387.58 μs |   185.685 μs | 1,571.22 μs |  0.25 |    0.03 |       - |   34.72 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,459.27 μs** |    **709.81 μs** |    **38.907 μs** | **7,460.08 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,505.33 μs |    708.56 μs |    38.839 μs | 2,493.86 μs |  0.34 |    0.00 |       - |  341.08 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,404.05 μs** |    **240.31 μs** |    **13.172 μs** | **6,397.52 μs** |  **1.00** |    **0.00** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,505.51 μs |  3,581.95 μs |   196.339 μs | 1,578.94 μs |  0.24 |    0.03 |       - |   38.08 KB |        0.20 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,279.38 μs** |    **949.17 μs** |    **52.027 μs** | **8,295.99 μs** |  **1.00** |    **0.01** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,431.60 μs |  1,748.52 μs |    95.842 μs | 6,395.46 μs |  0.78 |    0.01 |       - |  366.41 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **130.15 μs** |     **71.20 μs** |     **3.903 μs** |   **131.40 μs** |  **1.00** |    **0.04** |  **0.2441** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    91.01 μs |    198.68 μs |    10.890 μs |    85.34 μs |  0.70 |    0.07 |       - |    4.31 KB |        0.13 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,308.54 μs** |    **216.95 μs** |    **11.892 μs** | **1,306.45 μs** |  **1.00** |    **0.01** |  **3.9063** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      | 1,078.86 μs |  1,849.37 μs |   101.370 μs | 1,048.87 μs |  0.82 |    0.07 |       - |    45.2 KB |        0.13 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **644.98 μs** |    **141.30 μs** |     **7.745 μs** |   **643.06 μs** |  **1.00** |    **0.01** |  **1.4648** |  **121.87 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   798.92 μs |  3,320.21 μs |   181.992 μs |   697.93 μs |  1.24 |    0.24 |       - |    7.54 KB |        0.06 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **5,058.25 μs** | **50,745.29 μs** | **2,781.521 μs** | **6,484.81 μs** |  **1.42** |    **1.27** | **11.7188** | **1220.27 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 7,828.39 μs | 43,269.42 μs | 2,371.743 μs | 6,923.41 μs |  2.19 |    1.67 |       - |   86.56 KB |        0.07 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,464.53 μs** |  **4,238.94 μs** |   **232.350 μs** | **5,334.99 μs** |  **1.00** |    **0.05** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,309.26 μs |    301.10 μs |    16.504 μs | 1,300.83 μs |  0.24 |    0.01 |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,339.84 μs** |    **410.55 μs** |    **22.504 μs** | **5,328.88 μs** |  **1.00** |    **0.01** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,326.48 μs |    253.79 μs |    13.911 μs | 1,325.65 μs |  0.25 |    0.00 |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,335.25 μs** |    **189.46 μs** |    **10.385 μs** | **5,338.80 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,332.39 μs |    624.10 μs |    34.209 μs | 1,328.30 μs |  0.25 |    0.01 |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,560.99 μs** |  **7,287.36 μs** |   **399.445 μs** | **5,337.58 μs** |  **1.00** |    **0.09** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,333.09 μs |    367.95 μs |    20.169 μs | 1,337.02 μs |  0.24 |    0.01 |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **156.9 μs** |    **684.9 μs** |  **37.54 μs** |  **1.04** |    **0.29** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   165.1 μs |    970.9 μs |  53.22 μs |  1.09 |    0.37 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **185.3 μs** |    **474.2 μs** |  **25.99 μs** |  **1.01** |    **0.18** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   202.0 μs |    510.8 μs |  28.00 μs |  1.11 |    0.20 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,206.7 μs** |  **1,852.3 μs** | **101.53 μs** |  **1.00** |    **0.10** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   982.6 μs |    884.0 μs |  48.45 μs |  0.82 |    0.07 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,567.5 μs** |  **9,623.6 μs** | **527.50 μs** |  **1.07** |    **0.41** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,638.7 μs | 10,369.8 μs | 568.40 μs |  1.12 |    0.44 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **949.7 ns** |   **849.6 ns** |  **46.57 ns** |  **1.00** |    **0.06** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,697.1 ns | 3,065.7 ns | 168.04 ns |  1.79 |    0.17 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,561.6 ns** | **2,194.5 ns** | **120.29 ns** |  **1.00** |    **0.10** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,330.6 ns | 2,134.6 ns | 117.00 ns |  1.50 |    0.12 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.186 μs |  19.369 μs | 1.0617 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.564 μs |  14.804 μs | 0.8115 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.651 μs |  18.854 μs | 1.0334 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 11.099 μs |  17.918 μs | 0.9822 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.716 μs |  17.545 μs | 0.9617 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.548 μs |  20.912 μs | 1.1463 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 18.056 μs |  59.772 μs | 3.2763 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 23.018 μs |  11.050 μs | 0.6057 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.310 μs |  14.492 μs | 0.7944 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 18.742 μs |   2.557 μs | 0.1402 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 47.778 μs | 108.827 μs | 5.9652 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 42.460 μs | 103.439 μs | 5.6698 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  6.625 μs |  27.191 μs | 1.4905 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  9.019 μs |  29.035 μs | 1.5915 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error        | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|-------------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,861.160 ns | 4,278.339 ns | 234.5102 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |              |             |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     13.292 ns |     1.504 ns |   0.0824 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.772 ns |     2.361 ns |   0.1294 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     31.175 ns |     4.131 ns |   0.2264 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.625 ns |    10.514 ns |   0.5763 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.890 ns |     2.911 ns |   0.1595 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |              |             |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.926 ns |     7.484 ns |   0.4102 ns |  1.00 |    0.00 | 0.0106 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.629 ns |    14.519 ns |   0.7959 ns |  0.71 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.932 μs |  14.355 μs |  0.7868 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 438.407 μs | 123.960 μs |  6.7946 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.352 μs |  18.400 μs |  1.0086 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 201.136 μs | 219.443 μs | 12.0284 μs |      80 B |


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