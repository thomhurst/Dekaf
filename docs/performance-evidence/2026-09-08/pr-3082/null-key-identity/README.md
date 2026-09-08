# Preserve wire-null key identity

**Performance acceptance remains INCONCLUSIVE for this correction; do not merge.** The PR's historical large-distinct-key regression remains an unapproved blocker. Both new local timing controls exceed the predeclared 2% drift limit. Zero allocations and unchanged result size do not waive any protected metric.

## Correction and correctness

Product `2032fb6decd1a28abced409658d440305fe5bd35` preserves the Kafka wire-null bit through eager, header-routed, prepared/async and in-memory result construction. Partition-key creation consumes that bit, so a null key stays separate from a non-null empty/default value-type key. Null deserialized reference values still use the null lane. EOF and null flags share the existing byte; no public API is added.

The new dispatcher test reproduces the previous bug on `405cc88836d813d3dc0b7df726e2d6063a1a00d1`: byte arrays pass, while ReadOnlyMemory<byte>, Memory<byte> and ArraySegment<byte> each block empty-key work behind a held null-key handler. All four pass after correction, while the second null-key handler remains ordered behind the first.

Validation passes: the initial .NET 10 consumer suite has 1,126 cases, followed by 46 focused cases covering the final constructor/header-routing/in-memory tests; these sets overlap. The .NET 8 consumer/testing suite passes all 1,552 cases. Twelve Kafka 4.3.1 comparer tests pass, including record/batch null-versus-empty ordering and completed/pending async deserialization through ConsumeAsync and ConsumeOneAsync. The four async cases pass again after tightening failure cleanup to observe pending consumption. Eight unchanged AsyncSerde integration cases also pass. The docs production build and diff checks pass.

Four initial integration fixture cases incorrectly combined partitioned handlers with asynchronous deserializers. They fail with the existing documented ConsumeBatchAsync NotSupportedException. The corrected fixture exercises async result construction through supported consumption APIs; the library restriction is unchanged. The original failure log, report, fixture source and test DLL/PDB are retained. No CI rerun, skip or product guard relaxation was used.

## Local A1/B/A2 evidence

A1/A2: exact immediately preceding PR product `405cc88836d813d3dc0b7df726e2d6063a1a00d1`. B: exact product `2032fb6decd1a28abced409658d440305fe5bd35`. Both contain main `5df2f0d03607389384b5c1466e17812a9084fac9`. These incremental Windows controls do not replace the required fresh-main hosted Ubuntu comparison.

| Operation | A1 ns | B ns | A2 ns | B/A1 | B/A2 | Control drift | B/op A1/B/A2 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Raw-memory construction and partition-key hash | 43.5644 | 39.8497 | 41.0575 | -8.53% | -2.94% | -5.75% | 0 / 0 / 0 |
| Already-deserialized construction and EOF read | 0.48046 | 0.68261 | 0.67595 | +42.07% | +0.98% | +40.69% | 0 / 0 / 0 |

Each cell retains all 25 samples. The typed case's +42.07% versus A1 is explicitly retained; the same baseline changes +40.69% between controls. Neither averaging the controls nor the other case's improvement can remove that uncertainty. The 99.9% intervals are 41.2396–45.8891 / 39.5854–40.1140 / 40.6950–41.4200 ns for construction/hash, and 0.45753–0.50340 / 0.66913–0.69608 / 0.66362–0.68827 ns for typed construction. Overlap does not establish equivalence. Full extrema, confidence intervals and phase deltas are in summary.json/csv.

MemoryDiagnoser reports 0 B per operation in all six cases; independent 1,000-operation thread-local probes record exactly 0 B. Unsafe.SizeOf<ConsumeResult<TKey,TValue>> is unchanged in all six tested shapes: string/string 96 B, int/int 88 B, byte[]/byte[] 96 B, and each binary memory/segment type paired with itself 112 B. Layout evidence is representative, not a public layout guarantee.

Windows 11 25H2, Intel Core i7-12700K, SDK10.0.400/runtime10.0.11, BDN0.15.8, Release/net10.0, workstation GC, tiered compilation disabled, affinity mask4, InProcessEmit, no profiler. Both products and fixtures build and validate before sequential A1/B/A2; each case starts a fresh process. No owned build/test runs during timing. The same hashed baseline binaries serve both controls.

Direct actual-workload warmups last 20.000–20.014 seconds. In phase/case order, completed operations are 243,131,705; 438,491,058; 222,320,184; 438,252,483; 244,132,069; 443,161,268. Subsequent 30 BDN workload warmups last 25.791–32.668 seconds. Measured iterations target 1,000 ms and retain all outliers. Each first measured boundary records three new compilations, 0.676–0.762 ms total, with no later compilation-count changes; measured thread-pool observations remain zero. Boundary counters include logger/BDN activity and do not identify the methods. All JIT/CPU/GC/allocation/heap/RSS observations remain retained; BDN-forced GCs are not application GC evidence.

This fixture uses reused 64-byte non-null memory keys and fixed metadata. It measures construction plus hashing, or a narrow optimized already-deserialized call shape. The sub-nanosecond overhead-subtracted typed results are not general construction cost or actual message latency; their losses remain reported. The null-versus-empty behavior differs intentionally between products and is proven separately by correctness tests. No equivalent successful null-dispatch timing is claimed for the failing baseline. Full dispatch, loaded Kafka completion throughput, actual message p50/p99/max, isolated CPU/message, recovery/shutdown and sustained stability are not measured. Full acceptance remains missing.

## Reproduction and retention

Program.cs.txt, RuntimeLogger.cs.txt and Harness.csproj.txt are the exact measured harness sources. Restore their ordinary suffixes in a task-scoped directory under the repository, keeping the project with EnableDefaultItems=false. Build each product for net10.0 with CopyLocalLockFileAssemblies=true; build the harness with ProductDirectory pointing to that output and HasNullFlag=false for A, true for B. The sole conditional fixture adaptation passes the new internal null flag into PartitionMessageKey.From. The normal-path hash, result data and EOF outputs match across all six cases. run.ps1 records the exact phase/case command shape; experiment-plan.md predeclares settings and thresholds.

The first pinned-candidate smoke catches a missing System.IO.Hashing deployment dependency before timing. The corrected package-copy build and harness rebuild pass identity checks and both smoke cases. Its failure log and dependency manifest remain retained. No sample used that incomplete output.

Every benchmark phase binary, the baseline/final test binaries, failed and successful fixture sources, source ZIPs, raw command logs, retained reports, samples and runtime series are copied with SHA-256 verification outside the removable worktree. Intermediate test HTML reports and test assemblies overwritten by later local runs are not retained; their raw command logs remain. The before-fix unit report/binaries, unsupported-async fixture report/DLL/PDB, and final reports/binaries are preserved. This does not claim complete per-invocation test-binary retention. The PR update records the final directory and inventory count. This portable subset is bound to the measured source files by hashes; no product change follows measurement, and no paid run or performance PASS is claimed.
