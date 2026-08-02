---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-08-02 04:49 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.95 | 0.85–1.03 | 19% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.08 | 0.94–1.24 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.70 | 0.64–0.74 | 14% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.13 | 0.96–1.60 | 57% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.07–0.09 | 17% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.37 | 0.16–0.41 | 69% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.02 | 0.79–1.13 | 33% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.05 | 0.85–1.14 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.01 | 0.98–1.14 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.97 | 0.96–1.11 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.50–0.53 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.43 | 0.40–0.48 | 18% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.97–1.48 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,946.1 μs** | **113.50 μs** |  **75.07 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,568.4 μs |  22.97 μs |  15.19 μs |  0.43 |    0.01 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,149.7 μs** |  **88.49 μs** |  **52.66 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,758.1 μs |  73.53 μs |  48.64 μs |  0.53 |    0.01 |        - |       - |   51551 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,670.0 μs** |  **27.41 μs** |  **14.34 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,639.6 μs |  34.32 μs |  22.70 μs |  0.40 |    0.00 |        - |       - |    6101 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,446.3 μs** | **374.86 μs** | **223.08 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,382.5 μs | 160.77 μs |  95.67 μs |  1.08 |    0.02 |        - |       - |   69186 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **112.8 μs** |   **2.27 μs** |   **1.35 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    127.3 μs |  22.89 μs |  15.14 μs |  1.13 |    0.13 |        - |       - |     145 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,137.6 μs** |  **25.78 μs** |  **17.05 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,294.5 μs | 228.90 μs | 136.22 μs |  1.14 |    0.11 |        - |       - |    4433 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **949.4 μs** |  **19.50 μs** |  **11.61 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121450 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    949.8 μs |  59.78 μs |  39.54 μs |  1.00 |    0.04 |        - |       - |    1773 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,127.8 μs** |  **78.00 μs** |  **40.79 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213435 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,404.0 μs | 583.02 μs | 346.94 μs |  1.03 |    0.04 |        - |       - |   16930 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,464.5 μs** |  **25.57 μs** |  **16.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,488.9 μs |   7.95 μs |   4.73 μs |  0.46 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,478.9 μs** |  **42.98 μs** |  **22.48 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,510.7 μs |  19.31 μs |  12.77 μs |  0.46 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,473.8 μs** |  **26.34 μs** |  **17.42 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,495.1 μs |  10.30 μs |   6.81 μs |  0.46 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,479.2 μs** |  **34.73 μs** |  **22.97 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,507.3 μs |  11.94 μs |   7.90 μs |  0.46 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **113.3 μs** |    **53.33 μs** |  **27.89 μs** |   **114.0 μs** |  **1.06** |    **0.35** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   117.2 μs |    33.30 μs |  17.42 μs |   119.7 μs |  1.09 |    0.30 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **136.1 μs** |    **64.49 μs** |  **33.73 μs** |   **113.1 μs** |  **1.05** |    **0.33** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   153.7 μs |    33.38 μs |  17.46 μs |   152.7 μs |  1.18 |    0.27 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **993.5 μs** |   **584.47 μs** | **305.69 μs** |   **938.3 μs** |  **1.08** |    **0.43** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   636.5 μs |   104.42 μs |  46.36 μs |   637.8 μs |  0.69 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,312.4 μs** | **1,028.54 μs** | **537.95 μs** |   **969.2 μs** |  **1.12** |    **0.56** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,700.0 μs | 1,596.46 μs | 708.84 μs | 2,176.5 μs |  1.45 |    0.72 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,532.3 ns** |    **28.04 ns** |    **14.66 ns** | **5,531.4 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   478.8 ns |    36.46 ns |    21.70 ns |   478.2 ns |  0.09 |    0.00 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,817.3 ns** | **1,568.47 ns** | **1,037.45 ns** | **3,594.1 ns** |  **1.17** |    **0.67** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,159.8 ns |    89.57 ns |    59.24 ns | 1,152.3 ns |  0.48 |    0.21 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 587.02 ns | 5.978 ns | 1.552 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.14 ns | 0.028 ns | 0.004 ns |      - |         - |
| WriteDescribeGroupsV6      |  46.11 ns | 0.077 ns | 0.020 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.24 ns | 0.074 ns | 0.019 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.050 μs** | **0.0027 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **1**       | **2.075 μs** | **0.0070 μs** | **0.0011 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.399 μs** | **0.0122 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **9**       | **2.400 μs** | **0.0057 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **10**      | **2.398 μs** | **0.0166 μs** | **0.0043 μs** |         **-** |
| **WriteRequest** | **11**      | **2.401 μs** | **0.0137 μs** | **0.0021 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.80 ns** | **0.222 ns** | **0.034 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  93.82 ns | 1.171 ns | 0.181 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **90.88 ns** | **0.368 ns** | **0.057 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.12 ns | 0.308 ns | 0.080 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.6 ns |  5.71 ns |  2.99 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,130.2 ns | 12.59 ns |  6.58 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,321.0 ns |  3.18 ns |  2.10 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,271.6 ns |  3.58 ns |  2.13 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,097.6 ns |  5.98 ns |  3.13 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,009.7 ns |  6.04 ns |  3.99 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,081.9 ns |  7.14 ns |  3.73 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,838.8 ns |  3.92 ns |  2.59 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.2 ns |  4.65 ns |  2.77 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,827.3 ns | 20.75 ns | 13.72 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   810.8 ns |  4.04 ns |  2.67 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   834.9 ns |  2.04 ns |  1.35 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   167.6 ns |  0.14 ns |  0.08 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,737.5 ns |  3.93 ns |  2.60 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,299.9 ns |  2.88 ns |  1.71 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,074.49 ns | 120.030 ns | 79.392 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.57 ns |   0.023 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.28 ns |   0.022 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.05 ns |   0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.80 ns |   0.373 ns |  0.222 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |   0.006 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.12 ns |   2.658 ns |  1.582 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.49 ns |   0.172 ns |  0.114 ns |  0.50 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.8 ns |   1.96 ns |   1.17 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,402.4 ns | 329.04 ns | 195.80 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.1 ns |   0.34 ns |   0.20 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,543.3 ns | 183.00 ns |  95.71 ns |      - |      80 B |

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