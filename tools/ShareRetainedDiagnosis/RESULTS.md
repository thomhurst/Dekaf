# PR #3116 retained traversal: code-generation diagnosis

The reconstructed Linux probe does not find a changed traversal instruction stream or changed record field layout. This narrows the investigation but does not explain, reproduce, or waive the hosted retained-traversal slowdown. Performance acceptance remains blocked.

## Evidence and configuration

Historical hosted run [34145885653](https://github.com/thomhurst/Dekaf/actions/runs/34145885653) measured 1,024-record, zero-header retained traversal at 3,262.956 / 3,526.580 / 3,313.963 ns for A1/B/A2. The candidate was 8.08% and 6.42% slower than its controls. This diagnostic collects code and layout rather than new timings; those historical samples retain their original scope and verdict.

- Historical product A: `a48fafe4121350da7ad83fcdd238f0f8039d6d59`.
- Historical product B: `677b93bc1aac9d70f474ada85117d99aa952734a`.
- Original conditional fixture/evidence: `472f37f9dfdc349f580cd53154802915a42226e8`.
- Diagnostic plan and probe source before execution: `8742f60279b7b8fde531a8b13952ba8b91f9e02c`.
- Current PR head `52b4b1f89d0572329682c7e143297d9e4bd692a0` and fresh main `9eec358dad2a081dedbbfc75f02aee743e6bdad9` are not measured by this historical reconstruction.

The hosted artifact contains source, results and binary hashes, but not the original binary files. Both historical sources were rebuilt for this probe. All six core/abstraction/fixture DLL hashes differ from the original hosted hashes; these are explicitly new diagnostic binaries. Their assembly metadata identifies the corresponding historical source SHA, and all loaded outputs and hash comparisons are retained.

Local Linux SDK image: `sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c`. Environment: Ubuntu 24.04, .NET SDK 10.0.400/runtime 10.0.11, Release, `DOTNET_TieredCompilation=0`, consumer probe pinned to CPU 2. Both revisions build before execution; build servers then stop. The original central package versions and conditional baseline adapter remain. The new entry point calls the original full setup and then retained traversal directly, rather than through BenchmarkDotNet's generated engine. JIT output reports FullOpts, Unix generic X64/VEX and no PGO data for the traversal body. This machine and invocation order differ from the hosted experiment.

## Results

Three fresh processes execute sequentially: A1, B, A2. Each validates all 1,024 retained records with zero headers, checksum `1740800003123200`, and 1,000 further traversals with the exact expected aggregate checksum `1740800003123200000`. The original setup also validates parsed record metadata and borrowed/legacy adapter checksums.

| Diagnostic | A1 | B | A2 |
|---|---:|---:|---:|
| Optimized `Traverse` code size | 634 B | 634 B | 634 B |
| Normalized instruction stream | identical | identical | identical |
| Record field offsets | identical | identical | identical |
| Adjacent retained-record gaps of 72 bytes | 1005 / 1023 | 1005 / 1023 | 1005 / 1023 |
| Entire adjacent-gap histogram | identical | identical | identical |

The normalizer replaces only long hexadecimal addresses and omits comments apart from method identity. It preserves opcodes, branch labels and offsets, short immediates, field offsets and symbolic calls. Raw JIT output, normalized files and all three pairwise diffs are retained; the normalized diffs are empty. This does not assert byte-for-byte equality of relocated native code or performance equivalence.

Field offsets from the managed object reference are: Topic 8, Headers 16, Offset 24, TimestampMs 32, Partition 40, Key 44, Value 48, DeliveryCount 52 and AcknowledgeType 56. `ShareConsumeResult<int,int>` is a class in both revisions. Its source and the complete legacy `ParsePartitionRecords` method are identical, as is the conditional benchmark source; the baseline/candidate symbols select different setup adapters and borrowed paths outside retained traversal.

Address modulo 64 distributions differ between B and the A controls. The full distributions and relative reference addresses are retained. These captures occur after setup, traversal, reflective field inspection and successful `GC.TryStartNoGCRegion`; starting that region may itself perform collection. They are diagnostic snapshots, not proof of the heap layout during the original hosted timed interval. No cache-miss counters or causal address experiment was collected. Matching gap histograms do not establish matching cache behavior.

## Decision

There is no supported field-layout or traversal-instruction correction from this result. Do not make a speculative product change or label matching code as a performance PASS. Generated BenchmarkDotNet wrappers, invocation/setup ordering, heap placement, hardware effects and original measurement state remain outside this probe's causal attribution. A next experiment must distinguish a specific one of those influences using controlled inputs and preserved runtime evidence; another identical short probe is not useful.

No timing, CPU/message, latency, allocation or stability gate is inferred from the short code-generation invocations. The existing hosted regression and missing full-head acceptance remain unresolved. No paid workflow, product source change or performance gate update occurred.

## Retention and reproduction

`prepare.py`, `run.py`, `Program.cs`, `analyze.py` and `provenance.py` reproduce the task-scoped setup from the pinned Git sources. Run from this branch's worktree: prepare, run, analyze, provenance. Preparation refuses to overwrite an existing product directory. The runner records the task-owned container identity and removes that container and its anonymous volumes after capturing inspection state. It starts no Kafka broker.

Durable archive: `C:/git/Dekaf-evidence/pr-3116/retained-codegen-20260908/`. It contains exact product source archives, generated fixture/source trees, rebuilt loaded binaries, original fixture/evidence archive, build/environment/probe logs, raw and normalized disassembly, field/address captures, source and binary comparisons, driver scripts and verified copy manifests. The disposable NuGet download cache is excluded; complete loaded output directories are retained. This branch contains diagnostic tooling only and is not a product fix or acceptance PR.
