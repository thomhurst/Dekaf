---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 13:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,154.13 μs** |   **933.79 μs** |  **51.184 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,383.89 μs |   307.00 μs |  16.828 μs |  0.22 |    0.00 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,302.54 μs** |   **663.63 μs** |  **36.376 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,364.62 μs |   789.04 μs |  43.250 μs |  0.32 |    0.01 |  15.6250 |       - |  339.88 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,387.23 μs** |   **495.31 μs** |  **27.149 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,699.74 μs | 1,068.78 μs |  58.583 μs |  0.27 |    0.01 |        - |       - |   36.97 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,086.20 μs** | **1,308.71 μs** |  **71.735 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,518.39 μs | 4,669.16 μs | 255.933 μs |  0.54 |    0.02 |  15.6250 |       - |  369.57 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.99 μs** |   **137.98 μs** |   **7.563 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **42.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.67 μs |    16.40 μs |   0.899 μs |  0.47 |    0.02 |   0.4883 |       - |   12.77 KB |        0.30 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,457.50 μs** | **1,926.86 μs** | **105.617 μs** |  **1.00** |    **0.09** |  **25.3906** |       **-** |  **421.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    665.09 μs |   317.54 μs |  17.405 μs |  0.46 |    0.03 |   3.9063 |       - |     120 KB |        0.28 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    299.76 μs |   214.48 μs |  11.756 μs |     ? |       ? |   6.8359 |  5.8594 |  193.82 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,153.84 μs | 2,099.20 μs | 115.064 μs |     ? |       ? |  70.3125 | 62.5000 | 2063.35 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,532.69 μs** |   **366.00 μs** |  **20.062 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,352.87 μs |   205.73 μs |  11.277 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,531.72 μs** | **1,252.15 μs** |  **68.634 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,112.97 μs |    32.05 μs |   1.757 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,506.29 μs** |   **413.22 μs** |  **22.650 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,113.73 μs |    49.38 μs |   2.707 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,532.47 μs** |   **229.39 μs** |  **12.574 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,196.78 μs |   473.43 μs |  25.950 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.191 ms** |  **9.600 ms** | **0.5262 ms** | **3,167.156 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.644 ms | 11.847 ms | 0.6494 ms |    13.375 ms | 0.004 |  406.45 KB |        5.45 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.191 ms** |  **9.161 ms** | **0.5022 ms** | **3,164.314 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.228 ms | 20.993 ms | 1.1507 ms |    12.511 ms | 0.004 |  590.59 KB |        2.36 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.788 ms** |  **3.042 ms** | **0.1667 ms** | **3,165.721 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    12.874 ms | 38.057 ms | 2.0860 ms |    12.078 ms | 0.004 |  809.15 KB |        1.34 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.999 ms** |  **8.057 ms** | **0.4416 ms** | **3,165.749 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.640 ms | 34.283 ms | 1.8792 ms |    16.303 ms | 0.005 | 2658.25 KB |        1.12 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.129 ms** | **18.840 ms** | **1.0327 ms** | **3,156.309 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.468 ms |  8.538 ms | 0.4680 ms |     5.284 ms | 0.002 |  181.24 KB |       75.32 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.046 ms** | **15.708 ms** | **0.8610 ms** | **3,156.307 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.094 ms | 19.287 ms | 1.0572 ms |     5.856 ms | 0.002 |  185.71 KB |       44.60 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.305 ms** | **19.136 ms** | **1.0489 ms** | **3,156.714 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.347 ms | 17.484 ms | 0.9584 ms |     7.663 ms | 0.002 |  181.46 KB |       75.41 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.531 ms** | **19.649 ms** | **1.0770 ms** | **3,156.306 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.225 ms | 53.116 ms | 2.9115 ms |     5.776 ms | 0.002 |  264.95 KB |       63.39 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 16.408 μs |  4.831 μs | 0.2648 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 13.354 μs | 17.199 μs | 0.9427 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.098 μs | 11.697 μs | 0.6412 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 33.901 μs | 10.713 μs | 0.5872 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 12.505 μs | 15.253 μs | 0.8361 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 16.072 μs | 13.502 μs | 0.7401 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 16.788 μs | 20.724 μs | 1.1359 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.718 μs | 15.192 μs | 0.8327 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  9.783 μs | 20.301 μs | 1.1127 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.390 μs |  3.157 μs | 0.1731 μs |  1.356 μs |  0.33 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.175 μs |  7.320 μs | 0.4013 μs |  1.016 μs |  0.28 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.363 μs |  8.472 μs | 0.4644 μs |  1.127 μs |  0.33 |    0.10 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.571 μs |  8.882 μs | 0.4868 μs |  2.344 μs |  0.62 |    0.11 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.729 μs | 24.699 μs | 1.3538 μs |  1.027 μs |  0.42 |    0.28 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 47.573 μs | 85.283 μs | 4.6746 μs | 48.786 μs | 11.42 |    1.13 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.175 μs |  4.369 μs | 0.2395 μs |  4.206 μs |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  5.481 μs | 21.194 μs | 1.1617 μs |  4.824 μs |  1.32 |    0.25 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.668 μs |  73.751 μs |  4.0425 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   478.683 μs | 503.474 μs | 27.5971 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.777 μs |  12.653 μs |  0.6936 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,409.196 μs | 190.975 μs | 10.4680 μs |    1280 B |


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