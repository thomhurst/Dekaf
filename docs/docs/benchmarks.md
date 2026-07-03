---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 13:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,110.93 μs** |   **432.19 μs** |  **23.690 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,242.03 μs | 1,777.46 μs |  97.428 μs |  0.20 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,246.85 μs** | **1,926.70 μs** | **105.609 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,240.37 μs |   264.51 μs |  14.499 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.89 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,627.04 μs** |    **65.11 μs** |   **3.569 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,172.26 μs | 1,188.86 μs |  65.165 μs |  0.18 |    0.01 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,722.49 μs** | **5,072.01 μs** | **278.014 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,966.77 μs | 2,851.46 μs | 156.298 μs |  0.51 |    0.02 |  15.6250 |       - |   369.5 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.01 μs** |   **136.97 μs** |   **7.508 μs** |  **1.00** |    **0.07** |   **2.1973** |       **-** |    **37.7 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.03 μs |    62.64 μs |   3.433 μs |  0.43 |    0.03 |   0.7324 |  0.4883 |   18.78 KB |        0.50 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,367.40 μs** | **3,101.21 μs** | **169.988 μs** |  **1.01** |    **0.16** |  **23.4375** |       **-** |  **413.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    655.75 μs |   482.01 μs |  26.421 μs |  0.48 |    0.06 |   3.9063 |       - |   99.89 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    344.88 μs |   458.99 μs |  25.159 μs |     ? |       ? |   5.8594 |  4.8828 |  188.14 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,263.96 μs | 4,019.14 μs | 220.303 μs |     ? |       ? |  62.5000 | 54.6875 | 1922.72 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.71 μs** |   **172.61 μs** |   **9.461 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,313.37 μs |   666.08 μs |  36.510 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,403.45 μs** |   **112.81 μs** |   **6.183 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,348.73 μs |   243.66 μs |  13.356 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,405.97 μs** |    **52.19 μs** |   **2.861 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,364.42 μs |   195.37 μs |  10.709 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.51 μs** |    **66.88 μs** |   **3.666 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,107.13 μs |    54.31 μs |   2.977 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.214 ms** |  **3.398 ms** | **0.1862 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.628 ms | 46.068 ms | 2.5252 ms | 0.005 |  411.94 KB |        5.52 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.159 ms** | **26.226 ms** | **1.4375 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.455 ms | 11.581 ms | 0.6348 ms | 0.005 |  589.59 KB |        2.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.810 ms** | **18.822 ms** | **1.0317 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.260 ms | 14.886 ms | 0.8160 ms | 0.005 |  811.61 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.581 ms** | **20.820 ms** | **1.1412 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.915 ms | 17.739 ms | 0.9723 ms | 0.005 | 2649.06 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.263 ms** | **21.125 ms** | **1.1579 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.913 ms |  7.321 ms | 0.4013 ms | 0.002 |   181.3 KB |       75.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.822 ms** | **51.972 ms** | **2.8488 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.745 ms | 41.734 ms | 2.2876 ms | 0.002 |  184.63 KB |       44.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.295 ms** | **23.544 ms** | **1.2905 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.078 ms | 12.663 ms | 0.6941 ms | 0.002 |  181.43 KB |       75.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.900 ms** | **31.714 ms** | **1.7384 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.600 ms |  4.341 ms | 0.2379 ms | 0.002 |   183.8 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.803 μs | 104.121 μs | 5.7072 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.312 μs |   1.441 μs | 0.0790 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.107 μs |   4.670 μs | 0.2560 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 29.376 μs |  87.898 μs | 4.8180 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.927 μs |   1.196 μs | 0.0656 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.317 μs |  64.639 μs | 3.5431 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 20.191 μs |  26.363 μs | 1.4451 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.611 μs |   4.706 μs | 0.2579 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.426 μs |   2.920 μs | 0.1600 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,717.0 ns | 2,668.02 ns | 146.24 ns |  0.40 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,440.0 ns | 4,270.24 ns | 234.07 ns |  0.34 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,362.0 ns |     0.00 ns |   0.00 ns |  0.32 |    0.00 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,696.0 ns | 3,518.72 ns | 192.87 ns |  0.63 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    760.7 ns | 1,429.58 ns |  78.36 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,837.8 ns | 5,125.59 ns | 280.95 ns |  9.28 |    0.15 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,293.2 ns | 1,387.04 ns |  76.03 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,003.7 ns | 2,744.65 ns | 150.44 ns |  0.93 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.201 μs |   5.767 μs |  0.3161 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   532.376 μs | 181.880 μs |  9.9695 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.212 μs |  22.118 μs |  1.2123 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,675.527 μs | 280.161 μs | 15.3566 μs |    1280 B |


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