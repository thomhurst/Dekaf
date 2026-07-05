---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 20:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,634.24 μs** |   **993.37 μs** |  **54.450 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,585.16 μs |   706.04 μs |  38.700 μs |  0.24 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,735.97 μs** |   **370.33 μs** |  **20.299 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,377.49 μs |   641.44 μs |  35.160 μs |  0.31 |    0.00 |  15.6250 |       - |  339.48 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,396.30 μs** | **1,962.13 μs** | **107.551 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,462.23 μs | 1,699.21 μs |  93.140 μs |  0.23 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,212.52 μs** | **6,520.28 μs** | **357.399 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,756.63 μs | 5,918.44 μs | 324.409 μs |  0.51 |    0.02 |  15.6250 |       - |  361.77 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.68 μs** |   **102.24 μs** |   **5.604 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.39 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.12 μs |    94.35 μs |   5.172 μs |  0.46 |    0.03 |        - |       - |     8.8 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,481.97 μs** |   **478.16 μs** |  **26.209 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    659.50 μs | 1,013.93 μs |  55.577 μs |  0.45 |    0.03 |        - |       - |  105.36 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    224.12 μs |   589.94 μs |  32.337 μs |     ? |       ? |   0.9766 |       - |   98.97 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,329.12 μs** |   **477.04 μs** |  **26.148 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1227.78 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,291.44 μs | 1,621.36 μs |  88.872 μs |  0.98 |    0.03 |   7.8125 |       - |  980.76 KB |        0.80 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,475.41 μs** |    **97.90 μs** |   **5.366 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,406.99 μs |   501.97 μs |  27.515 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,505.69 μs** |   **314.26 μs** |  **17.226 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,397.78 μs |    98.60 μs |   5.405 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,494.99 μs** |    **63.18 μs** |   **3.463 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,404.04 μs |   232.78 μs |  12.760 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,470.32 μs** |   **200.29 μs** |  **10.979 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,399.72 μs |   473.80 μs |  25.971 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.109 ms** | **21.698 ms** | **1.1894 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    22.412 ms | 63.390 ms | 3.4746 ms | 0.007 |  596.66 KB |        8.00 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.353 ms** | **15.183 ms** | **0.8322 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    18.645 ms | 35.018 ms | 1.9195 ms | 0.006 |   778.8 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.285 ms** |  **6.991 ms** | **0.3832 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.176 ms | 28.437 ms | 1.5587 ms | 0.006 |  998.27 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.485 ms** | **20.310 ms** | **1.1132 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.805 ms | 18.940 ms | 1.0382 ms | 0.006 | 2764.53 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,158.140 ms** |  **4.337 ms** | **0.2377 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.867 ms |  6.036 ms | 0.3308 ms | 0.002 |  184.45 KB |       76.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.328 ms** | **28.448 ms** | **1.5594 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.299 ms | 14.872 ms | 0.8152 ms | 0.002 |  195.41 KB |       46.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.111 ms** |  **9.649 ms** | **0.5289 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.942 ms |  5.826 ms | 0.3194 ms | 0.003 |  184.73 KB |       76.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.246 ms** |  **1.374 ms** | **0.0753 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.292 ms | 14.800 ms | 0.8112 ms | 0.002 |  262.71 KB |       62.85 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.846 μs |  2.966 μs | 0.1626 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.613 μs |  5.769 μs | 0.3162 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.034 μs |  3.872 μs | 0.2122 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.830 μs |  1.585 μs | 0.0869 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.966 μs | 62.953 μs | 3.4507 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.427 μs | 70.289 μs | 3.8528 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.155 μs | 10.874 μs | 0.5961 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.771 μs | 20.597 μs | 1.1290 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.780 μs |  2.816 μs | 0.1544 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.555 μs |  5.968 μs | 0.3271 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.333 μs |  0.5473 μs | 0.0300 μs |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.369 μs |  2.0085 μs | 0.1101 μs |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.312 μs |  1.4933 μs | 0.0819 μs |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.588 μs |  0.3828 μs | 0.0210 μs |  0.64 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.058 μs |  3.8074 μs | 0.2087 μs |  0.26 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46.624 μs | 76.9392 μs | 4.2173 μs | 11.58 |    1.05 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.032 μs |  3.9509 μs | 0.2166 μs |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.252 μs | 15.8437 μs | 0.8684 μs |  1.06 |    0.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.92 μs |   6.856 μs |  0.376 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   561.78 μs | 829.822 μs | 45.485 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.14 μs |   1.024 μs |  0.056 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,625.82 μs | 118.651 μs |  6.504 μs |    1280 B |


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