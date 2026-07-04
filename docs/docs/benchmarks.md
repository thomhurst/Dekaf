---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 02:36 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,171.15 μs** |    **754.76 μs** |    **41.371 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,325.42 μs |  2,207.96 μs |   121.026 μs |  0.21 |    0.02 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,376.99 μs** |    **546.27 μs** |    **29.943 μs** |  **1.00** |    **0.00** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,316.92 μs |    641.59 μs |    35.168 μs |  0.31 |    0.00 | 15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,225.98 μs** |    **744.49 μs** |    **40.808 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,168.68 μs |    523.96 μs |    28.720 μs |  0.19 |    0.00 |       - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **17,031.23 μs** | **98,244.05 μs** | **5,385.088 μs** |  **1.06** |    **0.40** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,983.78 μs |  1,041.68 μs |    57.098 μs |  0.37 |    0.09 | 15.6250 |       - |  361.68 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **149.08 μs** |    **229.89 μs** |    **12.601 μs** |  **1.00** |    **0.10** |  **2.4414** |       **-** |   **42.15 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     51.69 μs |     23.61 μs |     1.294 μs |  0.35 |    0.03 |       - |       - |    9.01 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,476.21 μs** |  **1,445.82 μs** |    **79.250 μs** |  **1.00** |    **0.07** | **23.4375** |       **-** |  **387.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    628.41 μs |  1,048.70 μs |    57.483 μs |  0.43 |    0.04 |       - |       - |   52.02 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.53 μs |    277.64 μs |    15.218 μs |     ? |       ? |  0.9766 |       - |  107.53 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,165.53 μs |  3,086.42 μs |   169.177 μs |     ? |       ? |  7.8125 |       - | 1023.61 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,431.63 μs** |    **125.23 μs** |     **6.865 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,390.92 μs |    175.17 μs |     9.602 μs |  0.26 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,437.74 μs** |    **193.59 μs** |    **10.611 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,374.23 μs |    257.91 μs |    14.137 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,436.04 μs** |    **194.64 μs** |    **10.669 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,207.92 μs |  1,384.49 μs |    75.889 μs |  0.22 |    0.01 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,431.37 μs** |    **126.02 μs** |     **6.908 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,356.39 μs |    205.22 μs |    11.249 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.566 ms** | **22.285 ms** | **1.2215 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.328 ms | 31.318 ms | 1.7166 ms | 0.006 |   593.4 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.663 ms** | **21.746 ms** | **1.1920 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.771 ms | 15.077 ms | 0.8264 ms | 0.005 |  775.56 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.777 ms** | **18.891 ms** | **1.0355 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.573 ms | 55.416 ms | 3.0375 ms | 0.006 |  994.84 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.154 ms** | **21.684 ms** | **1.1886 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.161 ms | 48.827 ms | 2.6764 ms | 0.005 | 2762.05 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.673 ms** | **15.528 ms** | **0.8512 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.623 ms | 28.569 ms | 1.5660 ms | 0.002 |  184.49 KB |       76.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.543 ms** | **34.354 ms** | **1.8831 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.366 ms | 16.164 ms | 0.8860 ms | 0.002 |  186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.561 ms** | **16.144 ms** | **0.8849 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.167 ms | 15.067 ms | 0.8259 ms | 0.002 |  193.65 KB |       80.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.545 ms** | **22.208 ms** | **1.2173 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.816 ms |  4.188 ms | 0.2296 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.825 μs |   2.324 μs |  0.1274 μs | 14.888 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.619 μs |   6.553 μs |  0.3592 μs |  9.712 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 16.498 μs | 197.471 μs | 10.8240 μs | 10.399 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.874 μs |   2.342 μs |  0.1284 μs | 26.821 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.910 μs |   1.654 μs |  0.0907 μs |  8.916 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.452 μs |   2.522 μs |  0.1383 μs | 19.415 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.874 μs |   4.846 μs |  0.2657 μs | 17.884 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.048 μs |  31.598 μs |  1.7320 μs | 20.277 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.328 μs |   1.655 μs |  0.0907 μs |  5.315 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.503 μs |   3.749 μs |  0.2055 μs | 10.400 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,277.5 ns |   965.4 ns |  52.92 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,235.3 ns |   557.4 ns |  30.55 ns |  0.28 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,449.5 ns | 2,060.6 ns | 112.95 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,469.0 ns | 1,220.7 ns |  66.91 ns |  0.57 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    664.5 ns |   549.4 ns |  30.12 ns |  0.15 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,689.7 ns | 5,254.1 ns | 287.99 ns |  9.34 |    0.09 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,356.2 ns |   650.2 ns |  35.64 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,952.5 ns | 2,211.9 ns | 121.24 ns |  0.91 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.26 μs |   2.305 μs |  0.126 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   519.28 μs | 290.397 μs | 15.918 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.06 μs |   0.390 μs |  0.021 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,683.97 μs | 318.612 μs | 17.464 μs |    1280 B |


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