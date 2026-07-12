---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 14:48 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,111.1 μs** |    **144.34 μs** |     **7.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,624.6 μs |  1,463.06 μs |    80.20 μs |  0.27 |    0.01 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,238.2 μs** |    **922.24 μs** |    **50.55 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,514.8 μs |    747.54 μs |    40.98 μs |  0.35 |    0.01 |  15.6250 |       - |  341.35 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,630.6 μs** |    **699.68 μs** |    **38.35 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,716.4 μs |  6,369.81 μs |   349.15 μs |  0.26 |    0.05 |        - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,145.7 μs** |  **4,558.14 μs** |   **249.85 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,372.8 μs |  7,695.25 μs |   421.80 μs |  0.75 |    0.04 |  15.6250 |       - |  434.03 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **113.3 μs** |     **21.54 μs** |     **1.18 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.2 μs |     93.43 μs |     5.12 μs |  0.97 |    0.04 |        - |       - |    4.32 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,227.2 μs** |  **1,043.29 μs** |    **57.19 μs** |  **1.00** |    **0.06** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,063.3 μs |  2,020.63 μs |   110.76 μs |  0.87 |    0.09 |        - |       - |   43.26 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **960.7 μs** |    **154.18 μs** |     **8.45 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    794.6 μs |  1,198.87 μs |    65.71 μs |  0.83 |    0.06 |        - |       - |    8.33 KB |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,643.3 μs** | **30,945.36 μs** | **1,696.22 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** | **1224.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,737.2 μs | 14,612.61 μs |   800.97 μs |  0.80 |    0.18 |        - |       - |   94.52 KB |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,392.7 μs** |     **63.82 μs** |     **3.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,367.4 μs |    159.16 μs |     8.72 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,385.9 μs** |     **47.84 μs** |     **2.62 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,242.2 μs |  2,324.91 μs |   127.44 μs |  0.23 |    0.02 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,392.8 μs** |     **14.97 μs** |     **0.82 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,103.1 μs |     50.42 μs |     2.76 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,401.3 μs** |    **143.49 μs** |     **7.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,105.5 μs |     15.18 μs |     0.83 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **117.4 μs** |    **507.9 μs** |  **27.84 μs** |  **1.03** |    **0.29** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   175.0 μs |    249.5 μs |  13.68 μs |  1.54 |    0.30 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **147.2 μs** |    **763.3 μs** |  **41.84 μs** |  **1.05** |    **0.34** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   206.1 μs |  1,014.4 μs |  55.60 μs |  1.47 |    0.47 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **978.7 μs** |  **4,017.9 μs** | **220.24 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,058.5 μs |    108.6 μs |   5.95 μs |  1.12 |    0.25 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,640.4 μs** | **13,652.7 μs** | **748.35 μs** |  **1.14** |    **0.64** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,338.1 μs |    339.5 μs |  18.61 μs |  0.93 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **767.0 ns** |   **843.5 ns** |  **46.24 ns** |  **1.00** |    **0.08** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,846.4 ns | 2,853.2 ns | 156.39 ns |  2.41 |    0.22 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,346.3 ns** | **4,201.7 ns** | **230.31 ns** |  **1.02** |    **0.21** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,950.1 ns | 3,695.2 ns | 202.54 ns |  2.23 |    0.34 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.302 μs |   5.736 μs |  0.3144 μs | 26.249 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 17.706 μs | 209.222 μs | 11.4682 μs | 11.161 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.763 μs |   3.558 μs |  0.1950 μs |  8.676 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.476 μs |   3.175 μs |  0.1740 μs |  8.436 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.116 μs |  45.289 μs |  2.4825 μs | 15.468 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.125 μs |  67.194 μs |  3.6831 μs | 13.074 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 15.008 μs |  61.775 μs |  3.3861 μs | 13.185 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.104 μs |   1.645 μs |  0.0902 μs | 27.140 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.276 μs |  27.709 μs |  1.5188 μs | 15.118 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.404 μs |   1.324 μs |  0.0726 μs | 20.367 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.968 μs |  67.677 μs |  3.7096 μs | 36.108 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 35.597 μs |  30.777 μs |  1.6870 μs | 35.045 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.291 μs |   2.009 μs |  0.1101 μs |  4.297 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.711 μs |   5.594 μs |  0.3066 μs | 10.741 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,823.76 ns | 150.204 ns | 8.233 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.51 ns |   0.112 ns | 0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.53 ns |   0.635 ns | 0.035 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.58 ns |   2.310 ns | 0.127 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.33 ns |  10.690 ns | 0.586 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.189 ns | 0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    122.55 ns |  34.267 ns | 1.878 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.62 ns |   0.590 ns | 0.032 ns |  0.63 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error       | StdDev     | Allocated |
|------------------------ |-----------:|------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.351 μs |   0.6679 μs |  0.0366 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 499.137 μs |  70.1179 μs |  3.8434 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.098 μs |   0.6972 μs |  0.0382 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 245.519 μs | 277.0482 μs | 15.1859 μs |      80 B |


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