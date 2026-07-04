---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 09:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,112.63 μs** |   **529.28 μs** |  **29.011 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,376.77 μs | 1,862.95 μs | 102.114 μs |  0.23 |    0.01 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,353.67 μs** |   **269.81 μs** |  **14.789 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** | **1063.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,371.20 μs |   582.58 μs |  31.933 μs |  0.32 |    0.00 |  15.6250 |       - |  339.56 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,255.89 μs** |   **326.22 μs** |  **17.881 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,286.72 μs | 2,208.00 μs | 121.028 μs |  0.21 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,596.82 μs** | **2,633.09 μs** | **144.328 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,326.69 μs | 1,690.53 μs |  92.664 μs |  0.50 |    0.01 |  15.6250 |       - |   361.8 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.94 μs** |    **90.89 μs** |   **4.982 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **40.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.16 μs |    48.48 μs |   2.657 μs |  0.47 |    0.02 |        - |       - |    8.67 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,431.04 μs** |   **107.72 μs** |   **5.905 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **410.94 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    606.41 μs |   300.62 μs |  16.478 μs |  0.42 |    0.01 |        - |       - |   97.65 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    199.15 μs |   127.24 μs |   6.974 μs |     ? |       ? |   0.9766 |       - |  105.03 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,232.41 μs** |   **338.70 μs** |  **18.565 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1225.12 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,084.42 μs | 5,163.34 μs | 283.020 μs |  0.93 |    0.11 |   7.8125 |       - |  982.79 KB |        0.80 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,421.01 μs** |    **28.85 μs** |   **1.581 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,383.89 μs |   278.57 μs |  15.269 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,437.01 μs** |    **44.79 μs** |   **2.455 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,367.55 μs | 1,061.01 μs |  58.157 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.13 μs** |    **58.71 μs** |   **3.218 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,363.00 μs |   361.34 μs |  19.806 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,419.68 μs** |    **78.56 μs** |   **4.306 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,355.92 μs |   397.63 μs |  21.795 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.386 ms** | **21.128 ms** | **1.1581 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.584 ms | 33.394 ms | 1.8304 ms | 0.005 |  599.59 KB |        8.04 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.634 ms** |  **4.309 ms** | **0.2362 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.156 ms | 54.976 ms | 3.0134 ms | 0.005 |  776.52 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.488 ms** | **26.736 ms** | **1.4655 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.955 ms |  7.585 ms | 0.4157 ms | 0.005 |  995.44 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.508 ms** | **37.403 ms** | **2.0502 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.057 ms | 21.190 ms | 1.1615 ms | 0.005 | 2761.02 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.693 ms** | **16.669 ms** | **0.9137 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.274 ms | 24.271 ms | 1.3304 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.150 ms** | **27.723 ms** | **1.5196 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.194 ms |  3.731 ms | 0.2045 ms | 0.002 |  197.59 KB |       47.45 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.887 ms** |  **6.449 ms** | **0.3535 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.025 ms | 12.466 ms | 0.6833 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.017 ms** | **32.137 ms** | **1.7615 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.562 ms | 20.788 ms | 1.1395 ms | 0.002 |  259.02 KB |       61.97 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.577 μs | 209.8063 μs | 11.5002 μs | 15.048 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.371 μs |   2.2811 μs |  0.1250 μs |  9.368 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.357 μs |   1.4859 μs |  0.0814 μs | 10.320 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.759 μs | 250.2388 μs | 13.7164 μs | 27.021 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.084 μs |   0.3725 μs |  0.0204 μs |  9.077 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.557 μs |   1.5852 μs |  0.0869 μs | 19.507 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.574 μs |   4.3007 μs |  0.2357 μs | 17.684 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.146 μs |  10.0545 μs |  0.5511 μs | 20.133 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.451 μs |   1.4273 μs |  0.0782 μs |  4.427 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.218 μs |  13.1736 μs |  0.7221 μs | 11.252 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,195.0 ns |   1,296.1 ns |     71.04 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,164.7 ns |   2,187.0 ns |    119.88 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,396.3 ns |   1,281.4 ns |     70.24 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,578.3 ns |     459.1 ns |     25.17 ns |  0.64 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    735.3 ns |   1,004.8 ns |     55.08 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 53,637.0 ns | 213,476.0 ns | 11,701.34 ns | 13.42 |    2.54 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,998.0 ns |     657.8 ns |     36.06 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,953.7 ns |   4,023.3 ns |    220.53 ns |  0.99 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev     | Median      | Allocated |
|------------------------ |------------:|-------------:|-----------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.36 μs |     6.455 μs |   0.354 μs |    11.30 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   834.54 μs | 3,207.637 μs | 175.821 μs |   918.67 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    18.27 μs |   214.283 μs |  11.746 μs |    12.45 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,707.56 μs |   410.925 μs |  22.524 μs | 1,703.42 μs |    1280 B |


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