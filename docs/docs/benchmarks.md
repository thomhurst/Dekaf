---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 17:00 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,192.6 μs** |    **656.67 μs** |    **35.99 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,522.5 μs |  2,667.19 μs |   146.20 μs |  0.25 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,373.8 μs** |  **1,489.83 μs** |    **81.66 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,012.8 μs |  5,043.48 μs |   276.45 μs |  0.41 |    0.03 |  15.6250 |       - |   341.3 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,415.8 μs** |  **2,103.50 μs** |   **115.30 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,737.7 μs |  3,211.91 μs |   176.06 μs |  0.27 |    0.02 |        - |       - |   38.13 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,367.8 μs** |  **5,396.98 μs** |   **295.83 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,275.5 μs | 11,360.50 μs |   622.71 μs |  0.75 |    0.05 |  15.6250 |       - |  421.82 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.8 μs** |     **45.00 μs** |     **2.47 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    136.4 μs |     68.71 μs |     3.77 μs |  1.04 |    0.03 |        - |       - |    4.49 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,334.8 μs** |    **130.34 μs** |     **7.14 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,215.7 μs |  3,264.10 μs |   178.92 μs |  0.91 |    0.12 |        - |       - |   43.82 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,045.8 μs** |     **24.45 μs** |     **1.34 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    827.1 μs |  1,027.39 μs |    56.31 μs |  0.79 |    0.05 |        - |       - |    7.99 KB |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,609.4 μs** | **30,090.38 μs** | **1,649.36 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1225.74 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,153.0 μs |  9,452.61 μs |   518.13 μs |  0.87 |    0.15 |        - |       - |   88.73 KB |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.3 μs** |     **19.65 μs** |     **1.08 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.2 μs |     42.48 μs |     2.33 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.9 μs** |    **214.91 μs** |    **11.78 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,280.8 μs |  3,821.78 μs |   209.48 μs |  0.24 |    0.03 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,412.9 μs** |    **177.82 μs** |     **9.75 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,391.3 μs |    554.14 μs |    30.37 μs |  0.26 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.0 μs** |     **94.38 μs** |     **5.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,378.7 μs |    653.49 μs |    35.82 μs |  0.25 |    0.01 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **139.4 μs** |    **708.2 μs** |  **38.82 μs** |   **152.4 μs** |  **1.06** |    **0.40** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   185.7 μs |    828.3 μs |  45.40 μs |   193.9 μs |  1.42 |    0.50 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **168.9 μs** |    **694.6 μs** |  **38.07 μs** |   **190.3 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   169.7 μs |    183.5 μs |  10.06 μs |   169.9 μs |  1.05 |    0.24 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,100.3 μs** |  **4,567.5 μs** | **250.36 μs** | **1,144.9 μs** |  **1.04** |    **0.30** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,173.1 μs |    167.1 μs |   9.16 μs | 1,173.1 μs |  1.11 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,471.0 μs** | **12,920.2 μs** | **708.20 μs** | **1,071.3 μs** |  **1.14** |    **0.62** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,467.8 μs |    491.1 μs |  26.92 μs | 1,464.5 μs |  1.14 |    0.37 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **882.6 ns** |   **517.7 ns** |  **28.38 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,912.7 ns |   355.0 ns |  19.46 ns |  2.17 |    0.06 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,407.5 ns** |   **793.2 ns** |  **43.48 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,237.3 ns | 5,906.4 ns | 323.75 ns |  2.30 |    0.21 | 0.1000 |    2257 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.443 μs |  4.950 μs | 0.2713 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.376 μs |  3.114 μs | 0.1707 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.997 μs | 27.625 μs | 1.5142 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.130 μs |  2.839 μs | 0.1556 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.623 μs |  2.936 μs | 0.1609 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  9.958 μs |  6.288 μs | 0.3447 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 10.703 μs |  2.353 μs | 0.1290 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.577 μs |  1.459 μs | 0.0800 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.183 μs | 50.300 μs | 2.7571 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 18.052 μs | 77.841 μs | 4.2667 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 24.473 μs | 56.139 μs | 3.0771 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.277 μs | 40.362 μs | 2.2124 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.353 μs |  8.184 μs | 0.4486 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.046 μs |  3.981 μs | 0.2182 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|--------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,907.558 ns | 1,577.7165 ns | 86.4800 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |               |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.073 ns |     0.0691 ns |  0.0038 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    14.945 ns |     0.8460 ns |  0.0464 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    29.891 ns |     0.0904 ns |  0.0050 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    24.866 ns |     7.9086 ns |  0.4335 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.391 ns |     3.0417 ns |  0.1667 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |               |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    81.057 ns |     8.3273 ns |  0.4564 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    61.115 ns |     0.1420 ns |  0.0078 ns |  0.75 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.453 μs |   8.623 μs |  0.4726 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 413.756 μs | 509.024 μs | 27.9013 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.266 μs |   5.731 μs |  0.3141 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 180.237 μs | 403.915 μs | 22.1399 μs |      80 B |


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