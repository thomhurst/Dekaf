---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 16:18 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,325.9 μs** |    **988.65 μs** |    **54.19 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,723.3 μs |    458.57 μs |    25.14 μs |  0.43 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,430.6 μs** |  **1,490.13 μs** |    **81.68 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,866.9 μs |  2,882.77 μs |   158.01 μs |  0.52 |    0.02 |  15.6250 |       - |  347221 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,494.9 μs** |    **231.34 μs** |    **12.68 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,983.5 μs |  2,135.88 μs |   117.08 μs |  0.46 |    0.02 |        - |       - |   37809 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,115.5 μs** |  **2,645.81 μs** |   **145.03 μs** |  **1.00** |    **0.01** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,372.3 μs |  6,495.49 μs |   356.04 μs |  1.02 |    0.03 |        - |       - |  470226 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **106.5 μs** |    **101.78 μs** |     **5.58 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.9 μs |    153.49 μs |     8.41 μs |  1.14 |    0.09 |        - |       - |    4139 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,288.5 μs** |    **235.92 μs** |    **12.93 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,483.6 μs |  1,772.99 μs |    97.18 μs |  1.15 |    0.07 |        - |       - |   42848 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **997.5 μs** |     **56.84 μs** |     **3.12 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125375 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    982.7 μs |  1,221.14 μs |    66.94 μs |  0.99 |    0.06 |        - |       - |    6043 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,961.1 μs** | **31,713.49 μs** | **1,738.32 μs** |  **1.03** |    **0.26** |  **74.2188** |       **-** | **1254346 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,541.4 μs | 22,211.21 μs | 1,217.47 μs |  1.10 |    0.24 |        - |       - |   61553 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,504.1 μs** |    **184.28 μs** |    **10.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,593.0 μs |    336.72 μs |    18.46 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,837.1 μs** | **11,387.20 μs** |   **624.17 μs** |  **1.01** |    **0.13** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,589.8 μs |    197.91 μs |    10.85 μs |  0.45 |    0.04 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,485.4 μs** |     **98.45 μs** |     **5.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,583.0 μs |    404.55 μs |    22.17 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,467.7 μs** |     **95.63 μs** |     **5.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,580.2 μs |    175.63 μs |     9.63 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **140.5 μs** |    **637.9 μs** |  **34.96 μs** |   **153.6 μs** |  **1.05** |    **0.35** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   190.4 μs |    529.2 μs |  29.01 μs |   180.2 μs |  1.42 |    0.40 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **143.3 μs** |    **802.1 μs** |  **43.97 μs** |   **118.7 μs** |  **1.06** |    **0.37** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   197.8 μs |    962.4 μs |  52.75 μs |   167.8 μs |  1.46 |    0.48 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **952.5 μs** |  **3,598.8 μs** | **197.26 μs** |   **840.7 μs** |  **1.03** |    **0.25** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,311.7 μs |  2,217.5 μs | 121.55 μs | 1,242.0 μs |  1.41 |    0.25 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,464.2 μs** | **11,932.1 μs** | **654.04 μs** | **1,123.6 μs** |  **1.12** |    **0.57** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,325.7 μs |  4,516.9 μs | 247.58 μs | 2,229.3 μs |  1.78 |    0.58 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **889.5 ns** |   **916.1 ns** |  **50.22 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,143.0 ns | 2,350.6 ns | 128.84 ns |  2.41 |    0.17 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,448.6 ns** |   **920.0 ns** |  **50.43 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,456.4 ns | 2,685.2 ns | 147.18 ns |  2.39 |    0.11 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.899 μs |  53.726 μs |  2.9449 μs | 26.249 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.456 μs |   2.281 μs |  0.1250 μs | 10.459 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.280 μs |   2.372 μs |  0.1300 μs |  8.280 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 15.546 μs | 236.393 μs | 12.9575 μs |  8.085 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.994 μs |   2.004 μs |  0.1098 μs | 10.940 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.640 μs |   4.793 μs |  0.2627 μs | 12.493 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 22.385 μs | 298.041 μs | 16.3366 μs | 13.164 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.997 μs |   1.735 μs |  0.0951 μs | 27.031 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.411 μs |   5.563 μs |  0.3049 μs | 15.304 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.415 μs | 172.517 μs |  9.4562 μs | 21.248 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.216 μs |  10.419 μs |  0.5711 μs | 20.203 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.490 μs |   9.414 μs |  0.5160 μs | 22.607 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.939 μs |   1.116 μs |  0.0612 μs |  4.969 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.406 μs |  30.451 μs |  1.6691 μs | 14.447 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,691.90 ns | 324.821 ns | 17.805 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.82 ns |   0.301 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.31 ns |   0.993 ns |  0.054 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.80 ns |   2.149 ns |  0.118 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     36.76 ns |  15.033 ns |  0.824 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.139 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    150.50 ns |  94.764 ns |  5.194 ns |  1.00 |    0.04 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.23 ns |   3.586 ns |  0.197 ns |  0.51 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Median     | Allocated |
|------------------------ |----------:|-----------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.77 μs |   6.596 μs |  0.362 μs |  13.646 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 523.02 μs | 186.886 μs | 10.244 μs | 518.975 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  18.73 μs | 282.933 μs | 15.509 μs |   9.969 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 284.43 μs | 595.614 μs | 32.648 μs | 272.918 μs |      80 B |


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