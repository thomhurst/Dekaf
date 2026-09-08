# Admin BDN engine primer validation, 2026-09-08

**Tooling validation passed. Product performance acceptance remains INCONCLUSIVE.**

Harness `57fccaf666474b4ce767dee6b15563c7a0eab7f1` adopts the guarded callback/formatting primer identified by the [method-level diagnosis](20260908-bdn-stage-diagnosis.md). The earlier report records the diagnostic patch before adoption; this report describes the shared runner afterward. No product source changes were made.

## Change

BDN setup spends ten seconds exercising the generated empty iteration callbacks and measurement formatting, then performs the existing complete-entry probe primer and actual workload warmup. Only callbacks whose IL body is a single `ret` instruction may be primed. A cold non-inlined, non-optimized helper prevents those calls from disappearing during priming. The actual benchmark method and callback execution remain unchanged.

The normal job uses fifty workload warmup iterations. The runner verifies at least twenty elapsed workload seconds and positive completion counts from raw BDN `Workload/Warmup` measurements. Setup warmup and a fixed iteration count cannot substitute for this check. The runner also verifies the ten-second engine primer and matching callback/formatting counts, and saves the normal workload warmup summary.

Signal filenames contain both case and PID, preventing different cases from combining records after PID reuse. UTC/Stopwatch calibration is retained for tracing, with the existing caveat that interprocess signal receipt can overlap adjacent engine work. No events or measured samples are discarded.

## Validation

- Fifteen Python tests pass, including short/missing/non-finite warmup evidence, zero completions, insufficient engine priming, mismatched work counts, PID reuse, histogram preservation, both-control requirements, and loaded-binary identity.
- The normal-mode validators accept all three preserved full-duration callback-primer diagnostic processes. Their fifty actual workload warmups last 25.000, 25.563, and 25.209 seconds. This validates evidence handling; it is not a new performance run.
- A complete local A1/B/A2 smoke run builds exact product revisions A `5df2f0d03607389384b5c1466e17812a9084fac9` and B `e85fcb90bf1ba746eadb604f6f1347670eb04b98` before execution. A1 and A2 each exercise three controls; B exercises those controls plus five new cases. All fourteen BDN cases, all probe fixtures, engine primers, runtime boundaries, loaded identities, and retained worker checks pass.
- Builds use .NET SDK 10.0.400/runtime 10.0.11 on Windows x64, Release/net10.0, workstation GC, tiered compilation and PGO enabled. BDN is pinned to 0.15.8. No profiling agent is attached to smoke validation.
- Smoke uses `Job.Dry`, one actual iteration, 0.2-second probe setup/measurement phases, and the full ten-second engine primer plus complete-entry probe primer. Smoke explicitly bypasses the normal elapsed BDN workload warmup requirement and **cannot establish steady state or acceptance**.
- Final changes were reviewed for reuse, clarity, and scope; whitespace checks pass. No additional client tests or performance acceptance are claimed for this harness-only change.

Fresh main at start: `5df2f0d03607389384b5c1466e17812a9084fac9`. Main observed at end: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. The same exact A remains pinned throughout the smoke run.

## Remaining blocker

The separate standalone `classic:16` diagnostic still records five runtime helper compilations 22–26 seconds into measurement after 120 seconds of workload warmup. The three one-second intervals containing those compilations have maximum call latencies of 310.2, 331.1, and 477.1 microseconds. The three largest maxima in the whole run (1,939.5, 1,297.3, and 1,182.8 microseconds) occur in intervals with zero JIT transitions. This does not establish causality or steady state; it shows that removing the identified JIT transitions alone cannot explain all retained tails. Every interval remains preserved.

The diagnosed BDN issue is repaired in the harness, but Ubuntu validation across all workloads and standalone probe startup assessment remain necessary before another full performance campaign. Hosted run 34170245379 remains INCONCLUSIVE, with its protected-metric losses and control drift unchanged. No paid/full acceptance run was dispatched and no product gate was changed to PASS.

## Retention

Durable archive: `C:/git/Dekaf-evidence/pr-3128/engine-primer-20260908/`. Before adding this report, 978 files were SHA-256 verified against originals and 315 loaded assembly identities matched retained copies. The archive includes both exact product source ZIPs, the exact harness source ZIP, modified fixtures, generated workers, loaded binaries, all smoke reports/runtime series, unit logs, the normal-validator replay, and the complete per-second diagnostic attribution. The archive inventory also hashes this report after generation. Earlier method traces and all full-duration diagnostic data remain in `C:/git/Dekaf-evidence/pr-3128/bdn-stage-20260908/`.
