---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-08 11:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,916.1 μs** |    **52.39 μs** |    **34.66 μs** |  **5,914.4 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,249.1 μs |    37.62 μs |    22.39 μs |  1,244.8 μs |  0.21 |    0.00 |   1.9531 |       - |   37.77 KB |        0.35 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,221.0 μs** |    **83.00 μs** |    **54.90 μs** |  **7,204.8 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  6,015.1 μs | 3,339.33 μs | 2,208.76 μs |  5,841.4 μs |  0.83 |    0.29 |  23.4375 |  7.8125 |  474.79 KB |        0.45 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,655.9 μs** |    **13.62 μs** |     **9.01 μs** |  **6,656.3 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,750.2 μs |   869.79 μs |   575.31 μs |  1,371.2 μs |  0.26 |    0.08 |        - |       - |   45.51 KB |        0.23 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,479.1 μs** |   **253.24 μs** |   **167.50 μs** | **11,458.4 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 23,559.6 μs | 6,086.81 μs | 3,622.16 μs | 23,066.1 μs |  2.05 |    0.30 |  62.5000 |       - | 1273.62 KB |        0.66 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.1 μs** |     **2.34 μs** |     **1.55 μs** |    **134.3 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    367.7 μs |   184.40 μs |   121.97 μs |    387.0 μs |  2.74 |    0.87 |   0.2441 |       - |    5.23 KB |        0.12 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,326.6 μs** |    **49.58 μs** |    **29.50 μs** |  **1,329.9 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |   **421.8 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  3,212.7 μs |   665.53 μs |   348.09 μs |  3,322.4 μs |  2.42 |    0.25 |   1.9531 |       - |   52.87 KB |        0.13 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |          **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  2,989.0 μs |   519.61 μs |   343.69 μs |  2,939.7 μs |     ? |       ? |   0.4883 |       - |   15.67 KB |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |          **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 25,444.0 μs | 2,314.18 μs | 1,377.13 μs | 25,533.9 μs |     ? |       ? |   7.8125 |       - |  159.48 KB |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,366.0 μs** |     **3.34 μs** |     **1.99 μs** |  **5,366.2 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,334.8 μs |    24.68 μs |    16.33 μs |  1,335.9 μs |  0.25 |    0.00 |        - |       - |     1.6 KB |        1.34 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,368.1 μs** |     **5.14 μs** |     **3.06 μs** |  **5,367.7 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,313.4 μs |    32.49 μs |    21.49 μs |  1,324.0 μs |  0.24 |    0.00 |        - |       - |     1.6 KB |        1.34 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,365.4 μs** |     **1.93 μs** |     **1.01 μs** |  **5,365.3 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,242.0 μs |    29.98 μs |    19.83 μs |  1,248.2 μs |  0.23 |    0.00 |        - |       - |     1.6 KB |        0.77 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,367.1 μs** |     **3.91 μs** |     **2.59 μs** |  **5,368.1 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,096.2 μs |     1.32 μs |     0.87 μs |  1,096.4 μs |  0.20 |    0.00 |        - |       - |     1.6 KB |        0.77 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-OHSMRJ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-OHSMRJ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.177 s** | **0.0039 s** | **0.0006 s** |  **1.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0074 s | 0.0011 s |  0.95 |  121784 B |        1.58 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.174 s** | **0.0027 s** | **0.0007 s** |  **1.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0031 s | 0.0005 s |  0.95 |  316584 B |        1.23 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.175 s** | **0.0013 s** | **0.0003 s** |  **1.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0034 s | 0.0005 s |  0.95 |  370432 B |        0.60 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.174 s** | **0.0019 s** | **0.0005 s** |  **1.00** | **2424736 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0045 s | 0.0007 s |  0.95 | 2185672 B |        0.90 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.164 s** | **0.0015 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0044 s | 0.0011 s |  0.95 |   83792 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.164 s** | **0.0024 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0024 s | 0.0006 s |  0.95 |   85512 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.164 s** | **0.0031 s** | **0.0008 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.012 s | 0.0022 s | 0.0006 s |  0.95 |   84440 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.164 s** | **0.0016 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.013 s | 0.0033 s | 0.0009 s |  0.95 |   89824 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 27.075 μs |  8.6571 μs | 5.1517 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.445 μs |  0.4452 μs | 0.2649 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.256 μs |  0.2769 μs | 0.1448 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 43.372 μs | 17.5646 μs | 9.1866 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 24.608 μs |  8.3445 μs | 4.9657 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 21.909 μs |  9.9775 μs | 5.9374 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 18.033 μs |  1.0433 μs | 0.6209 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.175 μs |  0.7192 μs | 0.4757 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,412.9 ns |    265.75 ns |    158.14 ns |  0.36 |    0.05 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,516.5 ns |    257.14 ns |    153.02 ns |  0.38 |    0.05 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,509.7 ns |    410.09 ns |    244.04 ns |  0.38 |    0.07 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,121.2 ns |    392.91 ns |    259.89 ns |  0.79 |    0.10 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    634.1 ns |     61.19 ns |     32.00 ns |  0.16 |    0.02 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,091.0 ns | 17,191.89 ns | 11,371.37 ns | 10.12 |    2.93 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,000.3 ns |    725.04 ns |    431.46 ns |  1.01 |    0.14 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  4,145.6 ns |    908.30 ns |    600.78 ns |  1.05 |    0.18 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.274 μs |  0.6675 μs |  0.3972 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   461.664 μs | 15.9367 μs | 10.5411 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.216 μs |  0.7232 μs |  0.4784 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,395.917 μs | 33.5210 μs | 22.1721 μs |         - |


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