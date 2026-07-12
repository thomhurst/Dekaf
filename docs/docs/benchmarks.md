---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 19:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,248.6 μs** |    **595.40 μs** |    **32.64 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,678.2 μs |  4,823.44 μs |   264.39 μs |  0.27 |    0.04 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,390.8 μs** |    **844.65 μs** |    **46.30 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,561.4 μs |    991.69 μs |    54.36 μs |  0.35 |    0.01 |  15.6250 |       - |  341.35 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,165.9 μs** |    **244.58 μs** |    **13.41 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,774.7 μs |  1,280.20 μs |    70.17 μs |  0.29 |    0.01 |        - |       - |   38.12 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,492.1 μs** |  **6,134.54 μs** |   **336.25 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,406.9 μs | 22,045.11 μs | 1,208.37 μs |  0.75 |    0.09 |  15.6250 |       - |  435.96 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.7 μs** |     **61.69 μs** |     **3.38 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    121.1 μs |    302.43 μs |    16.58 μs |  0.89 |    0.11 |        - |       - |    4.22 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,355.3 μs** |    **498.89 μs** |    **27.35 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,122.5 μs |  2,010.33 μs |   110.19 μs |  0.83 |    0.07 |        - |       - |   43.74 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,081.6 μs** |     **41.51 μs** |     **2.28 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    816.0 μs |  1,407.90 μs |    77.17 μs |  0.75 |    0.06 |        - |       - |    8.42 KB |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,813.8 μs** | **33,167.24 μs** | **1,818.01 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1226.14 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,523.3 μs | 22,306.87 μs | 1,222.72 μs |  0.79 |    0.18 |        - |       - |   94.85 KB |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,417.6 μs** |     **92.59 μs** |     **5.08 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,370.1 μs |    622.95 μs |    34.15 μs |  0.25 |    0.01 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,428.3 μs** |     **94.37 μs** |     **5.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,328.3 μs |     99.61 μs |     5.46 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,420.9 μs** |     **72.24 μs** |     **3.96 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,181.7 μs |    258.73 μs |    14.18 μs |  0.22 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,435.5 μs** |     **48.69 μs** |     **2.67 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.0 μs |     66.79 μs |     3.66 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **146.1 μs** |    **599.61 μs** |  **32.87 μs** |   **163.2 μs** |  **1.04** |    **0.31** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   204.8 μs |    228.44 μs |  12.52 μs |   207.9 μs |  1.46 |    0.34 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **145.4 μs** |    **586.05 μs** |  **32.12 μs** |   **138.5 μs** |  **1.03** |    **0.28** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   215.4 μs |    606.31 μs |  33.23 μs |   231.7 μs |  1.53 |    0.35 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **948.9 μs** |  **3,063.37 μs** | **167.91 μs** |   **885.5 μs** |  **1.02** |    **0.21** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,146.5 μs |     65.82 μs |   3.61 μs | 1,145.5 μs |  1.23 |    0.17 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,438.1 μs** | **12,240.26 μs** | **670.93 μs** | **1,071.0 μs** |  **1.13** |    **0.60** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,388.3 μs |    405.12 μs |  22.21 μs | 1,397.1 μs |  1.09 |    0.35 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **984.3 ns** | **2,296.4 ns** | **125.87 ns** |  **1.01** |    **0.16** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,114.0 ns | 2,331.9 ns | 127.82 ns |  2.17 |    0.26 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,472.3 ns** |   **473.0 ns** |  **25.93 ns** |  **1.00** |    **0.02** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,502.6 ns | 8,592.8 ns | 471.00 ns |  2.38 |    0.28 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.212 μs |  2.2089 μs | 0.1211 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.207 μs |  0.9171 μs | 0.0503 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.519 μs |  2.1794 μs | 0.1195 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.572 μs |  2.4939 μs | 0.1367 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.512 μs |  2.6583 μs | 0.1457 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.574 μs |  2.8322 μs | 0.1552 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.084 μs |  3.6624 μs | 0.2007 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.825 μs |  1.4033 μs | 0.0769 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.974 μs |  0.5574 μs | 0.0306 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.218 μs |  2.4767 μs | 0.1358 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 35.189 μs | 31.5514 μs | 1.7294 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.436 μs | 29.3293 μs | 1.6076 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.122 μs |  1.5552 μs | 0.0852 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.633 μs |  0.9267 μs | 0.0508 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,134.32 ns | 167.428 ns | 9.177 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.77 ns |   0.012 ns | 0.001 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.66 ns |   0.918 ns | 0.050 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.05 ns |   0.467 ns | 0.026 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.16 ns |   9.193 ns | 0.504 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.83 ns |   0.086 ns | 0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.70 ns |  25.734 ns | 1.411 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.60 ns |  20.434 ns | 1.120 ns |  0.72 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.288 μs |   3.614 μs |  0.1981 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 506.200 μs | 255.642 μs | 14.0126 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.981 μs |   1.555 μs |  0.0852 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 244.939 μs | 231.546 μs | 12.6918 μs |      80 B |


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