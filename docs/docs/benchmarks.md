---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 00:02 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,163.70 μs** |   **357.36 μs** |  **19.588 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,257.33 μs | 1,862.72 μs | 102.102 μs |  0.20 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,380.91 μs** | **1,208.68 μs** |  **66.252 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,375.13 μs |   671.55 μs |  36.810 μs |  0.32 |    0.00 |  15.6250 |  7.8125 |  339.96 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,126.82 μs** |   **453.32 μs** |  **24.848 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,337.06 μs | 1,707.85 μs |  93.613 μs |  0.22 |    0.01 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,753.93 μs** | **1,889.19 μs** | **103.553 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,876.14 μs | 6,160.01 μs | 337.651 μs |  0.54 |    0.02 |  15.6250 |       - |  369.44 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.95 μs** |    **21.20 μs** |   **1.162 μs** |  **1.00** |    **0.01** |   **2.4414** |  **0.2441** |   **42.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.80 μs |    65.03 μs |   3.565 μs |  0.42 |    0.02 |   0.7324 |  0.4883 |   17.82 KB |        0.42 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,408.65 μs** |   **455.94 μs** |  **24.991 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **427.18 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    777.95 μs | 2,095.29 μs | 114.850 μs |  0.55 |    0.07 |   3.9063 |       - |   137.4 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    323.37 μs |    36.15 μs |   1.981 μs |     ? |       ? |   6.8359 |  5.8594 |  195.18 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,203.92 μs | 5,604.12 μs | 307.180 μs |     ? |       ? |  70.3125 | 62.5000 | 1976.08 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,426.92 μs** |    **74.52 μs** |   **4.085 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,266.21 μs |   192.61 μs |  10.558 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,427.02 μs** |    **72.21 μs** |   **3.958 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,229.63 μs |   408.58 μs |  22.396 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.62 μs** |    **34.59 μs** |   **1.896 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,202.27 μs |   295.28 μs |  16.185 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,437.17 μs** |   **102.79 μs** |   **5.634 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,176.80 μs |   196.74 μs |  10.784 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.974 ms** | **46.972 ms** | **2.5747 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.675 ms | 43.210 ms | 2.3685 ms | 0.005 |  441.66 KB |        5.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.212 ms** | **36.359 ms** | **1.9930 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.331 ms | 38.451 ms | 2.1076 ms | 0.004 |  849.98 KB |        3.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.773 ms** |  **5.998 ms** | **0.3288 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.222 ms | 75.430 ms | 4.1345 ms | 0.005 | 1114.19 KB |        1.85 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.793 ms** |  **8.588 ms** | **0.4707 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.957 ms | 27.914 ms | 1.5300 ms | 0.005 | 5763.71 KB |        2.43 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.612 ms** | **27.142 ms** | **1.4877 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.314 ms | 29.068 ms | 1.5933 ms | 0.002 |  243.76 KB |      101.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.806 ms** | **21.145 ms** | **1.1590 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.706 ms | 34.058 ms | 1.8668 ms | 0.002 |  693.77 KB |      166.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.708 ms** | **26.482 ms** | **1.4516 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.876 ms |  4.848 ms | 0.2657 ms | 0.002 |  692.07 KB |      287.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.029 ms** | **16.767 ms** | **0.9190 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.082 ms | 14.885 ms | 0.8159 ms | 0.002 |  2272.3 KB |      543.65 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 19.438 μs | 119.880 μs | 6.5710 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.996 μs |  47.712 μs | 2.6153 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.544 μs |  20.884 μs | 1.1447 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.105 μs |   9.042 μs | 0.4956 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 11.903 μs |   1.700 μs | 0.0932 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 15.822 μs |   7.637 μs | 0.4186 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 16.500 μs |  20.777 μs | 1.1388 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.814 μs |  13.069 μs | 0.7163 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.549 μs |  21.697 μs | 1.1893 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.455 μs |   3.229 μs | 0.1770 μs |  1.3570 μs |  0.25 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  2.425 μs |  11.608 μs | 0.6363 μs |  2.6065 μs |  0.42 |    0.11 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.815 μs |   5.346 μs | 0.2930 μs |  1.7440 μs |  0.32 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3.396 μs |   8.630 μs | 0.4730 μs |  3.4005 μs |  0.59 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.206 μs |  17.929 μs | 0.9827 μs |  0.7045 μs |  0.21 |    0.15 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 51.295 μs | 145.591 μs | 7.9803 μs | 47.1045 μs |  8.93 |    1.66 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5.822 μs |  15.062 μs | 0.8256 μs |  5.8905 μs |  1.01 |    0.18 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  5.513 μs |  18.844 μs | 1.0329 μs |  4.9660 μs |  0.96 |    0.20 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.001 μs |  58.112 μs |  3.185 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   515.030 μs | 178.673 μs |  9.794 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.523 μs |  18.680 μs |  1.024 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,355.895 μs | 199.408 μs | 10.930 μs |    1280 B |


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