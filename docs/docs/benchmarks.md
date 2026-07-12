---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 03:00 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,029.0 μs** |    **419.70 μs** |  **23.01 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,509.1 μs |  1,532.63 μs |  84.01 μs |  0.25 |    0.01 |       - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,210.5 μs** |  **1,367.94 μs** |  **74.98 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,496.8 μs |  2,531.59 μs | 138.77 μs |  0.35 |    0.02 |  7.8125 |       - |  340.98 KB |        0.32 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,495.4 μs** |    **251.76 μs** |  **13.80 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,499.2 μs |  1,152.44 μs |  63.17 μs |  0.23 |    0.01 |       - |       - |   38.09 KB |        0.20 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,878.4 μs** |  **2,972.22 μs** | **162.92 μs** |  **1.00** |    **0.02** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 7,298.4 μs |  3,923.46 μs | 215.06 μs |  0.82 |    0.02 |       - |       - |  368.85 KB |        0.19 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **116.0 μs** |      **6.29 μs** |   **0.34 μs** |  **1.00** |    **0.00** |  **1.3428** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   101.0 μs |     27.24 μs |   1.49 μs |  0.87 |    0.01 |       - |       - |    6.39 KB |        0.19 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,161.8 μs** |  **2,676.94 μs** | **146.73 μs** |  **1.01** |    **0.16** | **13.6719** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   952.7 μs |    541.78 μs |  29.70 μs |  0.83 |    0.10 |       - |       - |   53.09 KB |        0.16 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **741.4 μs** |     **89.16 μs** |   **4.89 μs** |  **1.00** |    **0.01** |  **4.8828** |       **-** |  **122.03 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   301.0 μs |  1,242.96 μs |  68.13 μs |  0.41 |    0.08 |       - |       - |   92.54 KB |        0.76 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,501.0 μs** |  **2,136.70 μs** | **117.12 μs** |  **1.00** |    **0.02** | **48.8281** |       **-** | **1220.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 3,034.6 μs | 11,906.71 μs | 652.65 μs |  0.40 |    0.08 |       - |       - |  982.63 KB |        0.80 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,407.7 μs** |    **448.05 μs** |  **24.56 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,403.4 μs |    543.39 μs |  29.79 μs |  0.26 |    0.00 |       - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,347.9 μs** |    **103.08 μs** |   **5.65 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,390.2 μs |  1,250.53 μs |  68.55 μs |  0.26 |    0.01 |       - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,376.7 μs** |     **86.67 μs** |   **4.75 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,362.7 μs |    640.94 μs |  35.13 μs |  0.25 |    0.01 |       - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |            |              |           |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,371.0 μs** |    **211.46 μs** |  **11.59 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,359.5 μs |  1,301.35 μs |  71.33 μs |  0.25 |    0.01 |       - |       - |    1.08 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev      | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **156.0 μs** |    **569.46 μs** |    **31.21 μs** |   **141.4 μs** |  **1.02** |    **0.24** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   148.8 μs |    553.47 μs |    30.34 μs |   140.0 μs |  0.98 |    0.23 |   39.98 KB |        0.62 |
|                      |              |             |            |              |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **243.1 μs** |    **838.00 μs** |    **45.93 μs** |   **225.6 μs** |  **1.02** |    **0.23** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   167.6 μs |     90.98 μs |     4.99 μs |   164.9 μs |  0.70 |    0.11 |  215.77 KB |        0.90 |
|                      |              |             |            |              |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,057.8 μs** |  **1,764.80 μs** |    **96.73 μs** | **1,019.8 μs** |  **1.01** |    **0.11** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   988.9 μs |  1,589.89 μs |    87.15 μs |   953.6 μs |  0.94 |    0.10 |  476.66 KB |        0.73 |
|                      |              |             |            |              |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,526.2 μs** |  **9,104.87 μs** |   **499.07 μs** | **1,248.7 μs** |  **1.06** |    **0.40** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,834.0 μs | 19,075.85 μs | 1,045.61 μs | 1,231.2 μs |  1.28 |    0.72 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |---------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         | **1.113 μs** |  **5.6916 μs** | **0.3120 μs** |  **1.05** |    **0.34** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1.585 μs |  0.2050 μs | 0.0112 μs |  1.49 |    0.32 |     452 B |        0.70 |
|                      |             |          |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1.462 μs** |  **2.1747 μs** | **0.1192 μs** |  **1.00** |    **0.10** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3.039 μs | 12.2347 μs | 0.6706 μs |  2.09 |    0.43 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.142 μs |  0.8622 μs | 0.0473 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.108 μs |  2.9607 μs | 0.1623 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.663 μs |  2.0201 μs | 0.1107 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.403 μs |  5.8093 μs | 0.3184 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.372 μs |  1.2640 μs | 0.0693 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.760 μs |  5.2165 μs | 0.2859 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.666 μs |  2.1380 μs | 0.1172 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.903 μs |  1.6352 μs | 0.0896 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.957 μs |  1.0946 μs | 0.0600 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.331 μs |  1.7547 μs | 0.0962 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.213 μs | 35.6488 μs | 1.9540 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 39.301 μs | 65.9383 μs | 3.6143 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.602 μs |  1.7432 μs | 0.0956 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.974 μs |  6.9108 μs | 0.3788 μs |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,403.36 ns | 398.349 ns | 21.835 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.77 ns |   0.217 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.23 ns |   0.289 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.56 ns |   4.294 ns |  0.235 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     36.11 ns |   6.359 ns |  0.349 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.81 ns |   1.342 ns |  0.074 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    126.54 ns |  26.282 ns |  1.441 ns |  1.00 |    0.01 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.18 ns |   0.593 ns |  0.033 ns |  0.63 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev     | Median     | Allocated |
|------------------------ |-----------:|-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  20.865 μs |   308.086 μs |  16.887 μs |  11.151 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 669.787 μs | 3,928.986 μs | 215.361 μs | 547.000 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.665 μs |    19.421 μs |   1.065 μs |   8.071 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 241.047 μs |   126.456 μs |   6.931 μs | 244.480 μs |      80 B |


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