---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 01:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,406.56 μs** |  **1,141.60 μs** |    **62.575 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,514.28 μs |    269.90 μs |    14.794 μs |  0.24 |    0.00 |        - |       - |    34.7 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,665.98 μs** |    **332.53 μs** |    **18.227 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,386.00 μs |  1,158.07 μs |    63.478 μs |  0.31 |    0.01 |  15.6250 |       - |  339.67 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,206.00 μs** |    **814.88 μs** |    **44.666 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,418.97 μs |  2,171.11 μs |   119.006 μs |  0.23 |    0.02 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,764.79 μs** |  **2,575.14 μs** |   **141.152 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,186.69 μs | 11,643.64 μs |   638.227 μs |  0.56 |    0.04 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.95 μs** |     **14.24 μs** |     **0.780 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     60.14 μs |     54.86 μs |     3.007 μs |  0.43 |    0.02 |        - |       - |    8.98 KB |        0.27 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,377.75 μs** |    **296.13 μs** |    **16.232 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    614.05 μs |    350.25 μs |    19.198 μs |  0.45 |    0.01 |        - |       - |   89.31 KB |        0.27 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **643.56 μs** |  **7,958.10 μs** |   **436.210 μs** |  **1.49** |    **1.46** |   **7.3242** |       **-** |  **122.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    216.80 μs |    276.12 μs |    15.135 μs |  0.50 |    0.35 |   0.9766 |       - |  101.58 KB |        0.83 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,236.24 μs** | **31,365.11 μs** | **1,719.227 μs** |  **1.02** |    **0.22** |  **74.2188** |       **-** | **1226.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,130.69 μs |  2,229.05 μs |   122.182 μs |  0.21 |    0.04 |   7.8125 |       - |  1002.9 KB |        0.82 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,659.34 μs** |    **527.56 μs** |    **28.918 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,125.64 μs |     41.01 μs |     2.248 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,558.44 μs** |    **267.17 μs** |    **14.644 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,126.59 μs |     43.95 μs |     2.409 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,594.67 μs** |    **346.02 μs** |    **18.966 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,123.70 μs |     43.67 μs |     2.394 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,593.62 μs** |  **1,630.05 μs** |    **89.349 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,126.36 μs |     27.94 μs |     1.532 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.260 ms** | **18.301 ms** | **1.0031 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.944 ms | 28.557 ms | 1.5653 ms | 0.005 | 600.74 KB |        8.05 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.064 ms** | **12.410 ms** | **0.6802 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.414 ms | 25.844 ms | 1.4166 ms | 0.005 | 785.44 KB |        3.14 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.133 ms** |  **9.346 ms** | **0.5123 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.576 ms | 57.595 ms | 3.1570 ms | 0.006 | 1084.4 KB |        1.80 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.808 ms** | **17.278 ms** | **0.9470 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    19.621 ms | 55.331 ms | 3.0329 ms | 0.006 | 2770.6 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.102 ms** | **25.259 ms** | **1.3845 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.480 ms | 20.328 ms | 1.1143 ms | 0.002 | 184.56 KB |       76.70 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.406 ms** | **18.088 ms** | **0.9915 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.718 ms | 18.118 ms | 0.9931 ms | 0.002 | 186.51 KB |       44.79 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.409 ms** | **28.103 ms** | **1.5404 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.604 ms | 17.175 ms | 0.9414 ms | 0.002 | 195.25 KB |       81.14 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.053 ms** | **42.622 ms** | **2.3363 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.889 ms |  6.129 ms | 0.3359 ms | 0.002 | 187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.320 μs |  1.583 μs | 0.0868 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.467 μs |  4.846 μs | 0.2656 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.155 μs |  1.657 μs | 0.0908 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.758 μs |  3.780 μs | 0.2072 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.180 μs |  3.662 μs | 0.2007 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.078 μs |  9.357 μs | 0.5129 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.512 μs | 33.899 μs | 1.8581 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.378 μs | 31.496 μs | 1.7264 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.460 μs |  9.109 μs | 0.4993 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.683 μs |  7.767 μs | 0.4257 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,354.7 ns |    680.3 ns |  37.29 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,450.2 ns |  5,809.1 ns | 318.42 ns |  0.33 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,158.0 ns |  6,181.8 ns | 338.85 ns |  0.26 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,313.7 ns |  6,219.8 ns | 340.93 ns |  0.52 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    672.5 ns |  1,644.7 ns |  90.15 ns |  0.15 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,008.7 ns | 11,308.5 ns | 619.86 ns |  7.90 |    0.49 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,444.0 ns |  5,713.1 ns | 313.16 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,110.8 ns | 12,870.6 ns | 705.48 ns |  0.93 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.65 μs |  10.533 μs |  0.577 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   563.56 μs | 847.099 μs | 46.432 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.22 μs |   2.554 μs |  0.140 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,594.71 μs | 264.368 μs | 14.491 μs |    1280 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to baseline (Confluent.Kafka)
  - `< 1.0` = Dekaf is faster
  - `> 1.0` = Confluent is faster
  - `1.0` = Same performance
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*