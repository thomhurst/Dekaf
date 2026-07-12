---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 07:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,278.86 μs** |    **264.948 μs** |    **14.523 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,608.57 μs |  5,293.230 μs |   290.140 μs |  0.26 |    0.04 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,461.37 μs** |  **1,311.768 μs** |    **71.902 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,537.12 μs |     31.004 μs |     1.699 μs |  0.34 |    0.00 |  15.6250 |       - |  341.23 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,123.12 μs** |    **440.442 μs** |    **24.142 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,904.82 μs |  4,742.212 μs |   259.937 μs |  0.31 |    0.04 |        - |       - |   38.13 KB |        0.20 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,637.47 μs** |  **3,900.103 μs** |   **213.778 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,392.14 μs |    908.430 μs |    49.794 μs |  0.66 |    0.01 |  15.6250 |       - |  404.91 KB |        0.21 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.52 μs** |     **18.955 μs** |     **1.039 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     88.87 μs |     60.787 μs |     3.332 μs |  0.65 |    0.02 |        - |       - |    5.23 KB |        0.16 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,380.85 μs** |    **921.334 μs** |    **50.501 μs** |  **1.00** |    **0.04** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    944.91 μs |    925.944 μs |    50.754 μs |  0.68 |    0.04 |        - |       - |  116.17 KB |        0.35 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,102.09 μs** |      **6.847 μs** |     **0.375 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    398.36 μs |    663.968 μs |    36.394 μs |  0.36 |    0.03 |        - |       - |   82.49 KB |        0.67 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,969.68 μs** | **33,122.104 μs** | **1,815.534 μs** |  **1.03** |    **0.24** |  **74.2188** |       **-** | **1226.34 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  4,436.73 μs | 12,412.598 μs |   680.376 μs |  0.46 |    0.10 |        - |       - | 1102.29 KB |        0.90 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,448.91 μs** |     **51.788 μs** |     **2.839 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,356.53 μs |    257.453 μs |    14.112 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,444.84 μs** |     **75.960 μs** |     **4.164 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,341.68 μs |    339.498 μs |    18.609 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,451.98 μs** |     **52.750 μs** |     **2.891 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,304.52 μs |    214.410 μs |    11.753 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,535.85 μs** |  **1,276.300 μs** |    **69.958 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,196.25 μs |    107.898 μs |     5.914 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.3 μs** |    **163.29 μs** |   **8.95 μs** |   **117.6 μs** |  **1.00** |    **0.09** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   170.4 μs |     73.62 μs |   4.04 μs |   172.3 μs |  1.41 |    0.09 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **167.0 μs** |    **758.52 μs** |  **41.58 μs** |   **188.4 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   186.2 μs |    200.44 μs |  10.99 μs |   180.5 μs |  1.17 |    0.30 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,131.4 μs** |  **5,171.90 μs** | **283.49 μs** | **1,165.1 μs** |  **1.05** |    **0.34** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,176.2 μs |    451.25 μs |  24.73 μs | 1,182.3 μs |  1.09 |    0.25 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,449.4 μs** | **12,309.02 μs** | **674.70 μs** | **1,076.6 μs** |  **1.13** |    **0.60** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,866.6 μs | 13,720.22 μs | 752.05 μs | 1,445.4 μs |  1.45 |    0.70 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **887.1 ns** | **1,616.3 ns** |  **88.59 ns** |  **1.01** |    **0.12** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,989.0 ns |   333.2 ns |  18.27 ns |  2.26 |    0.19 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,469.9 ns** |   **931.3 ns** |  **51.05 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,403.3 ns | 4,988.6 ns | 273.44 ns |  2.32 |    0.18 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.760 μs |  5.4314 μs | 0.2977 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.031 μs |  4.5499 μs | 0.2494 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 10.426 μs | 34.7008 μs | 1.9021 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.062 μs |  3.5620 μs | 0.1952 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.919 μs |  3.8326 μs | 0.2101 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.413 μs | 60.2284 μs | 3.3013 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 17.214 μs |  2.6206 μs | 0.1436 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.809 μs |  5.2134 μs | 0.2858 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.088 μs |  0.6407 μs | 0.0351 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.504 μs | 88.1298 μs | 4.8307 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.965 μs | 64.4705 μs | 3.5338 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 38.269 μs | 99.9492 μs | 5.4786 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.370 μs |  7.6891 μs | 0.4215 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.670 μs |  1.5810 μs | 0.0867 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,617.65 ns | 82.162 ns | 4.504 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.78 ns |  0.274 ns | 0.015 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.91 ns |  1.943 ns | 0.106 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.50 ns |  0.529 ns | 0.029 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.46 ns |  1.238 ns | 0.068 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.06 ns |  0.860 ns | 0.047 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.48 ns | 31.365 ns | 1.719 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.13 ns |  0.375 ns | 0.021 ns |  0.69 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev      | Allocated |
|------------------------ |-----------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.857 μs |     6.488 μs |   0.3556 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 581.073 μs | 1,874.577 μs | 102.7519 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.749 μs |    15.141 μs |   0.8299 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 215.974 μs |    65.927 μs |   3.6137 μs |      80 B |


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