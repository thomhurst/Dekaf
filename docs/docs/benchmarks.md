---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-04-27 02:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,098.08 μs** |    **72.587 μs** |  **48.012 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,374.25 μs |    97.069 μs |  64.205 μs |  0.23 |    0.01 |        - |       - |   32.04 KB |        0.30 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,405.36 μs** |    **56.845 μs** |  **33.827 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,330.30 μs |    73.779 μs |  43.905 μs |  0.31 |    0.01 |  15.6250 |       - |  309.61 KB |        0.29 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,363.07 μs** |   **126.105 μs** |  **83.411 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,257.67 μs |    33.602 μs |  19.996 μs |  0.20 |    0.00 |        - |       - |   34.43 KB |        0.18 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,635.29 μs** | **1,111.736 μs** | **581.459 μs** |  **1.00** |    **0.06** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,305.18 μs |   136.534 μs |  71.410 μs |  0.50 |    0.02 |  15.6250 |       - |  343.92 KB |        0.18 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.22 μs** |     **7.049 μs** |   **3.687 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.06 μs |     5.628 μs |   3.349 μs |  0.40 |    0.02 |   0.4883 |  0.2441 |   16.06 KB |        0.38 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,504.62 μs** |    **41.357 μs** |  **27.355 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **421.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    713.09 μs |    81.061 μs |  53.617 μs |  0.47 |    0.03 |   3.9063 |       - |   97.43 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    300.24 μs |    27.206 μs |  17.995 μs |     ? |       ? |   6.8359 |  5.8594 |  205.06 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,868.22 μs |   348.100 μs | 182.063 μs |     ? |       ? |   7.8125 |       - |  207.83 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,433.40 μs** |    **31.155 μs** |  **20.607 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,355.53 μs |    21.456 μs |  14.192 μs |  0.25 |    0.00 |        - |       - |    1.29 KB |        1.09 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,427.32 μs** |    **16.165 μs** |  **10.692 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,394.32 μs |    33.633 μs |  22.246 μs |  0.26 |    0.00 |        - |       - |    1.29 KB |        1.09 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,423.52 μs** |     **3.465 μs** |   **2.062 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,228.62 μs |   184.960 μs | 122.340 μs |  0.23 |    0.02 |        - |       - |    1.29 KB |        0.63 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.39 μs** |     **5.948 μs** |   **3.540 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.71 μs |     1.107 μs |   0.732 μs |  0.21 |    0.00 |        - |       - |    1.29 KB |        0.63 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-IUAFYC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-IUAFYC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Gen0      | Gen1      | Gen2      | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|----------:|----------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.156 ms** |  **6.811 ms** | **1.7689 ms** | **1.000** |         **-** |         **-** |         **-** |    **76592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.660 ms |  5.837 ms | 1.5158 ms | 0.005 |         - |         - |         - |  9010288 B |      117.64 |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.595 ms** | **13.512 ms** | **3.5091 ms** | **1.000** |         **-** |         **-** |         **-** |   **256592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.535 ms |  3.912 ms | 1.0159 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 |  9462392 B |       36.88 |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.210 ms** |  **4.668 ms** | **0.7224 ms** | **1.000** |         **-** |         **-** |         **-** |   **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.447 ms | 14.836 ms | 3.8528 ms | 0.006 | 1000.0000 | 1000.0000 | 1000.0000 |  9666176 B |       15.67 |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.486 ms** |  **2.761 ms** | **0.7170 ms** | **1.000** |         **-** |         **-** |         **-** |  **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.956 ms |  9.192 ms | 2.3870 ms | 0.006 | 1000.0000 | 1000.0000 | 1000.0000 | 14354888 B |        5.92 |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.369 ms** |  **6.478 ms** | **1.6824 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.544 ms |  4.958 ms | 1.2876 ms | 0.002 |         - |         - |         - |   368008 B |          NA |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.517 ms** |  **3.879 ms** | **1.0074 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.169 ms |  1.606 ms | 0.2485 ms | 0.002 |         - |         - |         - |   828608 B |          NA |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.093 ms** |  **4.493 ms** | **1.1669 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.119 ms |  1.045 ms | 0.1617 ms | 0.002 |         - |         - |         - |   826072 B |          NA |
|                      |            |              |             |              |           |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.111 ms** |  **2.789 ms** | **0.7242 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.625 ms |  4.823 ms | 0.7464 ms | 0.002 |         - |         - |         - |  2401192 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 19.971 μs |  9.0525 μs | 5.3870 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.391 μs |  0.2741 μs | 0.1434 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.957 μs |  0.2287 μs | 0.1361 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 34.247 μs | 11.9164 μs | 7.0913 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 14.809 μs |  9.6145 μs | 5.7214 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 25.411 μs |  9.5194 μs | 5.6649 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.399 μs |  2.6425 μs | 1.7478 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;           |  3.805 μs |  0.1557 μs | 0.0814 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  9.181 μs |  0.1890 μs | 0.0988 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,860.2 ns |    240.12 ns |   158.82 ns |  0.48 |    0.04 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,610.3 ns |    100.09 ns |    59.56 ns |  0.42 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,244.9 ns |     31.39 ns |    18.68 ns |  0.32 |    0.01 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,406.0 ns |    108.24 ns |    71.59 ns |  0.62 |    0.02 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    734.6 ns |    105.66 ns |    55.26 ns |  0.19 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,056.1 ns | 10,218.95 ns | 6,081.13 ns | 12.44 |    1.51 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,863.5 ns |    110.01 ns |    57.54 ns |  1.00 |    0.02 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,213.3 ns |     70.80 ns |    42.13 ns |  0.83 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.055 μs |  0.4001 μs |  0.2646 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   931.829 μs | 33.3822 μs | 22.0803 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.924 μs |  0.1602 μs |  0.1059 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,733.237 μs | 95.2990 μs | 63.0344 μs |         - |


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