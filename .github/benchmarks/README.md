# Maintained performance comparison harness

Prefer standard .NET tooling over custom measurement and profiling code. See
[tool selection and BenchmarkDotNet settings](STANDARD-TOOLS.md). The shared
`micro` suite uses BenchmarkDotNet's normal out-of-process toolchain; it has no
custom runtime logger, JIT event listener or background resource sampler.
Pool, outbox, dispatch and administrative fixtures also remove custom BDN runtime
loggers, samplers and JIT listeners. JIT investigations are manual and separate
from timing; compilation activity is not an acceptance metric.

Every pull request that changes `src/` runs
[performance-gate.yml](../workflows/performance-gate.yml): an automatic same-VM
A1/B/A2 screen of the maintained `tools/Dekaf.Benchmarks` fixtures selected from
the changed paths by [performance_gate.py](../scripts/performance_gate.py)
(merge base versus PR head, each revision building its own fixture copy). Use
[performance-comparison.yml](../workflows/performance-comparison.yml) for
manual, exact-revision comparisons with loaded or bespoke suites. The manual
[benchmarks.yml](../workflows/benchmarks.yml) and
[stress-tests.yml](../workflows/stress-tests.yml) workflows have no schedule;
they retain their paid-run limits and publishing coverage when dispatched.

The harness is maintained with the repository. Its source is pinned independently
from both products: H is the harness, A is fresh main, and B is the current open
PR head containing A. Each workload comparison runs A1, B, A2 sequentially in
one job on one `ubuntu-latest` VM. Independent workloads can use separate jobs
as described below. The `micro` suite screens every case against the declared
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
a baseline that is not on main or is older than 7 days, a candidate missing the
baseline, a changed/closed PR, and unknown suites. Main moving after dispatch does
not invalidate a campaign; `performance-pins.json` records how far main moved.
`performance-pins.json` records independent workflow/harness/product identities.
The preparation job verifies fresh main and the open PR once before jobs fan out.
Queued jobs use that pinned campaign, so main moving during the campaign does
not change their controls. Each job still records main at its end.
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
| `micro` | Bespoke cases declared per PR (#3082, #3083, #3085, #3086, #3116, #3117, #3137, #3142, #3149, #3158); normal [project references](aba/Dekaf.Benchmarks.csproj) and BDN CLI in the workflow. Ordinary PRs use the automatic gate instead |
| `micro-completion` | Standard BenchmarkDotNet publication and manual completion for #3083, ordered/fragmented batches of 128 and 4,096 records; compare full batch cost and identify amortized reservation storage separately |
| `pool`, `pool-recovery` | Pool reset and recovery; [driver](../scripts/pool_reset_aba.py) |
| `pool-loaded`, `pool-profile` | Loaded producer pool comparison and diagnostic profiling; [driver](../scripts/pool_loaded_aba.py) |
| `admin`, `admin-pilot`, `admin-calibration` | Cached-transport administration for #3128, #3129, #3136, #3138; [driver](../scripts/admin_refresh.py) |
| `dispatch`, `dispatch-loaded`, `dispatch-pilot`, `dispatch-record-pilot` | Dispatch micro/loaded and diagnostic cases; [driver](dispatch-aba/run.py) |
| `dispatch-adjacent`, `dispatch-loaded-adjacent` | Adjacent dispatch workloads; [driver](dispatch-aba/run_adjacent.py) |
| `share-loaded` | Loaded share consumer; [driver](share-loaded/run.py) |
| `outbox` | Outbox relay cycle; [driver](../scripts/outbox_cycle_aba.py) |
| `outbox-loaded`, `outbox-adjacent`, `outbox-recovery` | Loaded, adjacent and [failure/lease-loss/shutdown](outbox-loaded/RECOVERY.md) outbox paths; [driver](../scripts/outbox_loaded_aba.py) |

Fixtures include explicit baseline API adaptations and candidate-only checks.
They are not a universal gate for arbitrary PRs. Extend fixtures and predeclare
coverage before evaluating a new change. Pilots/profiling and partial-scope suites
retain their stated limitations. The `micro` suite configures BDN warmup iteration counts; verify actual elapsed
workload warmup before using its measurements for steady-state acceptance.
Other suites enforce their own elapsed warmup and measurement plans. In particular,
admin captures use 30 seconds warmup and 30 seconds measurement per phase
per control (3 minutes per triplet, before builds/validation). Administrative
calls get a smaller sampling budget than sustained producer/consumer workloads.
All control and candidate-only cases remain covered: the full matrices take
14–27 minutes of warmup/measurement, plus builds and validation. The one-control
`admin-pilot` and identical-product `admin-calibration` use the same durations.
Metric tolerances and correctness checks still apply; inspect retained workload
trends and report uncertainty if the shorter capture does not reach steady state.

Do not reinterpret BDN iteration percentiles as per-message latency, process
counter failures as zero allocations, or missing metrics as passes. Preserve
all maxima and time series. Historical `PLAN.md`, `EXPERIMENT.md`, `REFRESH.md`
and dated reports describe their original configurations; they are not current
acceptance evidence. New H/A/B combinations require new evidence.

## Parallel workload jobs

The workflow splits `dispatch-adjacent` into six jobs: one for each of the four
loaded modes, one for all six micro cases, and one for all four shutdown cases.
`dispatch-loaded-adjacent` uses just the four loaded jobs. `outbox-loaded` and
`outbox-adjacent` each use four jobs, one per store/listener configuration.
`outbox-recovery` uses four jobs for #3085 (failure/lease-loss and listener off/on).
For #3171 it uses two jobs with listeners off, validating the product's commit
notifier during the same failure, lease-loss and loaded-shutdown workloads.
Other suites retain one comparison job. There are no new dispatch inputs.

Each job builds both pinned products and validates every selected case on both
before timing. Its complete A1/B/A2 triplets run sequentially with the existing
warmup, sampling, runtime settings and broker resets. Workloads never run
concurrently on the same VM. Coverage across these independent jobs must not
be interpreted as cross-VM absolute performance comparisons.

The pinned `performance-campaign.json` declares the exact workload groups.
Raw artifacts are named `performance-PR-SUITE-RUN_ID-SHARD`; small
`performance-completion-RUN_ID-SHARD` artifacts record completed coverage.
The final coverage job rejects missing/duplicate groups, mismatched campaigns,
different capture durations, incomplete/reordered phases and failed/cancelled
comparison jobs. It reports collection completeness, never performance acceptance.
Review each workload against its own same-VM controls. A failed-job rerun reuses
the original verified campaign and replaces that shard's artifacts; an entirely
new campaign must pin fresh main as usual.

This reduces elapsed time when runners are available, while duplicating some
build/setup cost. Based on the 9 September captures, full adjacent dispatch is
estimated at 40–45 minutes across six jobs (previously 181 minutes); ordinary
loaded outbox at 25–30 minutes across four (previously 86 minutes). Queueing is
excluded and the parallel workflow has not yet been timed. The longer adjacent
outbox variant keeps its 480-second warmup, so it has a larger budget.

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
