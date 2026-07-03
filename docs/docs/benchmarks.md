---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 09:59 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,038.49 μs** | **2,918.03 μs** | **159.947 μs** |  **1.00** |    **0.03** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,205.77 μs |   635.07 μs |  34.811 μs |  0.20 |    0.01 |       - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,015.72 μs** | **1,330.26 μs** |  **72.916 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,167.58 μs |    50.69 μs |   2.778 μs |  0.31 |    0.00 | 19.5313 |  3.9063 |  339.84 KB |        0.32 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,438.15 μs** |   **198.37 μs** |  **10.873 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,187.01 μs | 1,129.59 μs |  61.917 μs |  0.18 |    0.01 |       - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,085.15 μs** | **2,972.28 μs** | **162.921 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,538.73 μs | 1,772.89 μs |  97.178 μs |  0.50 |    0.01 | 15.6250 |       - |  369.45 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **128.42 μs** |   **458.88 μs** |  **25.153 μs** |  **1.03** |    **0.26** |  **2.8076** |       **-** |   **46.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    51.51 μs |    79.06 μs |   4.333 μs |  0.41 |    0.08 |  0.7324 |  0.4883 |   17.73 KB |        0.38 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   519.24 μs |   632.96 μs |  34.695 μs |     ? |       ? |  5.8594 |  3.9063 |  156.22 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   250.68 μs |   131.42 μs |   7.204 μs |     ? |       ? |  3.9063 |  3.4180 |  104.03 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,555.51 μs |   943.70 μs |  51.728 μs |     ? |       ? | 62.5000 | 54.6875 | 1787.03 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,415.17 μs** | **1,011.53 μs** |  **55.445 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,234.33 μs | 1,305.65 μs |  71.567 μs |  0.23 |    0.01 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,341.96 μs** |    **52.97 μs** |   **2.903 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,324.73 μs |   135.67 μs |   7.437 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,425.71 μs** | **2,402.26 μs** | **131.676 μs** |  **1.00** |    **0.03** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,336.00 μs | 1,525.85 μs |  83.637 μs |  0.25 |    0.01 |       - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,356.75 μs** |    **73.95 μs** |   **4.053 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,094.23 μs |    59.00 μs |   3.234 μs |  0.20 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error        | StdDev     | Median       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-------------:|-----------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.223 ms** |    **36.843 ms** |  **2.0195 ms** | **3,167.366 ms** | **1.000** |    **0.00** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.424 ms |    13.253 ms |  0.7264 ms |    13.702 ms | 0.004 |    0.00 | 406.22 KB |        5.44 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.836 ms** |    **65.576 ms** |  **3.5944 ms** | **3,166.186 ms** | **1.000** |    **0.00** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.268 ms |    22.896 ms |  1.2550 ms |    12.690 ms | 0.004 |    0.00 | 591.45 KB |        2.36 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,187.390 ms** |   **727.353 ms** | **39.8687 ms** | **3,164.774 ms** | **1.000** |    **0.02** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    11.767 ms |    47.645 ms |  2.6116 ms |    10.579 ms | 0.004 |    0.00 | 814.34 KB |        1.35 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,201.387 ms** | **1,165.532 ms** | **63.8867 ms** | **3,165.023 ms** | **1.000** |    **0.02** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.532 ms |    89.043 ms |  4.8808 ms |    12.459 ms | 0.005 |    0.00 | 2578.6 KB |        1.09 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.535 ms** |    **23.449 ms** |  **1.2853 ms** | **3,154.930 ms** | **1.000** |    **0.00** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.726 ms |    13.295 ms |  0.7287 ms |     5.631 ms | 0.002 |    0.00 | 183.59 KB |       76.30 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.884 ms** |    **29.868 ms** |  **1.6371 ms** | **3,155.290 ms** | **1.000** |    **0.00** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.935 ms |    11.047 ms |  0.6055 ms |     4.879 ms | 0.002 |    0.00 | 189.15 KB |       45.42 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.267 ms** |    **20.184 ms** |  **1.1063 ms** | **3,155.910 ms** | **1.000** |    **0.00** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.736 ms |    73.914 ms |  4.0515 ms |     6.824 ms | 0.003 |    0.00 | 181.43 KB |       75.40 |
|                      |            |              |             |              |              |            |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.676 ms** |    **15.507 ms** |  **0.8500 ms** | **3,155.370 ms** | **1.000** |    **0.00** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.099 ms |     7.357 ms |  0.4033 ms |     5.102 ms | 0.002 |    0.00 |  258.1 KB |       61.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 21.179 μs | 88.2898 μs | 4.8395 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.400 μs |  4.2290 μs | 0.2318 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.152 μs |  0.4162 μs | 0.0228 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.800 μs |  1.2745 μs | 0.0699 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 10.860 μs | 60.7863 μs | 3.3319 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.336 μs |  3.6999 μs | 0.2028 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.969 μs | 53.1460 μs | 2.9131 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.492 μs |  1.1586 μs | 0.0635 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.082 μs | 13.0087 μs | 0.7131 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,252.7 ns |     667.9 ns |     36.61 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,138.7 ns |   1,004.8 ns |     55.08 ns |  0.27 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,436.3 ns |   2,206.9 ns |    120.97 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,524.8 ns |     486.2 ns |     26.65 ns |  0.60 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    677.7 ns |     737.3 ns |     40.41 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,686.7 ns | 214,116.9 ns | 11,736.47 ns | 11.15 |    2.45 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,191.3 ns |   2,732.5 ns |    149.78 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,730.7 ns |   5,116.1 ns |    280.43 ns |  0.89 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.148 μs |   2.001 μs |  0.1097 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   564.505 μs | 603.993 μs | 33.1069 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.078 μs |  13.308 μs |  0.7295 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,650.136 μs | 303.096 μs | 16.6137 μs |    1280 B |


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