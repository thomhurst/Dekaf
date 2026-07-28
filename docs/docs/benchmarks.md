---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 18:03 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.22 | 0.90–1.28 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.47 | 0.90–1.93 | 70% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.24 | 0.66–1.36 | 56% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.09 | 1.29–2.22 | 44% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 2 | 0.09 | 0.09–0.10 | 12% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 2 | 0.33 | 0.33–0.33 | 1% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.10 | 1.04–1.21 | 15% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.09 | 0.90–1.13 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.97–1.05 | 7% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.98 | 0.91–1.03 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.44 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 1.02–1.11 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.45–0.48 | 5% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,099.9 μs** |    **62.74 μs** |  **37.34 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,676.2 μs |    15.57 μs |  10.30 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,362.6 μs** |    **79.63 μs** |  **52.67 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,776.9 μs |    35.72 μs |  18.68 μs |  0.51 |    0.00 |        - |       - |   51799 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,229.3 μs** |    **61.68 μs** |  **36.71 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,701.2 μs |    47.47 μs |  31.40 μs |  0.43 |    0.01 |        - |       - |    6277 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,645.0 μs** |   **191.12 μs** | **113.73 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,959.0 μs |   371.39 μs | 194.24 μs |  1.02 |    0.02 |        - |       - |   71444 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.9 μs** |     **5.02 μs** |   **3.32 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    148.7 μs |    20.20 μs |  13.36 μs |  1.14 |    0.10 |        - |       - |     213 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,282.8 μs** |    **26.00 μs** |  **15.47 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,159.3 μs |   134.01 μs |  70.09 μs |  0.90 |    0.05 |        - |       - |    2141 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,086.8 μs** |     **8.63 μs** |   **5.14 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121587 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,059.2 μs |   116.52 μs |  77.07 μs |  0.97 |    0.07 |        - |       - |    1978 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,957.6 μs** |   **163.53 μs** | **108.16 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1216012 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,987.0 μs | 1,055.91 μs | 698.42 μs |  0.91 |    0.06 |        - |       - |   19158 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,497.4 μs** |     **8.50 μs** |   **5.62 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,596.6 μs |     6.61 μs |   3.46 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,497.2 μs** |    **13.12 μs** |   **7.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,601.5 μs |    15.84 μs |  10.48 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,508.2 μs** |    **17.75 μs** |  **11.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,610.7 μs |     8.63 μs |   5.14 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,503.3 μs** |    **11.54 μs** |   **7.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,618.1 μs |    18.63 μs |  12.32 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **127.1 μs** |    **54.42 μs** |  **28.46 μs** |   **125.6 μs** |  **1.04** |    **0.31** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   114.3 μs |    37.22 μs |  19.47 μs |   112.0 μs |  0.94 |    0.25 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **158.1 μs** |    **67.40 μs** |  **35.25 μs** |   **147.2 μs** |  **1.04** |    **0.31** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   141.6 μs |    33.96 μs |  15.08 μs |   142.7 μs |  0.93 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,102.5 μs** |   **609.95 μs** | **319.02 μs** |   **996.2 μs** |  **1.07** |    **0.39** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   733.0 μs |   218.77 μs |  97.13 μs |   677.4 μs |  0.71 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,076.5 μs** |    **41.51 μs** |  **14.80 μs** | **1,069.9 μs** |  **1.00** |    **0.02** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,392.5 μs | 1,247.77 μs | 554.02 μs |   978.8 μs |  1.29 |    0.48 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,526.8 ns** |  **20.93 ns** |  **12.45 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   474.5 ns |  27.68 ns |  16.47 ns |  0.09 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,742.5 ns** |  **85.12 ns** |  **44.52 ns** |  **1.00** |    **0.02** | **0.1450** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,216.4 ns | 184.72 ns | 109.92 ns |  0.33 |    0.03 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 466.25 ns | 3.792 ns | 0.985 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  28.89 ns | 0.160 ns | 0.025 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.72 ns | 0.348 ns | 0.054 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.50 ns | 0.461 ns | 0.071 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.080 μs** | **0.0030 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **1**       | **2.096 μs** | **0.0123 μs** | **0.0019 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.398 μs** | **0.0047 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **9**       | **2.382 μs** | **0.0027 μs** | **0.0004 μs** |         **-** |
| **WriteRequest** | **10**      | **2.401 μs** | **0.0017 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **11**      | **2.392 μs** | **0.0097 μs** | **0.0015 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **105.01 ns** | **0.161 ns** | **0.025 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.88 ns | 0.254 ns | 0.039 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.96 ns** | **0.593 ns** | **0.154 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.69 ns | 0.176 ns | 0.027 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.1 ns | 1.72 ns | 0.90 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,269.9 ns | 3.63 ns | 1.90 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,604.9 ns | 8.44 ns | 4.41 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,262.9 ns | 3.37 ns | 2.23 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,901.0 ns | 3.75 ns | 2.23 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,974.5 ns | 7.91 ns | 4.71 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,053.1 ns | 9.07 ns | 4.74 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,840.6 ns | 4.06 ns | 2.12 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.0 ns | 2.01 ns | 1.20 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.3 ns | 5.00 ns | 3.31 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   775.3 ns | 1.79 ns | 0.93 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   817.5 ns | 1.42 ns | 0.74 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   170.5 ns | 0.48 ns | 0.32 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,689.4 ns | 8.37 ns | 4.98 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,377.4 ns | 1.43 ns | 0.85 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,524.375 ns | 73.5634 ns | 43.7764 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    10.686 ns |  0.0900 ns |  0.0471 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    13.891 ns |  0.0925 ns |  0.0551 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    24.483 ns |  0.4189 ns |  0.2771 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    29.636 ns |  1.3027 ns |  0.8616 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.201 ns |  0.1223 ns |  0.0728 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    92.243 ns |  1.3697 ns |  0.9060 ns |  1.00 |    0.01 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    50.847 ns |  0.3275 ns |  0.2166 ns |  0.55 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     297.1 ns |   0.27 ns |   0.14 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,704.2 ns |  86.72 ns |  57.36 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     210.5 ns |   0.57 ns |   0.30 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,516.9 ns | 232.27 ns | 138.22 ns |      - |      80 B |

---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio
- **Confidence**: `⚠ Low` when latest `RatioSD > 0.30` or rolling run spread exceeds 30%
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*