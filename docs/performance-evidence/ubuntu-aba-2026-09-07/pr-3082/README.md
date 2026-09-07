# PR #3082: Ubuntu A–B–A

Candidate `5492f3069a32c34bab5d8a1d8a001d0436d28086`; main baseline `a48fafe4121350da7ad83fcdd238f0f8039d6d59`. [Completed Actions run](https://github.com/thomhurst/Dekaf/actions/runs/34145853504).

**Acceptance: REGRESSION (large distinct keys).** Repeated binary keys improve 77â€“80%, but 64 KiB distinct keys remain about 3.25â€“3.35 times slower than main.

The 64 KiB byte[] case is 8.620 us/record versus 2.604/2.655 us; ReadOnlyMemory<byte> is 8.623 us versus 2.575/2.613 us. Both have separated timing intervals versus both controls. The 1 KiB ReadOnlyMemory<byte> case also regresses 7.82â€“11.30% with separated intervals. Main uses incorrect reference equality for separately fetched equal binary keys; full-content hashing retains a real cost. String control moves -12.51%, so smaller differences are not attributed confidently. Distinct-key allocations remain 2,177/2,271 B per record, not zero. No tradeoff is accepted.

The table below reports ns per benchmark operation, using each fixture's OperationsPerInvoke denominator. Shutdown and parser rows are whole operations/batches; sustained dispatch is per message. BDN iteration statistics are not message latency percentiles. CPU/message and long-run stability were not measured by this run. The existing performance gate remains blocking; no prior protected-metric finding is erased by a mean-time improvement.

See [raw provenance](raw/provenance.json), [complete statistics](raw/comparison.json), logs and before/candidate/control reports in [raw](raw). Fixture adapters and scope are documented in the [runner README](https://github.com/thomhurst/Dekaf/blob/06aa795796080ce6879139ac2a253d4f0b4266ea/.github/benchmarks/aba/README.md).

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
