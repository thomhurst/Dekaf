---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 18:05 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 133× less | Mixed |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.14 | 1.02–1.46 | 38% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.77 | 0.70–0.95 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.46 | 0.80–2.22 | 97% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 49% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.14–0.32 | 65% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.77–1.05 | 30% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.58–1.20 | 63% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.84 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.86 | 0.74–1.39 | 75% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.12 | 0.99–1.92 | 83% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.47 | 820% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.04–0.47 | 807% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.47 | 804% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.03–0.47 | 789% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,244.0 μs** | **231.76 μs** | **153.30 μs** |  **1.00** |    **0.03** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,701.3 μs |  24.76 μs |  14.74 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,602.1 μs** |  **63.45 μs** |  **41.97 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048384 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,789.7 μs |  59.81 μs |  35.59 μs |  0.50 |    0.01 |        - |       - |   51946 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,376.7 μs** |  **71.83 μs** |  **47.51 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,805.0 μs |  35.66 μs |  21.22 μs |  0.44 |    0.00 |        - |       - |    7807 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,238.1 μs** | **257.06 μs** | **152.97 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,686.1 μs | 964.92 μs | 504.67 μs |  1.04 |    0.04 |        - |       - |   69826 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.4 μs** |   **1.53 μs** |   **1.01 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    117.9 μs |  14.44 μs |   9.55 μs |  0.94 |    0.07 |        - |       - |     157 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,280.1 μs** |   **7.22 μs** |   **4.78 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,287.1 μs | 349.35 μs | 231.07 μs |  1.01 |    0.17 |        - |       - |    1535 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,016.1 μs** |  **10.45 μs** |   **6.22 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121478 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    823.7 μs | 131.84 μs |  87.20 μs |  0.81 |    0.08 |        - |       - |    3233 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,275.7 μs** | **141.33 μs** |  **93.48 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215059 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,291.9 μs | 984.18 μs | 650.97 μs |  0.81 |    0.06 |        - |       - |   17381 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,492.5 μs** |   **8.55 μs** |   **5.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    309.0 μs |  12.16 μs |   8.04 μs |  0.06 |    0.00 |        - |       - |     623 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,494.1 μs** |   **7.71 μs** |   **4.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    310.8 μs |   7.92 μs |   5.24 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,507.9 μs** |  **21.42 μs** |  **14.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    321.0 μs |  15.31 μs |  10.13 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,504.3 μs** |  **18.75 μs** |  **12.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    319.6 μs |  10.80 μs |   7.14 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **131.4 μs** |    **36.96 μs** |  **19.33 μs** |   **125.2 μs** |  **1.02** |    **0.19** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   127.6 μs |     4.76 μs |   2.11 μs |   127.5 μs |  0.99 |    0.13 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **172.0 μs** |    **87.69 μs** |  **45.86 μs** |   **144.5 μs** |  **1.06** |    **0.36** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.8 μs |    10.12 μs |   4.49 μs |   173.2 μs |  1.07 |    0.24 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,089.1 μs** |   **493.04 μs** | **257.87 μs** |   **973.4 μs** |  **1.04** |    **0.31** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   790.2 μs |   145.29 μs |  64.51 μs |   763.1 μs |  0.76 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,460.7 μs** |   **867.29 μs** | **453.61 μs** | **1,323.8 μs** |  **1.08** |    **0.44** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,573.5 μs | 1,266.25 μs | 562.22 μs | 1,959.1 μs |  1.17 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,573.9 ns** |  **16.28 ns** |  **8.52 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   597.3 ns | 125.77 ns | 83.19 ns |  0.11 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |           |          |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,727.4 ns** |  **45.01 ns** | **26.78 ns** |  **1.00** |    **0.01** | **0.1450** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,054.1 ns | 102.94 ns | 61.26 ns |  0.28 |    0.02 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 531.50 ns | 8.724 ns | 2.266 ns | 0.0486 |    1224 B |
| WriteFindCoordinatorV6     |  23.11 ns | 0.070 ns | 0.018 ns |      - |         - |
| WriteDescribeGroupsV6      |  41.34 ns | 0.209 ns | 0.054 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.68 ns | 0.177 ns | 0.046 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.827 μs** | **0.0044 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **1**       | **1.832 μs** | **0.0107 μs** | **0.0028 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.129 μs** | **0.0085 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **9**       | **1.896 μs** | **0.0146 μs** | **0.0023 μs** |         **-** |
| **WriteRequest** | **10**      | **2.984 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **11**      | **1.940 μs** | **0.0137 μs** | **0.0036 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **82.98 ns** | **0.205 ns** | **0.032 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 80.38 ns | 0.031 ns | 0.005 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **79.67 ns** | **0.424 ns** | **0.110 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 74.94 ns | 0.746 ns | 0.115 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,350.3 ns | 1.95 ns | 1.16 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,720.9 ns | 8.24 ns | 4.91 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,943.5 ns | 2.56 ns | 1.69 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,928.2 ns | 0.94 ns | 0.56 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,528.6 ns | 5.58 ns | 3.32 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,457.1 ns | 5.75 ns | 3.42 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,503.1 ns | 4.68 ns | 3.10 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,248.9 ns | 1.24 ns | 0.82 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   927.8 ns | 4.36 ns | 2.88 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,581.5 ns | 1.29 ns | 0.68 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   559.0 ns | 1.54 ns | 1.02 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   634.0 ns | 1.40 ns | 0.92 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   127.7 ns | 0.25 ns | 0.15 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,279.2 ns | 2.84 ns | 1.88 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   895.9 ns | 0.63 ns | 0.42 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,224.27 ns | 10.344 ns | 6.156 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.87 ns |  0.059 ns | 0.035 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.74 ns |  0.037 ns | 0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.17 ns |  0.109 ns | 0.065 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     40.03 ns |  1.120 ns | 0.741 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.81 ns |  0.044 ns | 0.026 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    141.28 ns |  6.004 ns | 3.573 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.86 ns |  0.147 ns | 0.097 ns |  0.39 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    276.6 ns |   0.45 ns |  0.30 ns | 0.0019 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 92,642.8 ns | 189.70 ns | 99.22 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    189.5 ns |   0.15 ns |  0.08 ns | 0.0031 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 93,736.5 ns | 111.23 ns | 58.18 ns |      - |      80 B |

</details>

<details>
<summary>How to read these tables</summary>

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Dekaf-internals tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio
- **Confidence**: `⚠ Low` when latest `RatioSD > 0.30` or rolling run spread exceeds 30%
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

</details>

*Benchmarks are automatically run on every push to main.*