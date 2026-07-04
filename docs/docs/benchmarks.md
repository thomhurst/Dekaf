---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 20:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,215.18 μs** |   **732.283 μs** |  **40.139 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,445.60 μs | 2,900.121 μs | 158.965 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,418.67 μs** | **1,301.362 μs** |  **71.332 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,399.74 μs |   797.750 μs |  43.727 μs |  0.32 |    0.01 |  15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,130.81 μs** |   **713.255 μs** |  **39.096 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,501.99 μs | 1,462.838 μs |  80.183 μs |  0.24 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,173.05 μs** | **4,316.304 μs** | **236.591 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,752.77 μs | 4,630.557 μs | 253.816 μs |  0.51 |    0.02 |  15.6250 |       - |     362 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.99 μs** |   **112.141 μs** |   **6.147 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **41.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     61.30 μs |   201.906 μs |  11.067 μs |  0.42 |    0.07 |   0.2441 |       - |    9.71 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,446.51 μs** |   **763.211 μs** |  **41.834 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |   **406.2 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    653.67 μs | 1,066.829 μs |  58.476 μs |  0.45 |    0.04 |        - |       - |   74.38 KB |        0.18 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    212.43 μs |   491.742 μs |  26.954 μs |     ? |       ? |   0.9766 |       - |   99.21 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,918.46 μs | 1,089.148 μs |  59.700 μs |     ? |       ? |   7.8125 |       - | 1011.94 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,446.33 μs** |   **309.289 μs** |  **16.953 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,116.28 μs |    25.973 μs |   1.424 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.74 μs** |   **260.728 μs** |  **14.291 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,116.79 μs |    53.730 μs |   2.945 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,439.25 μs** |   **162.971 μs** |   **8.933 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.68 μs |    31.729 μs |   1.739 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,442.27 μs** |   **151.976 μs** |   **8.330 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.51 μs |     5.669 μs |   0.311 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.858 ms** | **22.193 ms** | **1.2165 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.085 ms | 33.061 ms | 1.8122 ms | 0.005 |  591.88 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.286 ms** | **24.006 ms** | **1.3158 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.270 ms | 16.159 ms | 0.8857 ms | 0.005 |  774.64 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.145 ms** | **14.619 ms** | **0.8013 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.866 ms | 65.622 ms | 3.5970 ms | 0.006 | 1040.42 KB |        1.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.235 ms** | **18.205 ms** | **0.9979 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.630 ms | 50.611 ms | 2.7742 ms | 0.005 | 2761.82 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.185 ms** |  **7.276 ms** | **0.3988 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.036 ms | 29.790 ms | 1.6329 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.613 ms** |  **5.469 ms** | **0.2998 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.201 ms |  9.512 ms | 0.5214 ms | 0.002 |  187.76 KB |       45.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.343 ms** |  **7.984 ms** | **0.4376 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.988 ms | 13.306 ms | 0.7294 ms | 0.002 |  220.59 KB |       91.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.680 ms** | **13.646 ms** | **0.7480 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.862 ms |  2.253 ms | 0.1235 ms | 0.002 |  188.41 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.185 μs |   5.425 μs |  0.2974 μs | 32.018 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.781 μs |   5.803 μs |  0.3181 μs | 10.661 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.250 μs |   3.846 μs |  0.2108 μs | 11.367 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.055 μs |   5.574 μs |  0.3055 μs | 33.058 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 17.726 μs | 223.124 μs | 12.2302 μs | 10.846 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.647 μs |   5.171 μs |  0.2835 μs | 20.570 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 22.100 μs |  19.420 μs |  1.0645 μs | 22.684 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.908 μs |  17.571 μs |  0.9631 μs | 20.962 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.894 μs |  26.485 μs |  1.4517 μs |  4.096 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.588 μs |  11.248 μs |  0.6165 μs | 11.257 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |    995.3 ns |   4,882.8 ns |   267.64 ns |    952.0 ns |  0.26 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,359.0 ns |   1,731.4 ns |    94.91 ns |  1,392.0 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,802.2 ns |   8,905.1 ns |   488.12 ns |  2,032.5 ns |  0.47 |    0.12 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  5,802.3 ns |  98,365.9 ns | 5,391.77 ns |  2,734.0 ns |  1.50 |    1.23 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  6,630.0 ns | 180,490.1 ns | 9,893.27 ns |  1,262.0 ns |  1.72 |    2.24 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,459.7 ns |  14,026.4 ns |   768.83 ns | 35,203.0 ns |  9.18 |    1.14 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,915.3 ns |  10,269.9 ns |   562.93 ns |  3,855.0 ns |  1.01 |    0.18 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,160.8 ns |   6,372.3 ns |   349.29 ns |  4,000.5 ns |  1.08 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.382 μs |  32.585 μs |  1.7861 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.530 μs | 285.904 μs | 15.6714 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.415 μs |   5.121 μs |  0.2807 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,567.736 μs | 376.426 μs | 20.6332 μs |    1280 B |


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