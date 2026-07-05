---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 19:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,078.74 μs** |   **270.355 μs** |  **14.819 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,328.36 μs |   852.741 μs |  46.742 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,184.15 μs** | **1,037.009 μs** |  **56.842 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,236.07 μs |   286.156 μs |  15.685 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.54 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,795.14 μs** | **5,147.545 μs** | **282.154 μs** |  **1.00** |    **0.05** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,138.16 μs |   140.256 μs |   7.688 μs |  0.17 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,899.33 μs** | **4,725.494 μs** | **259.020 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,752.93 μs |   581.991 μs |  31.901 μs |  0.48 |    0.01 |  15.6250 |       - |  361.66 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.26 μs** |   **212.524 μs** |  **11.649 μs** |  **1.00** |    **0.10** |   **2.4414** |       **-** |   **41.14 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.62 μs |   148.829 μs |   8.158 μs |  0.47 |    0.06 |        - |       - |    6.89 KB |        0.17 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,335.43 μs** | **3,057.349 μs** | **167.584 μs** |  **1.01** |    **0.15** |  **23.4375** |       **-** |  **402.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    654.83 μs |   954.733 μs |  52.332 μs |  0.50 |    0.06 |        - |       - |   58.15 KB |        0.14 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    202.67 μs |   219.028 μs |  12.006 μs |     ? |       ? |   0.4883 |       - |   15.93 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,881.08 μs | 1,136.992 μs |  62.322 μs |     ? |       ? |   7.8125 |       - |  981.28 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,403.20 μs** |   **140.415 μs** |   **7.697 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,103.44 μs |    23.694 μs |   1.299 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,402.72 μs** |    **65.661 μs** |   **3.599 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,371.34 μs |   211.498 μs |  11.593 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,405.40 μs** |   **197.521 μs** |  **10.827 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,360.16 μs |   708.904 μs |  38.857 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.02 μs** |     **7.150 μs** |   **0.392 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,383.15 μs |   903.205 μs |  49.508 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.287 ms** |  **5.179 ms** | **0.2839 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.062 ms | 44.959 ms | 2.4643 ms | 0.005 |   597.5 KB |        8.01 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.122 ms** | **14.822 ms** | **0.8124 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.844 ms | 16.552 ms | 0.9073 ms | 0.005 |  777.95 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.856 ms** | **11.123 ms** | **0.6097 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.976 ms | 40.299 ms | 2.2089 ms | 0.005 |   998.3 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.481 ms** | **11.180 ms** | **0.6128 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.900 ms | 24.916 ms | 1.3657 ms | 0.006 | 2765.09 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.039 ms** | **14.990 ms** | **0.8216 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.403 ms | 15.041 ms | 0.8244 ms | 0.002 |  202.49 KB |       84.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.933 ms** | **30.362 ms** | **1.6643 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.906 ms | 10.634 ms | 0.5829 ms | 0.002 |  186.48 KB |       44.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.510 ms** | **12.759 ms** | **0.6994 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.462 ms | 16.621 ms | 0.9111 ms | 0.002 |  193.75 KB |       80.52 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.544 ms** | **24.235 ms** | **1.3284 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.246 ms | 14.029 ms | 0.7690 ms | 0.002 |  261.37 KB |       62.53 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.922 μs |  3.3249 μs | 0.1822 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.549 μs |  1.6419 μs | 0.0900 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.525 μs |  1.3774 μs | 0.0755 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.756 μs |  0.5654 μs | 0.0310 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.381 μs |  2.1145 μs | 0.1159 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.479 μs |  2.1040 μs | 0.1153 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.552 μs | 14.9292 μs | 0.8183 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.949 μs | 93.6783 μs | 5.1348 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.170 μs | 10.9285 μs | 0.5990 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.064 μs |  2.4372 μs | 0.1336 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,532.8 ns | 5,941.7 ns | 325.68 ns |  0.37 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,449.7 ns | 4,122.6 ns | 225.97 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.7 ns | 1,688.9 ns |  92.58 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,755.3 ns | 1,931.6 ns | 105.88 ns |  0.66 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    676.5 ns |   182.4 ns |  10.00 ns |  0.16 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,599.7 ns | 7,542.2 ns | 413.41 ns |  9.73 |    0.12 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,171.2 ns |   694.8 ns |  38.08 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,272.8 ns | 3,046.8 ns | 167.00 ns |  1.02 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error       | StdDev    | Allocated |
|------------------------ |------------:|------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.61 μs |    15.57 μs |  0.854 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   584.82 μs | 1,386.11 μs | 75.977 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.30 μs |    14.04 μs |  0.769 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,697.73 μs |   414.73 μs | 22.733 μs |    1280 B |


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