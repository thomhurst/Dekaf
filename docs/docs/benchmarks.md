---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-05-18 22:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,781.05 μs** |    **70.861 μs** |    **46.870 μs** | **5,794.96 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.56 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,246.45 μs |    26.809 μs |    17.732 μs | 1,249.02 μs |  0.22 |    0.00 |   1.9531 |       - |   32.03 KB |        0.30 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,106.59 μs** |    **61.123 μs** |    **36.373 μs** | **7,119.66 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,179.99 μs |     4.524 μs |     2.692 μs | 2,180.42 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  309.66 KB |        0.29 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,333.04 μs** |    **20.163 μs** |    **13.337 μs** | **6,328.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **194.09 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,090.98 μs |     7.205 μs |     3.769 μs | 1,092.13 μs |  0.17 |    0.00 |        - |       - |   34.43 KB |        0.18 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,629.03 μs** |   **178.837 μs** |    **93.535 μs** | **8,636.93 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,743.52 μs | 1,426.982 μs |   746.339 μs | 5,780.97 μs |  0.67 |    0.08 |  15.6250 |       - |   344.2 KB |        0.18 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    45.04 μs |     5.090 μs |     3.367 μs |    44.13 μs |     ? |       ? |   0.4883 |  0.2441 |   15.39 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   469.06 μs |    47.943 μs |    28.530 μs |   466.64 μs |     ? |       ? |   5.8594 |  3.9063 |  135.83 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   680.64 μs |   485.506 μs |   321.132 μs |   833.13 μs |     ? |       ? |   0.4883 |       - |   16.17 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 8,089.82 μs | 4,336.597 μs | 2,868.391 μs | 8,949.98 μs |     ? |       ? |   7.8125 |       - |  166.76 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,332.37 μs** |     **4.660 μs** |     **2.773 μs** | **5,332.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,320.84 μs |    33.321 μs |    22.040 μs | 1,318.10 μs |  0.25 |    0.00 |        - |       - |    1.29 KB |        1.09 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,331.79 μs** |     **5.136 μs** |     **3.057 μs** | **5,331.22 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,224.36 μs |    99.454 μs |    52.016 μs | 1,220.00 μs |  0.23 |    0.01 |        - |       - |    1.32 KB |        1.12 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,331.79 μs** |     **5.566 μs** |     **2.911 μs** | **5,331.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,271.35 μs |   212.591 μs |   140.616 μs | 1,317.56 μs |  0.24 |    0.03 |        - |       - |    1.29 KB |        0.63 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,330.75 μs** |     **3.747 μs** |     **1.960 μs** | **5,330.23 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,324.80 μs |    72.723 μs |    43.276 μs | 1,310.99 μs |  0.25 |    0.01 |        - |       - |    1.29 KB |        0.63 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-TGZGTC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-TGZGTC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-TGZGTC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-TGZGTC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Gen0      | Gen1      | Gen2      | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|----------:|----------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.079 ms** |  **1.6268 ms** | **0.2518 ms** | **1.000** |         **-** |         **-** |         **-** |    **76592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.048 ms | 12.8330 ms | 3.3327 ms | 0.004 |         - |         - |         - |  9016184 B |      117.72 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163.388 ms** |  **1.4247 ms** | **0.3700 ms** | **1.000** |         **-** |         **-** |         **-** |   **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.406 ms |  7.2036 ms | 1.8707 ms | 0.004 | 1000.0000 | 1000.0000 | 1000.0000 |  9446640 B |       36.77 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,162.811 ms** |  **0.9520 ms** | **0.1473 ms** | **1.000** |         **-** |         **-** |         **-** |   **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    11.945 ms |  5.8232 ms | 0.9012 ms | 0.004 | 1000.0000 | 1000.0000 | 1000.0000 |  9667960 B |       15.67 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,162.928 ms** |  **2.7261 ms** | **0.7080 ms** | **1.000** |         **-** |         **-** |         **-** |  **2424896 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.248 ms |  8.9140 ms | 1.3794 ms | 0.004 | 1000.0000 | 1000.0000 | 1000.0000 | 14437136 B |        5.95 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.734 ms** |  **2.9739 ms** | **0.7723 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     4.385 ms |  0.8646 ms | 0.2245 ms | 0.001 |         - |         - |         - |   368496 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,153.310 ms** | **11.9622 ms** | **1.8512 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.473 ms |  0.6267 ms | 0.1627 ms | 0.001 |         - |         - |         - |   828688 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,153.934 ms** |  **8.7197 ms** | **1.3494 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     5.055 ms |  3.2229 ms | 0.4987 ms | 0.002 |         - |         - |         - |   825744 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,154.050 ms** |  **2.8082 ms** | **0.7293 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.127 ms |  1.3457 ms | 0.3495 ms | 0.002 |         - |         - |         - |  4498536 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 25.649 μs | 14.2082 μs |  8.4551 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 12.957 μs |  1.0959 μs |  0.7249 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 13.693 μs |  1.1255 μs |  0.7445 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 34.205 μs |  3.3185 μs |  1.7357 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 19.572 μs | 12.1158 μs |  7.2099 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 32.599 μs | 19.9332 μs | 11.8619 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.975 μs |  1.6641 μs |  1.1007 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;           |  4.088 μs |  0.1649 μs |  0.0981 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  9.290 μs |  0.6044 μs |  0.3597 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,366.8 ns | 179.76 ns | 106.97 ns |  0.36 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,588.8 ns |  66.16 ns |  39.37 ns |  0.42 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,339.0 ns | 146.93 ns |  87.44 ns |  0.36 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,289.2 ns | 396.11 ns | 262.01 ns |  0.61 |    0.07 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    497.7 ns | 205.66 ns | 136.03 ns |  0.13 |    0.03 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 30,756.6 ns | 432.23 ns | 226.06 ns |  8.20 |    0.22 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,754.9 ns | 194.34 ns | 101.64 ns |  1.00 |    0.04 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  2,908.8 ns | 162.75 ns |  85.12 ns |  0.78 |    0.03 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.919 μs |  0.5271 μs |  0.3137 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   522.365 μs | 27.3215 μs | 18.0715 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.984 μs |  0.7264 μs |  0.4322 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,493.175 μs | 52.2783 μs | 34.5789 μs |         - |


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