---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 17:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,144.02 μs** |    **353.66 μs** |    **19.385 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,516.79 μs |  3,162.41 μs |   173.343 μs |  0.25 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,201.43 μs** |  **1,132.26 μs** |    **62.063 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,457.70 μs |    852.67 μs |    46.738 μs |  0.34 |    0.01 |  15.6250 |       - |  341.19 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,651.38 μs** |    **640.67 μs** |    **35.117 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,762.63 μs |  2,318.00 μs |   127.058 μs |  0.27 |    0.02 |        - |       - |   38.16 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,559.00 μs** |  **1,182.10 μs** |    **64.795 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,060.59 μs |  8,888.96 μs |   487.234 μs |  0.61 |    0.04 |  15.6250 |       - |  385.05 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **117.62 μs** |     **38.38 μs** |     **2.104 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     92.38 μs |    245.23 μs |    13.442 μs |  0.79 |    0.10 |        - |       - |    8.18 KB |        0.24 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,089.57 μs** |  **3,442.83 μs** |   **188.713 μs** |  **1.02** |    **0.22** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    852.55 μs |  1,021.88 μs |    56.013 μs |  0.80 |    0.13 |        - |       - |   51.88 KB |        0.15 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **997.09 μs** |     **45.46 μs** |     **2.492 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.46 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    313.62 μs |    880.30 μs |    48.252 μs |  0.31 |    0.04 |   0.9766 |       - |    89.7 KB |        0.73 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,820.02 μs** | **35,952.21 μs** | **1,970.662 μs** |  **1.04** |    **0.31** |  **74.2188** |       **-** | **1225.75 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,638.55 μs |  2,984.45 μs |   163.588 μs |  0.43 |    0.10 |        - |       - |  667.82 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,408.77 μs** |    **150.18 μs** |     **8.232 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,362.61 μs |    342.22 μs |    18.758 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,396.16 μs** |     **53.37 μs** |     **2.925 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,102.80 μs |     11.13 μs |     0.610 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,400.71 μs** |     **38.86 μs** |     **2.130 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,103.63 μs |     45.63 μs |     2.501 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.99 μs** |    **120.07 μs** |     **6.581 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,108.03 μs |    108.59 μs |     5.952 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **104.3 μs** |    **599.5 μs** |  **32.86 μs** |    **85.45 μs** |  **1.06** |    **0.38** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   180.3 μs |    460.3 μs |  25.23 μs |   183.78 μs |  1.83 |    0.48 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **172.5 μs** |    **982.6 μs** |  **53.86 μs** |   **168.41 μs** |  **1.07** |    **0.42** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   193.7 μs |    449.5 μs |  24.64 μs |   192.89 μs |  1.20 |    0.36 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **827.7 μs** |  **3,643.4 μs** | **199.71 μs** |   **723.57 μs** |  **1.04** |    **0.29** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,125.8 μs |  2,592.3 μs | 142.09 μs | 1,047.90 μs |  1.41 |    0.30 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,654.9 μs** | **14,298.9 μs** | **783.77 μs** | **1,496.92 μs** |  **1.16** |    **0.69** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,779.4 μs | 14,418.6 μs | 790.33 μs | 1,328.08 μs |  1.25 |    0.71 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **705.9 ns** | **1,312.8 ns** |  **71.96 ns** |  **1.01** |    **0.12** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,735.4 ns |   187.2 ns |  10.26 ns |  2.47 |    0.21 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,373.4 ns** | **4,373.4 ns** | **239.72 ns** |  **1.02** |    **0.21** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,146.0 ns | 3,020.1 ns | 165.54 ns |  2.33 |    0.34 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.158 μs |   1.277 μs | 0.0700 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.214 μs |   2.976 μs | 0.1631 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 12.220 μs |  46.008 μs | 2.5219 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.762 μs |   4.228 μs | 0.2317 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.120 μs |   1.622 μs | 0.0889 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.910 μs |   3.219 μs | 0.1764 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.858 μs |   2.067 μs | 0.1133 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.899 μs |   4.989 μs | 0.2735 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.040 μs |   3.577 μs | 0.1961 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.211 μs |   1.743 μs | 0.0956 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 39.969 μs | 102.255 μs | 5.6050 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.510 μs |  32.685 μs | 1.7916 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.642 μs |  26.812 μs | 1.4697 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.335 μs |  24.252 μs | 1.3293 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,180.78 ns | 552.892 ns | 30.306 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.51 ns |   0.130 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.49 ns |   7.611 ns |  0.417 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.68 ns |   2.851 ns |  0.156 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.08 ns |  17.912 ns |  0.982 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.88 ns |   3.177 ns |  0.174 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    122.69 ns | 218.282 ns | 11.965 ns |  1.01 |    0.12 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.42 ns |  16.765 ns |  0.919 ns |  0.62 |    0.06 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.780 μs |  26.694 μs |  1.4632 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 523.838 μs | 344.168 μs | 18.8650 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.722 μs |  16.885 μs |  0.9255 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 275.880 μs | 572.923 μs | 31.4038 μs |      80 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*