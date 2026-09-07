# PR #3082: Ubuntu A–B–A

A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`; B: `5492f3069a32c34bab5d8a1d8a001d0436d28086`.

Measurement completion is not performance acceptance. No automatic performance-gate override.

| Case | A1 ns | B ns | A2 ns | B/A1 | B/A2 | A drift | Allocated B: A1 / B / A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Dekaf.Benchmarks.Benchmarks.Unit / BinaryKeyDispatchBenchmarks / ByteArray /  | 2868.141 | 651.146 | 2898.543 | -77.30% | -77.54% | +1.06% | 2207 / 557 / 2207 |
| Dekaf.Benchmarks.Benchmarks.Unit / BinaryKeyDispatchBenchmarks / RawMemory /  | 2742.485 | 546.738 | 2786.591 | -80.06% | -80.38% | +1.61% | 2308 / 597 / 2307 |
| Dekaf.Benchmarks.Benchmarks.Unit / BinaryKeyDispatchBenchmarks / StringControl /  | 673.024 | 657.542 | 588.804 | -2.30% | +11.67% | -12.51% | 557 / 557 / 557 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / ByteArray / KeySize=1024 | 2604.995 | 2771.934 | 2718.773 | +6.41% | +1.96% | +4.37% | 2177 / 2177 / 2177 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / ByteArray / KeySize=65536 | 2604.110 | 8620.404 | 2654.551 | +231.03% | +224.74% | +1.94% | 2177 / 2177 / 2177 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / ByteArray / KeySize=8 | 2588.045 | 2538.069 | 2681.184 | -1.93% | -5.34% | +3.60% | 2177 / 2177 / 2177 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / RawMemory / KeySize=1024 | 2559.299 | 2848.551 | 2641.990 | +11.30% | +7.82% | +3.23% | 2271 / 2271 / 2271 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / RawMemory / KeySize=65536 | 2574.806 | 8623.475 | 2612.628 | +234.92% | +230.07% | +1.47% | 2271 / 2271 / 2271 |
| Dekaf.Benchmarks.Benchmarks.Unit / DistinctBinaryKeyDispatchBenchmarks / RawMemory / KeySize=8 | 2589.625 | 2607.902 | 2614.819 | +0.71% | -0.26% | +0.97% | 2271 / 2271 / 2271 |
