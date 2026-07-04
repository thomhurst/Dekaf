---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 07:13 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,182.58 μs** |   **545.06 μs** |  **29.877 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,346.11 μs | 1,056.73 μs |  57.923 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,408.58 μs** |   **324.13 μs** |  **17.767 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,270.10 μs |   221.68 μs |  12.151 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.48 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,162.44 μs** |   **482.94 μs** |  **26.471 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,326.84 μs | 2,320.66 μs | 127.203 μs |  0.22 |    0.02 |        - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,766.56 μs** | **6,051.32 μs** | **331.693 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,917.24 μs | 1,050.41 μs |  57.577 μs |  0.54 |    0.01 |  15.6250 |       - |  362.08 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **149.12 μs** |   **109.11 μs** |   **5.981 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     54.87 μs |    53.99 μs |   2.959 μs |  0.37 |    0.02 |   0.2441 |       - |    8.16 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,390.32 μs** | **2,759.75 μs** | **151.271 μs** |  **1.01** |    **0.13** |  **23.4375** |       **-** |  **408.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    609.87 μs |   803.85 μs |  44.062 μs |  0.44 |    0.05 |        - |       - |   69.16 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    208.53 μs |   264.79 μs |  14.514 μs |     ? |       ? |   0.9766 |       - |  104.82 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,159.43 μs | 3,519.80 μs | 192.932 μs |     ? |       ? |   7.8125 |       - |  946.04 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,440.87 μs** |    **99.89 μs** |   **5.476 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,116.70 μs |    15.55 μs |   0.852 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,434.78 μs** |    **13.31 μs** |   **0.730 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,121.54 μs |   176.32 μs |   9.664 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,440.51 μs** |    **21.98 μs** |   **1.205 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.85 μs |    19.03 μs |   1.043 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,442.25 μs** |   **102.73 μs** |   **5.631 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,118.10 μs |    76.83 μs |   4.211 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.117 ms** | **18.393 ms** | **1.0082 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.552 ms | 83.742 ms | 4.5902 ms | 0.006 |  596.45 KB |        7.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.708 ms** |  **2.555 ms** | **0.1400 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.145 ms | 42.982 ms | 2.3560 ms | 0.005 |  775.84 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.652 ms** |  **9.519 ms** | **0.5218 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.548 ms | 61.100 ms | 3.3491 ms | 0.006 |  995.02 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.784 ms** | **16.721 ms** | **0.9165 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.479 ms |  3.465 ms | 0.1899 ms | 0.005 | 2763.09 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.702 ms** | **32.064 ms** | **1.7576 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.914 ms |  2.591 ms | 0.1420 ms | 0.002 |  183.22 KB |       76.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.930 ms** | **12.261 ms** | **0.6721 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.711 ms |  5.668 ms | 0.3107 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.164 ms** |  **8.964 ms** | **0.4913 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.832 ms |  8.552 ms | 0.4688 ms | 0.002 |   184.7 KB |       76.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.009 ms** | **11.666 ms** | **0.6394 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.952 ms |  9.301 ms | 0.5098 ms | 0.002 |   187.6 KB |       44.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.741 μs |  2.559 μs | 0.1403 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.454 μs |  2.132 μs | 0.1169 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.243 μs |  3.147 μs | 0.1725 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 29.592 μs | 89.959 μs | 4.9310 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.628 μs | 25.664 μs | 1.4067 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.321 μs |  2.832 μs | 0.1552 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.241 μs |  8.777 μs | 0.4811 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.949 μs |  6.464 μs | 0.3543 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.441 μs |  2.689 μs | 0.1474 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.324 μs |  4.749 μs | 0.2603 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.322 μs |   1.4296 μs | 0.0784 μs |  1.2810 μs |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.283 μs |   0.5473 μs | 0.0300 μs |  1.2830 μs |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.329 μs |   1.6352 μs | 0.0896 μs |  1.2825 μs |  0.31 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.711 μs |   3.9009 μs | 0.2138 μs |  2.6240 μs |  0.63 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  6.179 μs | 171.9515 μs | 9.4252 μs |  0.7420 μs |  1.44 |    1.90 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42.118 μs |   1.9047 μs | 0.1044 μs | 42.1680 μs |  9.79 |    0.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.309 μs |   3.3788 μs | 0.1852 μs |  4.2390 μs |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.181 μs |   5.0438 μs | 0.2765 μs |  4.0580 μs |  0.97 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.37 μs |   4.508 μs |  0.247 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   544.58 μs | 476.921 μs | 26.142 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.19 μs |  34.727 μs |  1.904 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,665.63 μs | 215.722 μs | 11.824 μs |    1280 B |


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