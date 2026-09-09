# Maintained performance comparison harness

Use [performance-comparison.yml](../workflows/performance-comparison.yml) for
manual, exact-revision comparisons. The existing daily
[benchmarks.yml](../workflows/benchmarks.yml) and scheduled/manual
[stress-tests.yml](../workflows/stress-tests.yml) retain their existing behavior,
paid-run limits and publishing coverage.

The harness is maintained with the repository. Its source is pinned independently
from both products: H is the harness, A is fresh main, and B is the current open
PR head containing A. Each comparison runs A1, B, A2 sequentially on one
`ubuntu-latest` VM. A successful workflow or identity check is not a performance
PASS. Apply [repository acceptance requirements](../../AGENTS.md) to every
applicable protected metric, control drift, uncertainty and correctness result.

## Dispatch

Fetch fresh main, rebase the product PR if needed, and record full lowercase
40-character H/A/B SHAs. After this workflow is merged, dispatch from `main`:

```text
gh workflow run performance-comparison.yml --ref main -f pr=3128 -f suite=admin -f harness_sha=FULL_H_SHA -f baseline_sha=FULL_A_SHA -f candidate_sha=FULL_B_SHA
```

The workflow rejects moving product names, a different checked-out harness,
stale main, a candidate missing main, a changed/closed PR, and unknown suites.
`performance-pins.json` records independent workflow/harness/product identities.
Suite artifacts retain source/binary identities, settings and raw measurements.
The resource wrapper retains CPU topology, affinity, CPU steal, memory, pressure,
disk and child exit status. Inspect these alongside client measurements.

For identical-product repeatability, select `suite=admin-calibration`, set A and
B to the same fresh-main SHA, and use a supported administrative PR number to
select fixtures. H can differ. Calibration uses the same compiled baseline
binary in all phases and never grants product acceptance. See
[reliability and calibration details](HARNESS-RELIABILITY.md).

When that calibration fails, `suite=admin-profile` provides the bounded
GC/JIT/CPU attribution experiment for PR 3138 described in
[ADMIN-ATTRIBUTION.md](ADMIN-ATTRIBUTION.md). It requires identical fresh-main
products and cannot grant performance acceptance.
`suite=admin-sampler-control` isolates the CPU sampler with GC-only controls and
GC-plus-CPU sampling in B, using the same binary. Its differing diagnostic
settings prohibit product acceptance; see the same plan for the bounded test.

## Workload coverage

| Suite | Scope and maintained driver |
| --- | --- |
| `micro` | Focused cases for #3082, #3083, #3085, #3086, #3109, #3116, #3117, #3149, #3158; [driver](../scripts/benchmark_aba.py) |
| `pool`, `pool-recovery` | Pool reset and recovery; [driver](../scripts/pool_reset_aba.py) |
| `pool-loaded`, `pool-profile` | Loaded producer pool comparison and diagnostic profiling; [driver](../scripts/pool_loaded_aba.py) |
| `admin`, `admin-pilot`, `admin-calibration` | Cached-transport administration for #3128, #3129, #3136, #3138; [driver](../scripts/admin_refresh.py) |
| `admin-profile`, `admin-sampler-control` | Diagnostic attribution of identical-product legacy delete for #3138; [plan](ADMIN-ATTRIBUTION.md) |
| `dispatch`, `dispatch-loaded`, `dispatch-pilot`, `dispatch-record-pilot` | Dispatch micro/loaded and diagnostic cases; [driver](dispatch-aba/run.py) |
| `dispatch-adjacent`, `dispatch-loaded-adjacent` | Adjacent dispatch workloads; [driver](dispatch-aba/run_adjacent.py) |
| `share-loaded` | Loaded share consumer; [driver](share-loaded/run.py) |
| `outbox` | Outbox relay cycle; [driver](../scripts/outbox_cycle_aba.py) |
| `outbox-loaded`, `outbox-adjacent` | Loaded and adjacent outbox paths; [driver](../scripts/outbox_loaded_aba.py) |

Fixtures include explicit baseline API adaptations and candidate-only checks.
They are not a universal gate for arbitrary PRs. Extend fixtures and predeclare
coverage before evaluating a new change. Pilots/profiling and partial-scope suites
retain their stated limitations. The `micro` suite configures BDN warmup iteration counts; verify actual elapsed
workload warmup before using its measurements for steady-state acceptance.
Other suites enforce their own elapsed warmup and measurement plans. In particular,
full admin captures use 480 seconds warmup and 180 seconds measurement per phase
per control (33 minutes per triplet, before builds/validation).

Do not reinterpret BDN iteration percentiles as per-message latency, process
counter failures as zero allocations, or missing metrics as passes. Preserve
all maxima and time series. Historical `PLAN.md`, `EXPERIMENT.md`, `REFRESH.md`
and dated reports describe their original configurations; they are not current
acceptance evidence. New H/A/B combinations require new evidence.

## Validation and provenance

[performance-harness-tests.yml](../workflows/performance-harness-tests.yml) runs
driver/replay tests, recorder allocation tests, pool interval tests, actual
administrative fixture builds/captures and Linux affinity/resource smoke checks.
For the administrative smoke locally:

```text
python .github/scripts/smoke_admin_harness.py
```

Its 0.2-second phases validate build compatibility and accounting only.

Imported harness source: `9ea11d3a0f4ca623a274f8810f3eb16039714c21`, including
the reliability fixes from #3159. Migration preserves main's dependency pins
and existing workflows. Historical allocation observations remain linked to
their original commit instead of copied into this tree. Two historical startup
diagnostic launchers (`run_diagnostic.py`, `run_startup.py`) require an external,
uncommitted `AdminJitTraceInspector`; they are excluded from the maintained
entry points. The runnable allocation-counter calibration utility remains a
diagnostic, not a replacement acceptance metric.
