# Controlled benchmark scope

The controlled run deliberately selects three scenarios, each across five jobs: baseline, published product, candidate, published control, and baseline control. It contains 15 measured cases. The two original generated reports retain their measured bytes and hashes.

| Original report | Included methods | Included parameter values | Measured cases |
|---|---|---|---:|
| [BinaryKeyDispatchBenchmarks](https://github.com/thomhurst/Dekaf/blob/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence/controlled/results/Dekaf.Benchmarks.Benchmarks.Unit.BinaryKeyDispatchBenchmarks-report-github.md) | `StringControl` only | This selected method has no `KeySize` parameter | 5 |
| [DistinctBinaryKeyDispatchBenchmarks](https://github.com/thomhurst/Dekaf/blob/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence/controlled/results/Dekaf.Benchmarks.Benchmarks.Unit.DistinctBinaryKeyDispatchBenchmarks-report-github.md) | `ByteArray`, `RawMemory` | `KeySize = 65536` only | 10 |

The controlled run excludes `BinaryKeyDispatchBenchmarks.ByteArray` and `.RawMemory`, and excludes the distinct-key sizes 8 and 1024. Do not infer results for those combinations from these reports. The distinct-key report does include binary methods; it is not a string-only experiment.

The [controlled runner](https://github.com/thomhurst/Dekaf/blob/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence/comparison-controlled/Program.cs.txt) records runtime/job settings and forwards the supplied benchmark selection. The [complete log](https://github.com/thomhurst/Dekaf/blob/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence/controlled.log) lists the executed cases and their samples. Each generated table names its included methods; the distinct-key table also includes its `KeySize` column. This companion explains the omitted combinations without modifying the generated evidence.

The separate initial matrix under [timing](https://github.com/thomhurst/Dekaf/tree/96a8694db9178379f8ee0b1844850bc971974116/docs/performance-evidence/2026-09-07/pr-3082/evidence/timing) uses different runtime/affinity settings and remains a separate experiment. Its broader coverage cannot fill the missing controlled cases or establish equivalence. Neither experiment establishes whole-PR performance acceptance; the large-key cost relative to the reference-equality baseline and missing protected metrics remain blocking.
