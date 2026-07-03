---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 14:37 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,086.65 μs** |   **138.16 μs** |   **7.573 μs** |  **1.00** |    **0.00** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,250.74 μs | 2,328.43 μs | 127.629 μs |  0.21 |    0.02 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,170.61 μs** |   **556.09 μs** |  **30.481 μs** |  **1.00** |    **0.01** |  **62.5000** |  **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,307.13 μs |   367.29 μs |  20.133 μs |  0.32 |    0.00 |  15.6250 |        - |  340.21 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,677.29 μs** |   **135.80 μs** |   **7.444 μs** |  **1.00** |    **0.00** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,189.78 μs |   697.64 μs |  38.240 μs |  0.18 |    0.00 |        - |        - |   37.26 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,475.00 μs** |   **764.28 μs** |  **41.893 μs** |  **1.00** |    **0.00** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,895.49 μs | 1,591.42 μs |  87.231 μs |  0.51 |    0.01 |  15.6250 |        - |  372.14 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **159.03 μs** |   **160.65 μs** |   **8.806 μs** |  **1.00** |    **0.07** |   **2.5635** |        **-** |   **42.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.83 μs |    50.64 μs |   2.776 μs |  0.40 |    0.02 |   0.9766 |   0.7324 |   20.48 KB |        0.48 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,257.60 μs** |   **597.02 μs** |  **32.725 μs** |  **1.00** |    **0.03** |  **25.3906** |        **-** |  **419.73 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    689.74 μs |   812.12 μs |  44.515 μs |  0.55 |    0.03 |   3.9063 |        - |   119.5 KB |        0.28 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    377.66 μs |   345.74 μs |  18.951 μs |     ? |       ? |  10.7422 |   9.7656 |  243.13 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,623.98 μs | 4,581.91 μs | 251.150 μs |     ? |       ? | 117.1875 | 109.3750 | 2491.92 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,401.03 μs** |   **128.99 μs** |   **7.071 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,348.13 μs |   238.46 μs |  13.071 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.48 μs** |    **52.34 μs** |   **2.869 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,360.38 μs |   248.54 μs |  13.623 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.13 μs** |   **132.44 μs** |   **7.259 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,105.74 μs |    33.98 μs |   1.862 μs |  0.20 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.10 μs** |   **196.98 μs** |  **10.797 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,105.24 μs |    48.46 μs |   2.656 μs |  0.20 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.459 ms** |  **6.349 ms** | **0.3480 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.365 ms | 58.788 ms | 3.2224 ms | 0.005 |   406.4 KB |        5.45 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.596 ms** | **13.749 ms** | **0.7537 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.121 ms | 11.711 ms | 0.6419 ms | 0.004 |  590.19 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.423 ms** |  **7.548 ms** | **0.4137 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.546 ms | 62.810 ms | 3.4429 ms | 0.005 |  808.44 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.841 ms** | **38.214 ms** | **2.0946 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.087 ms | 35.117 ms | 1.9249 ms | 0.004 | 2649.56 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.181 ms** | **14.732 ms** | **0.8075 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.742 ms | 10.914 ms | 0.5982 ms | 0.002 |  182.36 KB |       75.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.267 ms** |  **8.276 ms** | **0.4536 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.713 ms |  7.186 ms | 0.3939 ms | 0.002 |  183.19 KB |       43.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.292 ms** | **15.700 ms** | **0.8606 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.100 ms | 25.995 ms | 1.4249 ms | 0.002 |  181.43 KB |       75.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.759 ms** | **21.201 ms** | **1.1621 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.309 ms | 12.262 ms | 0.6721 ms | 0.002 |  183.84 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 13.114 μs |  5.950 μs | 0.3261 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  5.632 μs | 20.773 μs | 1.1387 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  7.692 μs | 11.896 μs | 0.6520 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 19.627 μs | 20.696 μs | 1.1344 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.411 μs |  7.957 μs | 0.4361 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 12.678 μs |  8.572 μs | 0.4699 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 10.526 μs | 28.982 μs | 1.5886 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 10.164 μs | 41.969 μs | 2.3005 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.756 μs | 23.415 μs | 1.2834 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  6.635 μs | 33.501 μs | 1.8363 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,245.0 ns | 19,369.6 ns | 1,061.7 ns |  1,652.0 ns |  0.74 |    0.61 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |    637.7 ns |  3,472.7 ns |   190.4 ns |    651.0 ns |  0.38 |    0.15 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,359.0 ns | 18,371.2 ns | 1,007.0 ns |  1,773.0 ns |  0.81 |    0.59 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,924.7 ns |  3,270.3 ns |   179.3 ns |  2,018.0 ns |  1.15 |    0.35 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1,108.3 ns | 17,574.4 ns |   963.3 ns |  1,282.0 ns |  0.66 |    0.55 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 19,509.7 ns | 25,542.0 ns | 1,400.0 ns | 19,049.0 ns | 11.66 |    3.44 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  1,820.5 ns | 12,348.6 ns |   676.9 ns |  1,606.5 ns |  1.09 |    0.48 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,028.0 ns | 14,048.9 ns |   770.1 ns |  2,624.0 ns |  1.81 |    0.67 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   5.594 μs |  33.842 μs |  1.855 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 269.557 μs | 282.399 μs | 15.479 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |   4.743 μs |  25.139 μs |  1.378 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 758.721 μs | 314.566 μs | 17.242 μs |    1280 B |


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