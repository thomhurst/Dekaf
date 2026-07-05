---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 17:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,123.83 μs** |   **431.54 μs** |  **23.654 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,362.31 μs |   558.32 μs |  30.604 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,479.11 μs** |   **530.16 μs** |  **29.060 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,265.44 μs |   258.03 μs |  14.144 μs |  0.30 |    0.00 |  19.5313 |  3.9063 |  339.51 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,650.11 μs** | **7,936.35 μs** | **435.018 μs** |  **1.00** |    **0.08** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,261.47 μs |   379.61 μs |  20.808 μs |  0.19 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,519.27 μs** | **3,521.63 μs** | **193.032 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,708.29 μs | 3,303.09 μs | 181.054 μs |  0.54 |    0.01 |  15.6250 |       - |  361.87 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **142.20 μs** |    **58.98 μs** |   **3.233 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.37 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     60.81 μs |    40.60 μs |   2.225 μs |  0.43 |    0.02 |        - |       - |     7.7 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,413.18 μs** |   **874.12 μs** |  **47.913 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **420.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    625.76 μs |   761.34 μs |  41.732 μs |  0.44 |    0.03 |        - |       - |  147.94 KB |        0.35 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.42 μs |   212.44 μs |  11.645 μs |     ? |       ? |   0.9766 |       - |  100.89 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,053.84 μs | 1,250.83 μs |  68.562 μs |     ? |       ? |   7.8125 |       - |  975.12 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,426.42 μs** |    **39.01 μs** |   **2.138 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,359.90 μs |   310.15 μs |  17.000 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.16 μs** |    **98.97 μs** |   **5.425 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,328.48 μs |    66.68 μs |   3.655 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,446.48 μs** |   **231.42 μs** |  **12.685 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,116.21 μs |    37.62 μs |   2.062 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,422.29 μs** |   **126.07 μs** |   **6.911 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,114.83 μs |    33.02 μs |   1.810 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.783 ms** |  **5.872 ms** | **0.3219 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.181 ms |  8.386 ms | 0.4597 ms | 0.004 |  596.16 KB |        7.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.952 ms** | **11.448 ms** | **0.6275 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.457 ms | 12.824 ms | 0.7029 ms | 0.004 |  778.31 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.065 ms** | **26.666 ms** | **1.4617 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.889 ms | 45.130 ms | 2.4737 ms | 0.005 |  1079.8 KB |        1.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.643 ms** | **24.345 ms** | **1.3344 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.912 ms | 37.192 ms | 2.0386 ms | 0.005 | 2764.18 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.584 ms** | **28.769 ms** | **1.5769 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.352 ms |  9.346 ms | 0.5123 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.306 ms** | **29.697 ms** | **1.6278 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.822 ms | 11.165 ms | 0.6120 ms | 0.002 |  186.39 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.675 ms** | **22.193 ms** | **1.2164 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.776 ms | 16.872 ms | 0.9248 ms | 0.002 |   185.2 KB |       76.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.363 ms** |  **1.325 ms** | **0.0726 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.499 ms | 24.547 ms | 1.3455 ms | 0.002 |  205.17 KB |       49.09 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.874 μs |   2.4279 μs |  0.1331 μs | 25.798 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.280 μs |  32.7919 μs |  1.7974 μs | 10.319 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.341 μs |  26.1348 μs |  1.4325 μs | 10.559 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.796 μs |   2.4217 μs |  0.1327 μs | 26.830 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.090 μs |   0.8321 μs |  0.0456 μs |  9.086 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 30.118 μs | 309.1935 μs | 16.9479 μs | 20.393 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.336 μs |   6.8841 μs |  0.3773 μs | 18.379 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.914 μs |  41.0970 μs |  2.2527 μs | 24.526 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.801 μs |   2.9607 μs |  0.1623 μs |  4.883 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 18.688 μs | 251.4785 μs | 13.7844 μs | 10.839 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,756.0 ns |   1,636.6 ns |     89.71 ns |  0.42 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,183.8 ns |     848.0 ns |     46.48 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,888.2 ns |   1,136.4 ns |     62.29 ns |  0.46 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,530.7 ns |     421.3 ns |     23.09 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    775.3 ns |   1,086.1 ns |     59.53 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,797.3 ns | 216,189.5 ns | 11,850.08 ns | 11.33 |    2.49 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,132.5 ns |     965.4 ns |     52.92 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,505.2 ns |   8,336.9 ns |    456.97 ns |  1.09 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev     | Allocated |
|------------------------ |-------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.940 μs |     6.063 μs |  0.3323 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   516.542 μs |   217.760 μs | 11.9362 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.508 μs |    18.895 μs |  1.0357 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,712.480 μs | 1,054.389 μs | 57.7946 μs |    1280 B |


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