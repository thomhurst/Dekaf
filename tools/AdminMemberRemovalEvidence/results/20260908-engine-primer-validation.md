# Member-removal BDN engine primer validation, 2026-09-08

**Tooling validation passed. Product performance acceptance remains INCONCLUSIVE.**

Harness `a4d739f5306f1c3830fbea4aa2631f4315d6990f` ports the guarded BDN engine primer and elapsed normal-workload checks from PR #3128 harness `57fccaf666474b4ce767dee6b15563c7a0eab7f1`. The helper and both validator functions match that diagnosed implementation exactly. Existing fixtures, probe measurement code, workload matrix, both comparison sequences, and protected-metric criteria are preserved. No product source changes are included in this harness update.

## Repair and limits

Before complete-entry priming and actual setup workload warmup, BDN spends ten seconds exercising its generated empty iteration callbacks and measurement formatting. The guard permits only callbacks whose IL body is a single `ret` instruction. A cold non-inlined, non-optimized helper prevents priming calls from disappearing through inlining; actual benchmark execution is unchanged. Two fixed-stage BDN methods are prepared outside measured work.

Normal runs use fifty BDN workload warmup iterations. The driver requires at least twenty elapsed workload seconds and positive completions from raw Workload/Warmup measurements, and validates ten elapsed seconds of engine priming with positive, matching callback/formatting counts. It retains each result. A fixed iteration count alone cannot satisfy elapsed warmup. Host UTC/Stopwatch calibration supports method attribution while preserving the caveat that signal receipt can overlap adjacent engine work.

## Exact products and validation

- Baseline A: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`.
- Candidate B: `912e404b177fdac70934bc3e1aa2d0be797a55b6`.
- Immediately preceding product P: `f36d954d2ae6d3e4689d4383f72cc50cc825eafd`.
- Fifteen Python tests pass, including insufficient/non-finite workload duration, missing work, primer count mismatch, histogram preservation, loaded-binary identity, both-control requirements, and zero-resolution observations remaining INCONCLUSIVE.
- Normal-mode validators accept all three preserved full-duration callback-primer diagnostic processes from #3128. Their fifty workload warmups last 25.000, 25.563, and 25.209 seconds. This checks evidence handling using a different workload; it is not new timing evidence for this PR.
- All 42 BDN configurations and their probe fixtures pass in the local six-phase smoke run. Exact A, B, and P build and validate before the phases. Generated callbacks, engine primer evidence, case identities, runtime boundaries, loaded binaries, and worker retention checks pass.
- Windows x64, .NET SDK 10.0.400/runtime 10.0.11, Release/net10.0, BenchmarkDotNet 0.15.8, workstation GC, tiered compilation and PGO enabled. No profiler is attached.
- These tooling smoke suites overlap with the other admin harness smoke suite. Timing/CPU deltas are not interpreted. Smoke uses Job.Dry with one actual iteration, 0.2-second probe setup/measurement, and the full ten-second engine primer plus complete-entry probe primer. Explicit smoke does not satisfy normal acceptance warmup or establish steady state.
- Final changes were reviewed for reuse, clarity, and scope; whitespace checks pass. No new client test result or performance acceptance is claimed for this harness-only change.

| Phase | Product | Configurations | Engine primer seconds, min–max |
|---|---|---:|---:|
| A1 | A | 4 | 10.0000000–10.0000002 |
| B | B | 19 | 10.0000000–10.0000861 |
| A2 | A | 4 | 10.0000001–10.0000006 |
| P1 | P | 5 | 10.0000002–10.0000319 |
| B2 | B | 5 | 10.0000001–10.0000665 |
| P2 | P | 5 | 10.0000003–10.0000005 |

Fresh main at start: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. Main observed at end: `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. The exact A pin remains fixed throughout both control phases.

## Decision

The diagnosed BDN harness gap is repaired and the complete fixture matrix passes tooling validation. Ubuntu steady-state validation for these workloads and standalone probe startup assessment remain necessary before another full campaign. The canceled [run 34173466249](https://github.com/thomhurst/Dekaf/actions/runs/34173466249) remains INCONCLUSIVE, with its partial metrics, control uncertainty, and retention limits preserved. The earlier protected-metric losses have not been waived or attributed away.

No paid/full performance campaign was dispatched. The exact product head retains its failed/INCONCLUSIVE performance gate. This smoke run does not accept the fresh-main rebase.

## Retention

Durable archive: `C:/git/Dekaf-evidence/pr-3129/engine-primer-20260908/`. Before adding this report, 2045 files were SHA-256 verified against originals and 910 loaded assembly identities matched retained copies. The archive contains all three exact product source ZIPs, the exact harness source ZIP, copied fixtures, generated workers, loaded binaries, all raw smoke reports/runtime series, unit logs, port-identity verification, normal-validator replay, overlap notes, and analysis scripts. The inventory additionally hashes this report after generation. Raw smoke samples remain intact and cannot substitute for missing acceptance metrics.
