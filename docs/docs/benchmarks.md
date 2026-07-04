---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 20:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,228.27 μs** |   **791.76 μs** |  **43.399 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,544.02 μs | 4,709.33 μs | 258.135 μs |  0.25 |    0.04 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,296.80 μs** |   **272.75 μs** |  **14.950 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,302.26 μs | 1,104.00 μs |  60.514 μs |  0.32 |    0.01 |  15.6250 |       - |  339.38 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,591.77 μs** |   **168.25 μs** |   **9.222 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,191.47 μs | 1,301.06 μs |  71.315 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,524.44 μs** | **6,761.00 μs** | **370.593 μs** |  **1.00** |    **0.04** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,730.95 μs | 2,452.11 μs | 134.409 μs |  0.50 |    0.02 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **129.68 μs** |   **161.53 μs** |   **8.854 μs** |  **1.00** |    **0.08** |   **2.6855** |       **-** |   **44.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.39 μs |    99.51 μs |   5.455 μs |  0.51 |    0.05 |        - |       - |    8.11 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,322.66 μs** | **4,201.67 μs** | **230.308 μs** |  **1.02** |    **0.22** |  **27.3438** |       **-** |  **446.32 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    593.83 μs |   855.49 μs |  46.892 μs |  0.46 |    0.08 |        - |       - |   63.63 KB |        0.14 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    207.94 μs |    68.19 μs |   3.738 μs |     ? |       ? |   0.9766 |       - |  106.08 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,945.70 μs | 3,999.32 μs | 219.216 μs |     ? |       ? |   7.8125 |       - |  987.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,431.72 μs** |   **136.49 μs** |   **7.482 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,367.38 μs |   147.70 μs |   8.096 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.41 μs** |    **99.15 μs** |   **5.435 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,377.81 μs |   283.32 μs |  15.529 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,446.23 μs** |   **226.20 μs** |  **12.399 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,374.55 μs |    33.94 μs |   1.860 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.55 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,441.03 μs** |   **125.35 μs** |   **6.871 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,126.87 μs |   604.72 μs |  33.147 μs |  0.21 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.208 ms** | **17.030 ms** | **0.9335 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.176 ms | 36.506 ms | 2.0010 ms | 0.005 |  607.19 KB |        8.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.846 ms** | **26.797 ms** | **1.4689 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.946 ms | 48.751 ms | 2.6722 ms | 0.006 |  774.77 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.452 ms** |  **5.092 ms** | **0.2791 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.541 ms | 14.134 ms | 0.7747 ms | 0.006 |  997.75 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.617 ms** | **10.085 ms** | **0.5528 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.439 ms | 35.140 ms | 1.9262 ms | 0.006 | 2759.74 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.959 ms** | **23.764 ms** | **1.3026 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.318 ms | 32.224 ms | 1.7663 ms | 0.002 |   185.1 KB |       76.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.582 ms** |  **8.669 ms** | **0.4751 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.283 ms |  9.344 ms | 0.5122 ms | 0.002 |  186.35 KB |       44.75 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.961 ms** | **46.945 ms** | **2.5732 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.422 ms |  9.345 ms | 0.5122 ms | 0.002 |  189.19 KB |       78.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.973 ms** | **46.011 ms** | **2.5220 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.431 ms | 27.000 ms | 1.4800 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.852 μs |   4.598 μs |  0.2520 μs | 25.789 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.324 μs |  29.424 μs |  1.6128 μs | 13.095 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.464 μs |   2.486 μs |  0.1363 μs | 10.401 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.664 μs |   1.652 μs |  0.0905 μs | 26.700 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.920 μs |   1.863 μs |  0.1021 μs |  8.877 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.198 μs |   1.110 μs |  0.0608 μs | 20.168 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.725 μs | 188.087 μs | 10.3097 μs | 17.974 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.033 μs |  30.990 μs |  1.6987 μs | 20.238 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.916 μs |  42.269 μs |  2.3169 μs |  4.583 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.724 μs |   4.772 μs |  0.2616 μs | 10.811 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,217.5 ns | 1,424.9 ns |  78.10 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,476.0 ns | 6,619.9 ns | 362.86 ns |  0.36 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,377.0 ns |   729.7 ns |  40.00 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,586.5 ns | 1,169.2 ns |  64.09 ns |  0.63 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    747.7 ns |   278.7 ns |  15.28 ns |  0.18 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,864.3 ns | 9,241.7 ns | 506.57 ns |  9.71 |    0.18 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,107.8 ns | 1,281.0 ns |  70.22 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,936.7 ns | 1,804.2 ns |  98.90 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.05 μs |  12.596 μs |  0.690 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   522.85 μs | 222.290 μs | 12.184 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.08 μs |   3.777 μs |  0.207 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,671.34 μs | 422.181 μs | 23.141 μs |    1280 B |


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