---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 00:49 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,882.81 μs** |    **485.05 μs** |    **26.587 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,300.19 μs |  1,098.95 μs |    60.237 μs |  0.22 |    0.01 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,991.13 μs** |  **1,404.83 μs** |    **77.004 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,120.77 μs |    321.25 μs |    17.609 μs |  0.30 |    0.00 |  19.5313 |  3.9063 |   339.5 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,378.85 μs** |    **188.44 μs** |    **10.329 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,098.20 μs |    241.93 μs |    13.261 μs |  0.17 |    0.00 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,119.33 μs** |    **874.34 μs** |    **47.925 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,134.90 μs | 19,143.38 μs | 1,049.313 μs |  0.56 |    0.10 |  15.6250 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **103.19 μs** |    **168.16 μs** |     **9.217 μs** |  **1.01** |    **0.11** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    43.72 μs |    103.91 μs |     5.696 μs |  0.43 |    0.06 |   0.2441 |       - |    8.38 KB |        0.25 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **955.93 μs** |    **593.01 μs** |    **32.505 μs** |  **1.00** |    **0.04** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   740.79 μs |  3,226.84 μs |   176.874 μs |  0.78 |    0.16 |        - |       - |   70.74 KB |        0.21 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **895.29 μs** |  **2,948.65 μs** |   **161.625 μs** |  **1.02** |    **0.22** |   **7.3242** |       **-** |  **122.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   193.60 μs |  1,167.07 μs |    63.971 μs |  0.22 |    0.07 |   0.4883 |       - |   12.17 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **8,600.74 μs** | **28,128.46 μs** | **1,541.816 μs** |  **1.02** |    **0.22** |  **74.2188** |       **-** |  **1221.7 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 6,541.72 μs | 89,166.36 μs | 4,887.509 μs |  0.78 |    0.52 |   7.8125 |       - |  141.13 KB |        0.12 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,338.60 μs** |     **23.39 μs** |     **1.282 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,090.95 μs |     18.12 μs |     0.993 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,346.96 μs** |    **144.18 μs** |     **7.903 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,314.28 μs |    245.47 μs |    13.455 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,337.35 μs** |     **59.77 μs** |     **3.276 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,308.33 μs |  1,335.01 μs |    73.177 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,342.10 μs** |    **102.63 μs** |     **5.626 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,096.82 μs |     56.34 μs |     3.088 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.966 ms** |  **1.760 ms** | **0.0965 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.529 ms | 29.167 ms | 1.5987 ms | 0.005 |  600.91 KB |        8.05 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.586 ms** | **80.641 ms** | **4.4202 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11.713 ms | 26.041 ms | 1.4274 ms | 0.004 |  785.45 KB |        3.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.566 ms** | **32.795 ms** | **1.7976 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13.731 ms | 24.553 ms | 1.3458 ms | 0.004 | 1007.27 KB |        1.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.164 ms** | **13.997 ms** | **0.7672 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.032 ms | 17.083 ms | 0.9364 ms | 0.004 | 2770.33 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.195 ms** |  **3.736 ms** | **0.2048 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.545 ms | 32.355 ms | 1.7735 ms | 0.002 |  185.94 KB |       77.27 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.932 ms** | **21.687 ms** | **1.1887 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.268 ms |  9.427 ms | 0.5167 ms | 0.002 |  186.52 KB |       44.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.132 ms** |  **9.014 ms** | **0.4941 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.218 ms | 25.195 ms | 1.3810 ms | 0.002 |  184.72 KB |       76.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.393 ms** |  **8.121 ms** | **0.4452 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.046 ms |  3.771 ms | 0.2067 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.755 μs |   1.846 μs |  0.1012 μs | 25.769 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.149 μs |   5.541 μs |  0.3037 μs | 12.102 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.289 μs |   3.604 μs |  0.1975 μs | 10.229 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.797 μs |   1.076 μs |  0.0590 μs | 26.820 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.976 μs |  69.556 μs |  3.8126 μs |  8.825 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.230 μs |   1.753 μs |  0.0961 μs | 20.247 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.813 μs |   3.753 μs |  0.2057 μs | 17.823 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.639 μs | 212.436 μs | 11.6443 μs | 20.037 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.436 μs |   1.530 μs |  0.0839 μs |  4.479 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.460 μs |   7.035 μs |  0.3856 μs | 10.450 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,419.2 ns |  2,588.6 ns | 141.89 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,334.2 ns |  2,281.1 ns | 125.03 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,368.3 ns |    276.9 ns |  15.18 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,025.0 ns |  3,378.8 ns | 185.20 ns |  0.74 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    748.7 ns |    421.3 ns |  23.09 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,215.3 ns | 10,302.5 ns | 564.71 ns |  9.90 |    0.27 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,063.7 ns |  2,114.5 ns | 115.90 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,855.2 ns |  1,464.1 ns |  80.25 ns |  0.95 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.160 μs |   2.007 μs |  0.1100 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   528.423 μs | 434.218 μs | 23.8010 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.925 μs |   6.163 μs |  0.3378 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,686.159 μs | 286.068 μs | 15.6804 μs |    1280 B |


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