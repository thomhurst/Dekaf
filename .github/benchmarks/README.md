# Maintained performance comparison harness

Prefer standard .NET tooling over custom measurement and profiling code. See
[tool selection and BenchmarkDotNet settings](STANDARD-TOOLS.md). The shared
`micro` suite uses BenchmarkDotNet's normal out-of-process toolchain; it has no
custom runtime logger, JIT event listener or background resource sampler.
Pool, outbox, dispatch and administrative fixtures also remove custom BDN runtime
loggers, samplers and JIT listeners. JIT investigations are manual and separate
from timing; compilation activity is not an acceptance metric.

Use [performance-comparison.yml](../workflows/performance-comparison.yml) for
manual, exact-revision comparisons. The existing daily
[benchmarks.yml](../workflows/benchmarks.yml) and scheduled/manual
[stress-tests.yml](../workflows/stress-tests.yml) retain their existing behavior,
paid-run limits and publishing coverage.

The harness is maintained with the repository. Its source is pinned independently
from both products: H is the harness, A is fresh main, and B is the current open
PR head containing A. Each comparison runs A1, B, A2 sequentially on one
`ubuntu-latest` VM. The `micro` suite screens every case against the declared
tolerances and fails on `REGRESSION`; that screen is the acceptance result for
micro-scoped changes. Other suites and identity checks are not a performance
PASS by themselves. Apply [repository acceptance requirements](../../AGENTS.md)
to every applicable protected metric, control drift, uncertainty and
correctness result.

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
The shared micro suite retains static CPU/runtime/OS details and BDN's original
reports and logs. The shared Linux helper only selects CPU affinity; the custom
background host-resource sampler is removed.

For identical-product repeatability, select `suite=admin-calibration`, set A and
B to the same fresh-main SHA, and use a supported administrative PR number to
select fixtures. H can differ. Calibration uses the same compiled baseline
binary in all phases and never grants product acceptance. See
[reliability and calibration details](HARNESS-RELIABILITY.md).

## Workload coverage

| Suite | Scope and maintained driver |
| --- | --- |
| `micro` | Focused cases for #3082, #3083, #3085, #3086, #3116, #3117, #3149, #3158; normal [project references](aba/Dekaf.Benchmarks.csproj) and BDN CLI in the workflow |
| `pool`, `pool-recovery` | Pool reset and recovery; [driver](../scripts/pool_reset_aba.py) |
| `pool-loaded`, `pool-profile` | Loaded producer pool comparison and diagnostic profiling; [driver](../scripts/pool_loaded_aba.py) |
| `admin`, `admin-pilot`, `admin-calibration` | Cached-transport administration for #3128, #3129, #3136, #3138; [driver](../scripts/admin_refresh.py) |
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
administrative fixture builds/captures and Linux affinity smoke checks.
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
entry points. The custom allocation-counter calibration workload and trace
inspector have been removed; historical sources remain at the imported SHA.
