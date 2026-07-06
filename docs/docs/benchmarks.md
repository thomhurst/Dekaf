---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 01:10 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,232.39 μs** |    **616.04 μs** |    **33.767 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,339.37 μs |    556.63 μs |    30.511 μs |  0.21 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,392.75 μs** |    **517.97 μs** |    **28.392 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,362.90 μs |  1,138.02 μs |    62.379 μs |  0.32 |    0.01 |  15.6250 |       - |   339.4 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,132.65 μs** |    **802.83 μs** |    **44.006 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,431.62 μs |  1,987.39 μs |   108.935 μs |  0.23 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,922.67 μs** |  **1,913.27 μs** |   **104.873 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,103.63 μs |  7,199.70 μs |   394.640 μs |  0.55 |    0.03 |  15.6250 |       - |  361.82 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.27 μs** |     **62.29 μs** |     **3.414 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.18 μs |    173.03 μs |     9.484 μs |  0.49 |    0.06 |        - |       - |    7.14 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,404.33 μs** |    **352.54 μs** |    **19.324 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    637.08 μs |    312.67 μs |    17.138 μs |  0.45 |    0.01 |        - |       - |  112.58 KB |        0.34 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,123.67 μs** |    **155.13 μs** |     **8.503 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    221.70 μs |    151.15 μs |     8.285 μs |  0.20 |    0.01 |   0.9766 |       - |  106.41 KB |        0.87 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,336.52 μs** | **27,049.39 μs** | **1,482.669 μs** |  **1.02** |    **0.19** |  **74.2188** |       **-** | **1226.68 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,272.39 μs |  3,168.12 μs |   173.655 μs |  0.22 |    0.03 |   7.8125 |       - |  973.46 KB |        0.79 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,463.64 μs** |    **274.66 μs** |    **15.055 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,378.32 μs |    640.12 μs |    35.087 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,454.78 μs** |    **124.83 μs** |     **6.843 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,353.71 μs |    476.42 μs |    26.114 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,462.63 μs** |     **31.35 μs** |     **1.718 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,200.86 μs |    165.02 μs |     9.045 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,465.10 μs** |    **366.14 μs** |    **20.069 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.37 μs |     23.82 μs |     1.306 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.836 ms** |  **6.920 ms** | **0.3793 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.087 ms | 73.903 ms | 4.0509 ms | 0.005 |  600.54 KB |        8.05 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.296 ms** | **66.091 ms** | **3.6227 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.474 ms |  9.921 ms | 0.5438 ms | 0.005 |  785.13 KB |        3.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.053 ms** | **34.604 ms** | **1.8968 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.241 ms | 88.470 ms | 4.8493 ms | 0.006 | 1001.63 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.378 ms** | **16.266 ms** | **0.8916 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.192 ms | 24.781 ms | 1.3583 ms | 0.005 | 2771.08 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.276 ms** | **27.083 ms** | **1.4845 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.054 ms |  3.552 ms | 0.1947 ms | 0.002 |  184.57 KB |       76.70 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.391 ms** | **14.156 ms** | **0.7759 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.927 ms |  3.471 ms | 0.1902 ms | 0.002 |  186.54 KB |       44.80 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.896 ms** | **14.065 ms** | **0.7709 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.996 ms |  3.755 ms | 0.2058 ms | 0.002 |  184.75 KB |       76.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.514 ms** | **38.425 ms** | **2.1062 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.781 ms | 28.198 ms | 1.5456 ms | 0.002 |  187.27 KB |       44.81 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.969 μs |  2.946 μs | 0.1615 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.050 μs |  2.041 μs | 0.1119 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.571 μs |  3.386 μs | 0.1856 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.877 μs |  3.395 μs | 0.1861 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.438 μs | 88.393 μs | 4.8451 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.177 μs |  1.651 μs | 0.0905 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.489 μs | 35.316 μs | 1.9358 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.590 μs | 59.824 μs | 3.2791 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.283 μs |  7.443 μs | 0.4080 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.347 μs | 22.535 μs | 1.2352 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,462.7 ns | 5,106.3 ns | 279.90 ns |  0.35 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,389.0 ns | 1,630.4 ns |  89.37 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,238.0 ns | 1,072.7 ns |  58.80 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,518.7 ns | 1,010.9 ns |  55.41 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    761.7 ns | 1,269.3 ns |  69.57 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,357.2 ns | 9,754.2 ns | 534.66 ns |  9.98 |    0.14 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,143.7 ns |   690.7 ns |  37.86 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,906.7 ns | 2,476.7 ns | 135.76 ns |  0.94 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.176 μs |   5.039 μs |  0.2762 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   511.885 μs | 256.177 μs | 14.0419 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.328 μs |   1.173 μs |  0.0643 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,661.131 μs | 161.315 μs |  8.8422 μs |    1280 B |


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