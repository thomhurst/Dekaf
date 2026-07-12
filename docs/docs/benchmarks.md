---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 04:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,456.6 μs** |    **148.55 μs** |     **8.14 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,941.1 μs |  3,370.39 μs |   184.74 μs |  0.30 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,426.0 μs** |    **900.10 μs** |    **49.34 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,629.4 μs |  2,630.07 μs |   144.16 μs |  0.35 |    0.02 |  15.6250 |       - |  341.21 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,226.3 μs** |    **803.17 μs** |    **44.02 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,826.9 μs |  1,321.22 μs |    72.42 μs |  0.29 |    0.01 |        - |       - |   38.17 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,486.8 μs** |  **1,118.59 μs** |    **61.31 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,736.9 μs | 22,098.14 μs | 1,211.27 μs |  0.70 |    0.08 |  15.6250 |       - |  390.91 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.8 μs** |     **53.21 μs** |     **2.92 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    103.4 μs |    152.16 μs |     8.34 μs |  0.77 |    0.06 |        - |       - |    5.13 KB |        0.15 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,367.7 μs** |    **364.32 μs** |    **19.97 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    958.4 μs |    818.60 μs |    44.87 μs |  0.70 |    0.03 |        - |       - |   75.49 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,048.8 μs** |     **85.70 μs** |     **4.70 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    542.0 μs |  1,257.46 μs |    68.93 μs |  0.52 |    0.06 |        - |       - |   78.57 KB |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,475.9 μs** | **32,717.99 μs** | **1,793.38 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1225.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,427.3 μs | 13,099.44 μs |   718.02 μs |  0.37 |    0.10 |        - |       - |  988.77 KB |        0.81 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,486.4 μs** |    **113.86 μs** |     **6.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.7 μs |     52.59 μs |     2.88 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,888.9 μs** | **12,348.48 μs** |   **676.86 μs** |  **1.01** |    **0.14** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,118.3 μs |     98.90 μs |     5.42 μs |  0.19 |    0.02 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,589.9 μs** |    **391.83 μs** |    **21.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.5 μs |     17.25 μs |     0.95 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,476.6 μs** |    **158.57 μs** |     **8.69 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,113.4 μs |     33.06 μs |     1.81 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **154.6 μs** |    **898.4 μs** |  **49.24 μs** |   **173.1 μs** |  **1.09** |    **0.48** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   195.5 μs |    389.3 μs |  21.34 μs |   190.9 μs |  1.38 |    0.47 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **193.2 μs** |    **538.5 μs** |  **29.52 μs** |   **194.0 μs** |  **1.02** |    **0.19** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   239.7 μs |  1,192.3 μs |  65.36 μs |   243.0 μs |  1.26 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,160.9 μs** |  **5,212.4 μs** | **285.71 μs** | **1,175.1 μs** |  **1.04** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,206.8 μs |    180.7 μs |   9.90 μs | 1,211.8 μs |  1.09 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,655.9 μs** | **11,426.4 μs** | **626.32 μs** | **1,386.8 μs** |  **1.09** |    **0.48** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,873.0 μs | 13,086.2 μs | 717.30 μs | 1,468.5 μs |  1.23 |    0.54 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **947.2 ns** | **1,787.6 ns** |  **97.98 ns** |  **1.01** |    **0.13** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,043.3 ns |   438.1 ns |  24.01 ns |  2.17 |    0.21 |      - |     466 B |        0.72 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,458.8 ns** | **1,807.0 ns** |  **99.05 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,251.8 ns | 2,095.1 ns | 114.84 ns |  2.24 |    0.15 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.211 μs |   4.0357 μs |  0.2212 μs | 26.105 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.996 μs |  46.2192 μs |  2.5334 μs | 15.449 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.854 μs |   0.8426 μs |  0.0462 μs |  8.827 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.393 μs |   1.2909 μs |  0.0708 μs |  8.386 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.321 μs |   1.5673 μs |  0.0859 μs | 11.311 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.824 μs |   0.3558 μs |  0.0195 μs | 12.824 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.688 μs |   3.7374 μs |  0.2049 μs | 12.775 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 37.458 μs | 323.6666 μs | 17.7413 μs | 27.405 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.271 μs |  69.0029 μs |  3.7823 μs | 15.328 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.639 μs |   6.0717 μs |  0.3328 μs | 20.679 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.570 μs |  30.5136 μs |  1.6726 μs | 31.839 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.321 μs |  32.2820 μs |  1.7695 μs | 35.135 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  7.777 μs | 109.0192 μs |  5.9757 μs |  4.367 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.700 μs |   4.4494 μs |  0.2439 μs | 10.580 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,669.16 ns |  7.047 ns | 0.386 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.79 ns |  1.580 ns | 0.087 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.20 ns |  0.631 ns | 0.035 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.63 ns |  0.358 ns | 0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.37 ns |  8.761 ns | 0.480 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.80 ns |  0.198 ns | 0.011 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.60 ns | 17.257 ns | 0.946 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.78 ns |  0.904 ns | 0.050 ns |  0.71 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.334 μs |   2.789 μs |  0.1529 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 536.955 μs | 375.436 μs | 20.5789 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.275 μs |   2.896 μs |  0.1587 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 228.386 μs |  15.461 μs |  0.8475 μs |      80 B |


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