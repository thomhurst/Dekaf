---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 15:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,137.28 μs** |   **192.51 μs** |  **10.552 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,403.84 μs | 2,222.19 μs | 121.806 μs |  0.23 |    0.02 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,406.69 μs** |   **954.14 μs** |  **52.300 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,327.37 μs |   431.31 μs |  23.642 μs |  0.31 |    0.00 |  15.6250 |       - |  339.57 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,194.45 μs** | **1,427.89 μs** |  **78.268 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,294.89 μs | 2,117.25 μs | 116.054 μs |  0.21 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,812.92 μs** | **4,232.02 μs** | **231.971 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,649.19 μs | 1,705.53 μs |  93.486 μs |  0.52 |    0.01 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.03 μs** |    **85.37 μs** |   **4.679 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     61.32 μs |    76.32 μs |   4.183 μs |  0.42 |    0.03 |        - |       - |    7.43 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.56 μs** | **1,498.02 μs** |  **82.111 μs** |  **1.00** |    **0.07** |  **23.4375** |       **-** |  **422.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    651.80 μs | 1,123.81 μs |  61.600 μs |  0.46 |    0.04 |        - |       - |   72.75 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    191.04 μs |   448.31 μs |  24.573 μs |     ? |       ? |   0.4883 |       - |   15.27 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,893.47 μs | 2,473.43 μs | 135.577 μs |     ? |       ? |   7.8125 |       - | 1020.27 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,432.50 μs** |   **161.28 μs** |   **8.840 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,394.52 μs |   286.36 μs |  15.696 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,424.17 μs** |   **111.87 μs** |   **6.132 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,386.92 μs |   466.85 μs |  25.590 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,434.43 μs** |    **44.05 μs** |   **2.414 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,357.62 μs |   277.65 μs |  15.219 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,446.68 μs** |    **68.80 μs** |   **3.771 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,357.76 μs |   227.58 μs |  12.474 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.885 ms** | **10.236 ms** | **0.5610 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.443 ms | 23.401 ms | 1.2827 ms | 0.005 |  595.38 KB |        7.98 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.037 ms** | **20.592 ms** | **1.1287 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.892 ms | 24.427 ms | 1.3389 ms | 0.004 |  778.31 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.865 ms** | **10.237 ms** | **0.5611 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.690 ms | 41.402 ms | 2.2694 ms | 0.005 |  999.13 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.282 ms** | **10.586 ms** | **0.5803 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.945 ms | 19.842 ms | 1.0876 ms | 0.005 | 2810.24 KB |        1.19 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.065 ms** | **48.853 ms** | **2.6778 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.541 ms |  8.898 ms | 0.4877 ms | 0.002 |  184.44 KB |       76.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.585 ms** | **10.720 ms** | **0.5876 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.326 ms |  3.396 ms | 0.1862 ms | 0.002 |  186.45 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.828 ms** | **17.800 ms** | **0.9757 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.086 ms |  9.617 ms | 0.5271 ms | 0.002 |  202.65 KB |       84.22 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.238 ms** | **38.609 ms** | **2.1163 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.724 ms |  2.901 ms | 0.1590 ms | 0.002 |  232.12 KB |       55.53 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.327 μs | 41.2166 μs | 2.2592 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.218 μs |  2.9866 μs | 0.1637 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.851 μs | 21.3226 μs | 1.1688 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.850 μs |  4.1317 μs | 0.2265 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.937 μs |  0.9654 μs | 0.0529 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.509 μs |  2.0332 μs | 0.1114 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.724 μs | 29.6266 μs | 1.6239 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.184 μs | 49.4181 μs | 2.7088 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.532 μs |  0.5574 μs | 0.0306 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.088 μs | 16.1512 μs | 0.8853 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,778.2 ns |    182.7 ns |  10.02 ns |  0.44 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,368.7 ns |  1,474.6 ns |  80.83 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,312.7 ns |    472.3 ns |  25.89 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,615.3 ns |  1,429.6 ns |  78.36 ns |  0.65 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    741.2 ns |    489.6 ns |  26.84 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,564.0 ns | 12,659.0 ns | 693.88 ns |  9.84 |    0.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,024.5 ns |  2,908.5 ns | 159.43 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,957.7 ns |  2,111.1 ns | 115.72 ns |  0.98 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.181 μs |   4.142 μs |  0.2270 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.219 μs | 199.226 μs | 10.9202 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.482 μs |   4.699 μs |  0.2575 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,672.562 μs | 326.213 μs | 17.8808 μs |    1280 B |


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