---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-30 10:45 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.96 | 0.89–1.05 | 17% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.03 | 1.00–1.24 | 23% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.69 | 0.65–0.86 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.25 | 1.05–1.60 | 44% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.09–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.36 | 0.32–0.39 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.04 | 0.96–1.13 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.97–1.09 | 11% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.01 | 4% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.97 | 0.95–0.99 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.44 | 0% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.52 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.40–0.43 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.05 | 1.00–1.08 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,105.2 μs** |    **98.31 μs** |  **65.02 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,679.3 μs |    20.06 μs |  13.27 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,328.2 μs** |   **100.34 μs** |  **66.37 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,820.7 μs |    73.98 μs |  48.93 μs |  0.52 |    0.01 |        - |       - |   51580 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,306.9 μs** |    **55.38 μs** |  **32.96 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,730.1 μs |    18.01 μs |   9.42 μs |  0.43 |    0.00 |        - |       - |    6100 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,491.7 μs** |   **240.28 μs** | **158.93 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,781.3 μs | 1,152.05 μs | 762.01 μs |  1.02 |    0.06 |        - |       - |   77501 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.8 μs** |    **26.93 μs** |  **17.81 μs** |  **1.02** |    **0.21** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    133.2 μs |    28.27 μs |  18.70 μs |  1.04 |    0.22 |        - |       - |     143 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,400.3 μs** |    **30.10 μs** |  **19.91 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,400.3 μs |    74.38 μs |  38.90 μs |  1.00 |    0.03 |        - |       - |    2015 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,045.2 μs** |    **11.45 μs** |   **6.82 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121505 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,059.0 μs |   100.27 μs |  66.32 μs |  1.01 |    0.06 |        - |       - |    1981 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,384.0 μs** |   **133.89 μs** |  **79.68 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215052 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,109.1 μs |   348.73 μs | 230.67 μs |  0.97 |    0.02 |        - |       - |   16948 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,510.6 μs** |    **18.49 μs** |  **12.23 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,595.4 μs |    12.04 μs |   7.17 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,497.0 μs** |    **17.25 μs** |   **9.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1394 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,602.0 μs |    10.10 μs |   6.68 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.45 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,526.7 μs** |    **26.82 μs** |  **15.96 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,620.1 μs |    21.17 μs |  14.00 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,530.9 μs** |    **32.00 μs** |  **21.16 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,621.8 μs |    24.95 μs |  16.50 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **124.7 μs** |    **48.51 μs** |  **25.37 μs** |   **125.8 μs** |  **1.04** |    **0.28** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   119.2 μs |    25.52 μs |  13.35 μs |   121.4 μs |  0.99 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **162.8 μs** |    **74.12 μs** |  **38.77 μs** |   **158.7 μs** |  **1.05** |    **0.34** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   171.4 μs |    54.03 μs |  28.26 μs |   180.8 μs |  1.11 |    0.30 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,052.5 μs** |   **565.10 μs** | **295.56 μs** |   **874.4 μs** |  **1.06** |    **0.37** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   736.3 μs |   105.33 μs |  37.56 μs |   733.1 μs |  0.74 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,105.9 μs** |    **99.54 μs** |  **35.50 μs** | **1,097.6 μs** |  **1.00** |    **0.04** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,248.3 μs | 1,176.85 μs | 522.53 μs |   956.3 μs |  1.13 |    0.44 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,550.5 ns** |    **14.36 ns** |   **8.55 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   499.0 ns |    46.92 ns |  31.03 ns |  0.09 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,260.8 ns** | **1,629.18 ns** | **969.50 ns** |  **1.14** |    **0.63** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,194.8 ns |    52.55 ns |  27.48 ns |  0.42 |    0.19 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 536.98 ns | 10.883 ns | 2.826 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.22 ns |  0.083 ns | 0.022 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.78 ns |  0.202 ns | 0.031 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.24 ns |  0.038 ns | 0.010 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.012 μs** | **0.0052 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **1**       | **2.001 μs** | **0.0019 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.452 μs** | **0.0348 μs** | **0.0054 μs** |         **-** |
| **WriteRequest** | **9**       | **2.457 μs** | **0.0140 μs** | **0.0022 μs** |         **-** |
| **WriteRequest** | **10**      | **2.451 μs** | **0.0036 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **11**      | **2.460 μs** | **0.0184 μs** | **0.0048 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **108.65 ns** | **0.925 ns** | **0.240 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.34 ns | 2.349 ns | 0.363 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **99.07 ns** | **0.435 ns** | **0.067 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  96.60 ns | 0.678 ns | 0.176 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,744.2 ns |  9.27 ns | 5.52 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,069.3 ns |  2.60 ns | 1.55 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,408.5 ns |  4.80 ns | 2.51 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,442.0 ns |  3.82 ns | 2.53 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,005.2 ns | 15.28 ns | 9.09 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,209.7 ns |  4.71 ns | 2.46 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,964.2 ns |  3.84 ns | 2.29 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,900.0 ns |  5.51 ns | 3.28 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,194.0 ns |  3.27 ns | 2.16 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,042.8 ns |  3.60 ns | 1.88 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   717.1 ns |  2.04 ns | 1.22 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   810.1 ns |  2.57 ns | 1.70 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   159.9 ns |  0.31 ns | 0.20 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,721.6 ns |  3.26 ns | 1.71 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,183.4 ns |  1.78 ns | 0.93 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean          | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,491.954 ns | 11.9661 ns | 7.9148 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     11.577 ns |  0.0964 ns | 0.0637 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.332 ns |  0.3010 ns | 0.1991 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     28.221 ns |  0.7076 ns | 0.4681 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.868 ns |  2.4018 ns | 1.5887 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.012 ns |  0.0281 ns | 0.0167 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.828 ns |  2.0731 ns | 1.3712 ns |  1.00 |    0.02 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.595 ns |  1.4502 ns | 0.9592 ns |  0.52 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |-------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.5 ns |     3.04 ns |     2.01 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,621.2 ns |   103.35 ns |    61.50 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     226.3 ns |     0.51 ns |     0.34 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,982.5 ns | 1,736.46 ns | 1,148.56 ns |      - |      80 B |

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