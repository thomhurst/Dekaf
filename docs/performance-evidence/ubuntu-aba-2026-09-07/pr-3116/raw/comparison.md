# PR #3116: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `677b93bc1aac9d70f474ada85117d99aa952734a`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseBorrowedColdPreparedBatch / RecordCount=1024&HeaderCount=0 | 193391.882 | 179627.539 | 200815.958 | -7.12% | -10.55% | +3.84% | 90792 / 379 / 90792 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseBorrowedSynchronousBatch / RecordCount=1024&HeaderCount=0 | 182991.281 | 173291.761 | 183192.705 | -5.30% | -5.40% | +0.11% | 90448 / 128 / 90448 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseBorrowedWarmPreparedBatch / RecordCount=1024&HeaderCount=0 | 164207.087 | 156420.612 | 170029.556 | -4.74% | -8.00% | +3.55% | 90328 / 128 / 90328 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseColdPreparedBatch / RecordCount=1024&HeaderCount=0 | 193450.328 | 182279.215 | 201309.974 | -5.77% | -9.45% | +4.06% | 90792 / 90795 / 90795 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseSynchronousBatch / RecordCount=1024&HeaderCount=0 | 183074.586 | 183735.948 | 194391.927 | +0.36% | -5.48% | +6.18% | 90450 / 90448 / 90448 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / ParseWarmPreparedBatch / RecordCount=1024&HeaderCount=0 | 161304.318 | 163643.978 | 169778.302 | +1.45% | -3.61% | +5.25% | 90328 / 90328 / 90328 |
| Dekaf.Benchmarks.Benchmarks.Unit / ShareConsumerParsingBenchmarks / TraverseRetainedBatch / RecordCount=1024&HeaderCount=0 | 3262.956 | 3526.580 | 3313.963 | +8.08% | +6.42% | +1.56% | 0 / 0 / 0 |
