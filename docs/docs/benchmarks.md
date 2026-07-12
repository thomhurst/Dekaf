---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 11:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,320.1 μs** |  **1,044.80 μs** |    **57.27 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,777.3 μs |  4,829.12 μs |   264.70 μs |  0.28 |    0.04 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,461.7 μs** |    **472.52 μs** |    **25.90 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,694.0 μs |  3,374.43 μs |   184.96 μs |  0.36 |    0.02 |  15.6250 |       - |  341.23 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,130.7 μs** |    **741.11 μs** |    **40.62 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,930.2 μs |  2,298.70 μs |   126.00 μs |  0.31 |    0.02 |        - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,788.5 μs** |  **2,350.39 μs** |   **128.83 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,568.7 μs | 12,701.23 μs |   696.20 μs |  0.67 |    0.05 |  15.6250 |       - |  391.73 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.8 μs** |     **19.79 μs** |     **1.08 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    121.0 μs |    276.86 μs |    15.18 μs |  0.88 |    0.10 |        - |       - |    4.33 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,307.2 μs** |  **1,803.69 μs** |    **98.87 μs** |  **1.00** |    **0.10** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,210.5 μs |    754.52 μs |    41.36 μs |  0.93 |    0.07 |        - |       - |   44.26 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **665.0 μs** |  **8,312.94 μs** |   **455.66 μs** |  **1.49** |    **1.46** |   **7.3242** |       **-** |  **122.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    863.8 μs |    275.36 μs |    15.09 μs |  1.94 |    1.32 |        - |       - |    7.88 KB |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,531.9 μs** | **33,142.74 μs** | **1,816.67 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1226.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,345.1 μs |  9,001.78 μs |   493.42 μs |  0.71 |    0.12 |        - |       - |  100.45 KB |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,452.5 μs** |     **83.39 μs** |     **4.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,404.8 μs |    368.30 μs |    20.19 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,478.5 μs** |    **209.79 μs** |    **11.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,403.2 μs |     43.79 μs |     2.40 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **6,302.4 μs** | **24,691.33 μs** | **1,353.42 μs** |  **1.03** |    **0.26** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,432.5 μs |    311.62 μs |    17.08 μs |  0.23 |    0.04 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,481.4 μs** |    **300.96 μs** |    **16.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,388.1 μs |    278.12 μs |    15.24 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **128.0 μs** |    **762.5 μs** |  **41.79 μs** |   **113.1 μs** |  **1.07** |    **0.41** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   179.5 μs |    542.6 μs |  29.74 μs |   170.5 μs |  1.50 |    0.44 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **140.9 μs** |    **643.3 μs** |  **35.26 μs** |   **121.2 μs** |  **1.04** |    **0.30** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   208.3 μs |    703.0 μs |  38.53 μs |   222.9 μs |  1.53 |    0.38 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,110.0 μs** |  **5,010.3 μs** | **274.63 μs** | **1,121.5 μs** |  **1.04** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,158.0 μs |    331.9 μs |  18.19 μs | 1,162.2 μs |  1.09 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,458.3 μs** | **12,266.1 μs** | **672.35 μs** | **1,093.5 μs** |  **1.13** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,488.9 μs |    345.7 μs |  18.95 μs | 1,484.3 μs |  1.15 |    0.36 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **969.8 ns** | **3,069.2 ns** | **168.23 ns** |  **1.02** |    **0.21** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,052.3 ns | 1,432.4 ns |  78.51 ns |  2.16 |    0.32 |      - |     466 B |        0.72 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,549.8 ns** | **2,735.1 ns** | **149.92 ns** |  **1.01** |    **0.12** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,262.4 ns | 1,227.5 ns |  67.28 ns |  2.12 |    0.17 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.393 μs |  9.669 μs | 0.5300 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.580 μs | 14.764 μs | 0.8093 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.196 μs | 13.565 μs | 0.7435 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  6.294 μs | 13.101 μs | 0.7181 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.795 μs | 20.892 μs | 1.1452 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.163 μs | 14.009 μs | 0.7679 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 10.748 μs | 18.679 μs | 1.0238 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.672 μs | 26.436 μs | 1.4490 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.116 μs |  9.235 μs | 0.5062 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 17.907 μs | 10.624 μs | 0.5824 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 36.948 μs | 78.111 μs | 4.2815 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 38.018 μs | 75.113 μs | 4.1172 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.539 μs | 23.027 μs | 1.2622 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.158 μs | 16.145 μs | 0.8850 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,610.003 ns | 114.9530 ns | 6.3010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.294 ns |   0.2991 ns | 0.0164 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     13.518 ns |   2.2780 ns | 0.1249 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     27.739 ns |   0.8989 ns | 0.0493 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.125 ns |  14.4941 ns | 0.7945 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      7.207 ns |   0.3519 ns | 0.0193 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |     93.850 ns |  21.6192 ns | 1.1850 ns |  1.00 |    0.02 | 0.0106 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     69.556 ns |   0.4564 ns | 0.0250 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.38 μs |  40.247 μs |  2.206 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 386.81 μs | 293.423 μs | 16.083 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  11.56 μs |   9.896 μs |  0.542 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 225.87 μs | 176.376 μs |  9.668 μs |      80 B |


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