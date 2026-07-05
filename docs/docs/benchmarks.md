---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 21:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,299.51 μs** |  **1,119.53 μs** |  **61.365 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,380.35 μs |  2,042.87 μs | 111.976 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,338.24 μs** |    **851.68 μs** |  **46.683 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,363.49 μs |  1,195.31 μs |  65.519 μs |  0.32 |    0.01 |  15.6250 |       - |  339.63 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,218.45 μs** |    **268.37 μs** |  **14.710 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,392.56 μs |  4,147.92 μs | 227.362 μs |  0.22 |    0.03 |        - |       - |   36.25 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,169.40 μs** | **13,996.76 μs** | **767.210 μs** |  **1.00** |    **0.07** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,586.98 μs |  3,322.18 μs | 182.100 μs |  0.50 |    0.03 |  15.6250 |       - |  361.61 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.85 μs** |    **133.94 μs** |   **7.342 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **43.47 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     73.53 μs |    137.74 μs |   7.550 μs |  0.52 |    0.05 |        - |       - |    8.63 KB |        0.20 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,453.31 μs** |    **385.72 μs** |  **21.143 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **420.35 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    594.42 μs |    666.59 μs |  36.538 μs |  0.41 |    0.02 |        - |       - |  119.86 KB |        0.29 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    225.18 μs |    481.14 μs |  26.373 μs |     ? |       ? |   0.9766 |       - |  101.06 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,155.62 μs |  2,526.75 μs | 138.500 μs |     ? |       ? |   7.8125 |       - |  983.34 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.74 μs** |    **144.30 μs** |   **7.909 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,351.36 μs |    383.47 μs |  21.019 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,444.00 μs** |    **578.61 μs** |  **31.716 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,248.61 μs |    543.43 μs |  29.787 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,439.50 μs** |     **55.79 μs** |   **3.058 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,116.69 μs |     21.28 μs |   1.166 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.47 μs** |     **30.46 μs** |   **1.670 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.55 μs |     12.13 μs |   0.665 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.041 ms** |  **4.397 ms** | **0.2410 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.715 ms | 18.882 ms | 1.0350 ms | 0.005 |  599.98 KB |        8.04 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.637 ms** |  **4.952 ms** | **0.2714 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.467 ms | 26.583 ms | 1.4571 ms | 0.005 |  778.93 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.952 ms** |  **6.079 ms** | **0.3332 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.823 ms | 37.434 ms | 2.0519 ms | 0.006 | 1017.82 KB |        1.69 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.411 ms** | **17.076 ms** | **0.9360 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.886 ms | 52.700 ms | 2.8887 ms | 0.006 | 2764.27 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.236 ms** | **14.722 ms** | **0.8069 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.143 ms | 11.022 ms | 0.6041 ms | 0.002 |   184.6 KB |       76.72 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.321 ms** | **11.857 ms** | **0.6499 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.193 ms | 11.068 ms | 0.6067 ms | 0.002 |  186.55 KB |       44.80 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.132 ms** | **17.897 ms** | **0.9810 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.882 ms |  5.313 ms | 0.2912 ms | 0.002 |  256.88 KB |      106.75 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.561 ms** | **16.095 ms** | **0.8822 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.336 ms | 11.659 ms | 0.6391 ms | 0.002 |  187.39 KB |       44.83 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.804 μs |  1.4354 μs | 0.0787 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.537 μs | 11.7337 μs | 0.6432 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.522 μs |  2.3395 μs | 0.1282 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.096 μs |  8.4024 μs | 0.4606 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.018 μs |  0.7298 μs | 0.0400 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.320 μs |  1.5747 μs | 0.0863 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.930 μs |  1.0449 μs | 0.0573 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.268 μs |  5.7553 μs | 0.3155 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.638 μs |  1.0240 μs | 0.0561 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.463 μs |  2.4384 μs | 0.1337 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,453.5 ns |  6,654.4 ns | 364.75 ns |  0.27 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,455.0 ns |  1,870.0 ns | 102.50 ns |  0.27 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,383.0 ns |    482.7 ns |  26.46 ns |  0.26 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,567.8 ns |  1,485.9 ns |  81.45 ns |  0.48 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    774.3 ns |  1,158.6 ns |  63.51 ns |  0.14 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,691.3 ns |  7,218.4 ns | 395.66 ns |  7.58 |    0.80 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,423.3 ns | 12,937.4 ns | 709.14 ns |  1.01 |    0.16 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,927.5 ns |    965.4 ns |  52.92 ns |  0.73 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev    | Allocated |
|------------------------ |------------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.26 μs |     3.253 μs |  0.178 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   565.20 μs | 1,196.433 μs | 65.581 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.45 μs |    41.791 μs |  2.291 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,848.74 μs |   526.496 μs | 28.859 μs |    1280 B |


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