---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 06:09 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,167.31 μs** |   **990.04 μs** |  **54.268 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,278.65 μs |   649.55 μs |  35.604 μs |  0.21 |    0.01 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,266.59 μs** |   **274.62 μs** |  **15.053 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,336.90 μs | 1,163.24 μs |  63.761 μs |  0.32 |    0.01 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,606.31 μs** |   **217.16 μs** |  **11.903 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,136.88 μs |   718.59 μs |  39.388 μs |  0.17 |    0.01 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,512.99 μs** | **1,774.76 μs** |  **97.281 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,629.69 μs | 1,611.41 μs |  88.327 μs |  0.49 |    0.01 |  15.6250 |       - |  361.74 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **123.61 μs** |    **53.86 μs** |   **2.952 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.61 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     51.19 μs |    62.98 μs |   3.452 μs |  0.41 |    0.03 |   0.2441 |       - |    9.64 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,236.96 μs** | **1,407.08 μs** |  **77.127 μs** |  **1.00** |    **0.08** |  **23.4375** |       **-** |  **426.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    612.19 μs |   495.04 μs |  27.135 μs |  0.50 |    0.03 |        - |       - |   68.47 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    184.50 μs |   386.98 μs |  21.212 μs |     ? |       ? |   0.4883 |       - |   13.97 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,108.94 μs | 6,472.67 μs | 354.789 μs |     ? |       ? |   7.8125 |       - |  1005.4 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,476.02 μs** |    **57.72 μs** |   **3.164 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,311.25 μs |   613.17 μs |  33.610 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,438.56 μs** |   **312.17 μs** |  **17.111 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,362.03 μs |   300.65 μs |  16.480 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,432.88 μs** |    **53.57 μs** |   **2.937 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,113.77 μs |   212.52 μs |  11.649 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.46 μs** |   **143.07 μs** |   **7.842 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,125.19 μs |   188.91 μs |  10.355 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.176 ms** | **14.985 ms** | **0.8214 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.528 ms | 36.063 ms | 1.9767 ms | 0.006 | 594.47 KB |        7.97 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,169.701 ms** | **88.613 ms** | **4.8572 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.194 ms |  5.096 ms | 0.2793 ms | 0.005 | 777.04 KB |        3.10 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.513 ms** |  **5.905 ms** | **0.3237 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.519 ms | 51.511 ms | 2.8235 ms | 0.006 | 996.14 KB |        1.65 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.032 ms** |  **8.982 ms** | **0.4924 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.199 ms |  2.632 ms | 0.1443 ms | 0.005 | 2760.8 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.035 ms** | **22.210 ms** | **1.2174 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.025 ms | 11.151 ms | 0.6112 ms | 0.002 | 186.77 KB |       77.62 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.823 ms** |  **9.761 ms** | **0.5351 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.818 ms |  3.462 ms | 0.1898 ms | 0.002 | 186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.518 ms** |  **3.257 ms** | **0.1785 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.324 ms |  9.067 ms | 0.4970 ms | 0.002 | 256.59 KB |      106.63 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.919 ms** | **14.623 ms** | **0.8016 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.550 ms |  6.473 ms | 0.3548 ms | 0.002 | 186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 17.973 μs | 101.149 μs | 5.5443 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.182 μs |  28.772 μs | 1.5771 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.293 μs |   4.072 μs | 0.2232 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.744 μs |   1.925 μs | 0.1055 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.084 μs |   2.112 μs | 0.1158 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.207 μs |   2.279 μs | 0.1249 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.425 μs |   4.115 μs | 0.2256 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.395 μs |   4.483 μs | 0.2457 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.736 μs |   6.661 μs | 0.3651 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.281 μs |   2.949 μs | 0.1617 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,798.2 ns | 4,615.0 ns | 252.96 ns |  0.42 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,415.0 ns | 1,820.4 ns |  99.78 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,322.7 ns | 1,430.7 ns |  78.42 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,656.2 ns | 2,009.6 ns | 110.15 ns |  0.63 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    782.0 ns | 1,796.8 ns |  98.49 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,156.3 ns | 7,255.9 ns | 397.72 ns |  9.69 |    0.57 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,260.7 ns | 5,222.0 ns | 286.23 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,000.0 ns | 3,112.5 ns | 170.61 ns |  0.94 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Median      | Allocated |
|------------------------ |------------:|----------:|----------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    16.48 μs |  16.31 μs |  0.894 μs |    16.13 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   524.75 μs | 367.85 μs | 20.163 μs |   528.92 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    18.34 μs | 213.18 μs | 11.685 μs |    13.12 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,688.19 μs | 452.67 μs | 24.812 μs | 1,694.30 μs |    1280 B |


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