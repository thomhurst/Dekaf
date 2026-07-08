---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 17:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,031.36 μs** |    **341.75 μs** |    **18.732 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,436.30 μs |  2,529.09 μs |   138.628 μs |  0.24 |    0.02 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,133.62 μs** |  **1,203.40 μs** |    **65.963 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1063.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,315.70 μs |  1,004.56 μs |    55.063 μs |  0.32 |    0.01 |  15.6250 |       - |  339.67 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,663.03 μs** |  **1,059.51 μs** |    **58.075 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,432.82 μs |  2,859.31 μs |   156.729 μs |  0.22 |    0.02 |        - |       - |   36.47 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,322.20 μs** |  **8,433.89 μs** |   **462.290 μs** |  **1.00** |    **0.05** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,705.75 μs |  3,389.98 μs |   185.816 μs |  0.59 |    0.03 |  15.6250 |       - |  359.08 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.95 μs** |    **181.48 μs** |     **9.948 μs** |  **1.00** |    **0.09** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     82.01 μs |    147.80 μs |     8.101 μs |  0.64 |    0.07 |        - |       - |    4.21 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,380.39 μs** |  **2,605.05 μs** |   **142.792 μs** |  **1.01** |    **0.13** |  **19.5313** |       **-** |  **335.91 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    781.68 μs |  2,660.57 μs |   145.835 μs |  0.57 |    0.11 |        - |       - |   42.43 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **962.23 μs** |    **140.16 μs** |     **7.682 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.60 μs |    200.75 μs |    11.004 μs |  0.23 |    0.01 |        - |       - |   91.34 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,461.08 μs** | **33,927.96 μs** | **1,859.706 μs** |  **1.04** |    **0.30** |  **74.2188** |       **-** | **1224.84 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,498.88 μs |  2,300.83 μs |   126.116 μs |  0.31 |    0.07 |   7.8125 |       - |  931.16 KB |        0.76 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,410.13 μs** |    **162.18 μs** |     **8.890 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,104.94 μs |     70.98 μs |     3.891 μs |  0.20 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,418.83 μs** |    **211.18 μs** |    **11.576 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,106.97 μs |     53.26 μs |     2.919 μs |  0.20 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,423.69 μs** |    **201.42 μs** |    **11.040 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,234.91 μs |    282.47 μs |    15.483 μs |  0.23 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,447.30 μs** |     **91.16 μs** |     **4.997 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,301.27 μs |    547.84 μs |    30.029 μs |  0.24 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,985.93 μs** | **42,798.70 μs** | **2,345.941 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,907.44 μs | 74,788.70 μs | 4,099.421 μs | 0.006 |    0.00 |  619112 B |        8.10 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166,948.20 μs** |  **8,359.78 μs** |   **458.228 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,511.63 μs | 27,598.85 μs | 1,512.786 μs | 0.005 |    0.00 |  812352 B |        3.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,908.53 μs** |  **7,056.64 μs** |   **386.798 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18,149.70 μs | 56,036.62 μs | 3,071.556 μs | 0.006 |    0.00 | 1030408 B |        1.67 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,823.22 μs** | **16,534.36 μs** |   **906.304 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,099.43 μs | 31,073.57 μs | 1,703.247 μs | 0.005 |    0.00 | 2842384 B |        1.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **32.99 μs** |     **23.12 μs** |     **1.267 μs** |  **1.00** |    **0.05** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        28.50 μs |     54.87 μs |     3.008 μs |  0.86 |    0.08 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **28.96 μs** |     **61.11 μs** |     **3.350 μs** |  **1.01** |    **0.15** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        36.97 μs |    228.19 μs |    12.508 μs |  1.29 |    0.40 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **31.04 μs** |     **33.62 μs** |     **1.843 μs** |  **1.00** |    **0.07** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        29.57 μs |     66.08 μs |     3.622 μs |  0.95 |    0.11 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **38.66 μs** |    **123.18 μs** |     **6.752 μs** |  **1.02** |    **0.23** |    **2704 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        27.80 μs |     25.26 μs |     1.385 μs |  0.74 |    0.12 |    2784 B |        1.03 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.130 μs |  53.2860 μs |  2.9208 μs | 26.581 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 17.527 μs | 193.0704 μs | 10.5828 μs | 11.432 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.886 μs |   4.0631 μs |  0.2227 μs |  8.847 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.793 μs |   6.6245 μs |  0.3631 μs |  8.966 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.629 μs |   2.4372 μs |  0.1336 μs | 11.662 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.255 μs |   2.8708 μs |  0.1574 μs | 13.335 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 15.321 μs |  62.5711 μs |  3.4297 μs | 13.825 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.161 μs |   0.9031 μs |  0.0495 μs | 27.162 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.028 μs |   1.0428 μs |  0.0572 μs |  9.062 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.115 μs |  76.4206 μs |  4.1889 μs | 27.357 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 41.208 μs | 277.1806 μs | 15.1932 μs | 34.946 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.459 μs |  40.3921 μs |  2.2140 μs | 34.446 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.480 μs |  17.5482 μs |  0.9619 μs |  5.049 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.418 μs |  13.2772 μs |  0.7278 μs | 13.214 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,200.3 ns |   4,235.2 ns |    232.1 ns |  2,264.0 ns |  0.47 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,627.8 ns |   7,167.5 ns |    392.9 ns |  1,447.5 ns |  0.35 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  2,624.0 ns |   8,672.8 ns |    475.4 ns |  2,434.0 ns |  0.56 |    0.10 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,844.7 ns |  10,505.9 ns |    575.9 ns |  2,574.0 ns |  0.61 |    0.11 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    776.5 ns |   1,895.9 ns |    103.9 ns |    716.5 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 62,869.0 ns | 473,127.0 ns | 25,933.7 ns | 49,197.0 ns | 13.45 |    4.90 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,695.3 ns |   7,054.5 ns |    386.7 ns |  4,508.0 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  6,827.3 ns |  40,033.7 ns |  2,194.4 ns |  7,549.0 ns |  1.46 |    0.42 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error       | StdDev     | Allocated |
|------------------------ |-----------:|------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.338 μs |   2.8151 μs |  0.1543 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 525.286 μs | 454.7599 μs | 24.9269 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.031 μs |   0.7595 μs |  0.0416 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 228.321 μs |  15.8998 μs |  0.8715 μs |      80 B |


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