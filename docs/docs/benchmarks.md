---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 13:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,240.60 μs** |    **584.153 μs** |    **32.019 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,770.26 μs |  2,590.838 μs |   142.013 μs |  0.28 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,351.03 μs** |  **1,469.335 μs** |    **80.539 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,446.10 μs |  2,415.816 μs |   132.419 μs |  0.33 |    0.02 |  15.6250 |       - |  341.19 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,201.20 μs** |  **1,489.948 μs** |    **81.669 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,904.81 μs |  5,630.189 μs |   308.610 μs |  0.31 |    0.04 |        - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,606.77 μs** |  **2,411.821 μs** |   **132.200 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,250.72 μs |  6,590.103 μs |   361.226 μs |  0.65 |    0.03 |  15.6250 |       - |  390.12 KB |        0.20 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.67 μs** |      **4.625 μs** |     **0.253 μs** |  **1.00** |    **0.00** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     89.09 μs |    620.825 μs |    34.030 μs |  0.67 |    0.22 |        - |       - |    9.29 KB |        0.28 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,350.82 μs** |    **146.262 μs** |     **8.017 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    898.60 μs |  1,619.818 μs |    88.788 μs |  0.67 |    0.06 |        - |       - |   61.65 KB |        0.18 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,080.57 μs** |    **123.064 μs** |     **6.746 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    274.46 μs |    625.708 μs |    34.297 μs |  0.25 |    0.03 |   0.9766 |       - |  102.78 KB |        0.84 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,943.98 μs** | **25,334.559 μs** | **1,388.673 μs** |  **1.01** |    **0.18** |  **74.2188** |       **-** | **1226.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,080.73 μs |  7,865.734 μs |   431.147 μs |  0.31 |    0.06 |        - |       - |  979.83 KB |        0.80 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,413.17 μs** |     **44.560 μs** |     **2.442 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,369.78 μs |    499.523 μs |    27.381 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,550.87 μs** |  **3,887.921 μs** |   **213.110 μs** |  **1.00** |    **0.05** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,405.54 μs |    542.402 μs |    29.731 μs |  0.25 |    0.01 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.78 μs** |     **64.897 μs** |     **3.557 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,388.43 μs |    387.077 μs |    21.217 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,433.11 μs** |    **146.254 μs** |     **8.017 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,364.85 μs |    927.812 μs |    50.856 μs |  0.25 |    0.01 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **111.4 μs** |    **231.8 μs** |  **12.70 μs** |  **1.01** |    **0.14** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   175.1 μs |    672.7 μs |  36.87 μs |  1.59 |    0.33 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **161.2 μs** |    **706.3 μs** |  **38.71 μs** |  **1.05** |    **0.34** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   181.3 μs |    694.7 μs |  38.08 μs |  1.18 |    0.36 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **928.2 μs** |  **2,383.3 μs** | **130.64 μs** |  **1.01** |    **0.17** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,150.6 μs |    281.8 μs |  15.45 μs |  1.25 |    0.14 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,634.4 μs** | **10,231.1 μs** | **560.80 μs** |  **1.08** |    **0.47** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,363.8 μs |    256.9 μs |  14.08 μs |  0.90 |    0.27 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **864.7 ns** | **435.3 ns** | **23.86 ns** |  **1.00** |    **0.03** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 3,265.0 ns | 778.8 ns | 42.69 ns |  3.78 |    0.10 |      - |     452 B |        0.70 |
|                      |             |            |          |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,452.6 ns** | **874.3 ns** | **47.92 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 4,474.3 ns | 862.9 ns | 47.30 ns |  3.08 |    0.09 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 24.968 μs |  14.453 μs |  0.7922 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.331 μs |  15.534 μs |  0.8515 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.373 μs |  18.491 μs |  1.0136 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.383 μs |  17.701 μs |  0.9702 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.406 μs |  13.356 μs |  0.7321 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 10.934 μs |  19.518 μs |  1.0699 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.433 μs |  36.187 μs |  1.9835 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.477 μs |  54.632 μs |  2.9946 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.916 μs |  44.502 μs |  2.4393 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 17.072 μs |  97.510 μs |  5.3449 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 54.989 μs | 192.070 μs | 10.5280 μs |    2464 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 57.428 μs | 324.912 μs | 17.8095 μs |    2504 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.384 μs |  23.456 μs |  1.2857 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.476 μs |  18.075 μs |  0.9907 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,619.889 ns | 112.3124 ns | 6.1562 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     11.597 ns |   1.8394 ns | 0.1008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     14.799 ns |   1.3087 ns | 0.0717 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     29.828 ns |  11.6153 ns | 0.6367 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.703 ns |   5.9080 ns | 0.3238 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      7.818 ns |   0.9613 ns | 0.0527 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    102.557 ns |  19.4753 ns | 1.0675 ns |  1.00 |    0.01 | 0.0106 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     73.474 ns |   3.6309 ns | 0.1990 ns |  0.72 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   9.636 μs |  48.213 μs |  2.6427 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 435.420 μs |  35.583 μs |  1.9504 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  12.221 μs |  13.156 μs |  0.7211 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 216.576 μs | 486.204 μs | 26.6505 μs |      80 B |


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