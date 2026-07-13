---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 10:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,290.9 μs** |    **594.28 μs** |    **32.57 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,784.7 μs |  1,703.92 μs |    93.40 μs |  0.28 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,440.0 μs** |    **294.09 μs** |    **16.12 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,693.1 μs |  2,395.64 μs |   131.31 μs |  0.50 |    0.02 |  15.6250 |       - |  346458 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,202.2 μs** |    **333.45 μs** |    **18.28 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,946.8 μs |    649.69 μs |    35.61 μs |  0.48 |    0.01 |        - |       - |   36046 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,479.6 μs** |  **3,000.64 μs** |   **164.48 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,660.2 μs | 12,341.55 μs |   676.48 μs |  0.93 |    0.05 |        - |       - |  367100 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.1 μs** |      **3.97 μs** |     **0.22 μs** |  **1.00** |    **0.00** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    141.8 μs |     58.66 μs |     3.22 μs |  1.08 |    0.02 |        - |       - |    4162 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,296.8 μs** |    **309.33 μs** |    **16.96 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,469.3 μs |  1,920.33 μs |   105.26 μs |  1.13 |    0.07 |        - |       - |   42052 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,083.9 μs** |    **118.17 μs** |     **6.48 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125511 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    938.7 μs |    433.04 μs |    23.74 μs |  0.87 |    0.02 |        - |       - |    7281 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,958.1 μs** | **22,943.92 μs** | **1,257.63 μs** |  **1.01** |    **0.16** |  **74.2188** |       **-** | **1255605 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,606.6 μs | 13,201.28 μs |   723.61 μs |  0.87 |    0.12 |        - |       - |   88912 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,485.1 μs** |     **49.37 μs** |     **2.71 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,494.6 μs |     48.17 μs |     2.64 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,479.7 μs** |    **129.98 μs** |     **7.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,495.0 μs |    119.50 μs |     6.55 μs |  0.46 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,486.7 μs** |     **85.06 μs** |     **4.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,499.6 μs |    151.78 μs |     8.32 μs |  0.46 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,486.3 μs** |     **99.43 μs** |     **5.45 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,499.8 μs |     37.29 μs |     2.04 μs |  0.46 |    0.00 |        - |       - |     801 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **119.9 μs** |    **502.1 μs** |  **27.52 μs** |   **120.5 μs** |  **1.04** |    **0.30** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   198.5 μs |    119.5 μs |   6.55 μs |   196.5 μs |  1.72 |    0.36 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **181.0 μs** |  **1,112.2 μs** |  **60.97 μs** |   **183.8 μs** |  **1.09** |    **0.48** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   201.9 μs |    758.2 μs |  41.56 μs |   179.3 μs |  1.21 |    0.44 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,127.3 μs** |  **5,296.9 μs** | **290.34 μs** | **1,147.2 μs** |  **1.05** |    **0.34** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,311.2 μs |  3,575.2 μs | 195.97 μs | 1,200.2 μs |  1.22 |    0.33 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,411.2 μs** | **11,668.5 μs** | **639.59 μs** | **1,045.9 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,393.0 μs |    134.7 μs |   7.38 μs | 1,395.2 μs |  1.11 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **915.2 ns** | **1,632.3 ns** |  **89.47 ns** |  **1.01** |    **0.12** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,089.2 ns | 3,103.1 ns | 170.09 ns |  2.30 |    0.25 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,490.4 ns** | **1,762.7 ns** |  **96.62 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,202.5 ns | 1,861.9 ns | 102.06 ns |  2.15 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.033 μs |   4.323 μs | 0.2370 μs | 33.002 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.541 μs |  23.927 μs | 1.3115 μs | 10.897 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.109 μs |   3.311 μs | 0.1815 μs |  8.082 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.841 μs |   5.145 μs | 0.2820 μs |  7.771 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.939 μs |   3.706 μs | 0.2031 μs | 10.875 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.800 μs |  49.866 μs | 2.7333 μs | 12.308 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.117 μs |   3.703 μs | 0.2030 μs | 12.157 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.897 μs |   5.678 μs | 0.3113 μs | 32.803 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.732 μs | 143.999 μs | 7.8931 μs | 10.215 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.110 μs |   3.736 μs | 0.2048 μs | 20.180 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.149 μs |  48.348 μs | 2.6501 μs | 29.994 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.691 μs |  62.932 μs | 3.4495 μs | 31.476 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.067 μs |   8.389 μs | 0.4598 μs |  5.237 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.906 μs |   6.631 μs | 0.3635 μs | 10.775 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,608.46 ns | 91.372 ns | 5.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.05 ns |  9.189 ns | 0.504 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     22.30 ns |  1.232 ns | 0.068 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.16 ns |  1.486 ns | 0.081 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.06 ns |  9.933 ns | 0.544 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.25 ns |  4.854 ns | 0.266 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.74 ns | 12.989 ns | 0.712 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     81.98 ns | 19.979 ns | 1.095 ns |  0.78 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.803 μs |  11.317 μs | 0.6203 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.016 μs | 133.850 μs | 7.3368 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.791 μs |  12.725 μs | 0.6975 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 213.793 μs |  35.665 μs | 1.9549 μs |      80 B |


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