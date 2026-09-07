# PR #3109: shutdown CPU attribution

The previously higher CPU did not reproduce. The rebased candidate has lower observed shutdown CPU than both fresh-main controls, but block variation is too large to establish an improvement or equivalence. There is no demonstrated CPU regression to fix. No product optimization was made; changing shutdown deadlines without a measured cause would add correctness risk.

## Experiment and provenance

- Run: https://github.com/thomhurst/Dekaf/actions/runs/34153200991
- Main A: `f71303cb25c6568c454e9e88cf3ade3c91b6dc45`.
- PR B: `7316983bc4485ac1db34dc39c9cbdee38a56adcc`.
- Harness: `c57dc8b18507c30f3de5b92a0ff606e8a6ec43b4`.
- One `ubuntu-latest` VM, image `20260831.293.1`, AMD EPYC 9V74, CPUs 2/3; A1, B, A2 sequentially. Full SDK/runtime and hardware output are in `raw/`.
- Thirty seconds of workload warmup in each fresh process, then 30,000 shutdowns per phase, each draining 1,024 queued records. All 90,000 samples retained. All queue-drain, record-order, checkpoint and rejected-writer assertions passed.
- Rebase changed no `src/` or `tests/` contents from previously measured candidate `36ba24db1203c6f464fd253b12c97109377fd28c`. The prior 70 unit and two integration passes remain historical evidence for identical product/test contents, not newly executed tests.
- This changed diagnostic experiment separates setup, stop, and cleanup process CPU with `Environment.CpuUsage`. CPU reads add overhead. Stop latency is bracketed externally; the prior internal stage timestamps are disabled. Full-lifecycle allocations include fixture construction and measurement overhead, not per-message hot-path allocation.

## Results

| Metric | A1 | Candidate B | A2 | B vs A1 | B vs A2 |
|---|---:|---:|---:|---:|---:|
| Lifecycle CPU, us/operation | 530.410 | 522.380 | 535.032 | -1.51% | -2.36% |
| Setup CPU, us/operation | 234.133 | 233.223 | 236.289 | -0.39% | -1.30% |
| Stop CPU, us/operation | 290.945 | 283.825 | 293.407 | -2.45% | -3.27% |
| Cleanup CPU, us/operation | 2.636 | 2.631 | 2.650 | -0.16% | -0.70% |
| Lifecycle allocated bytes/operation | 255968.901 | 255944.829 | 255975.539 | -0.009% | -0.012% |
| Completed lifecycles/s | 3157.287 | 3173.969 | 3126.487 | +0.53% | +1.52% |
| Stop p50, ms | 0.169683 | 0.168310 | 0.170794 | -0.81% | -1.45% |
| Stop p99, ms | 0.410870 | 0.396309 | 0.408677 | -3.54% | -3.03% |
| Stop maximum, ms | 1.170128 | 0.947638 | 0.837435 | -19.01% | +13.16% |

CPU components exclude work between samples and periodic snapshots, so their sum is slightly below full-lifecycle CPU. Setup accounts for about 44–45% of measured lifecycle CPU. These CPU boundaries remain process-wide and include runtime/GC work; they are not instruction-level attribution.

## Uncertainty and comparison with previous evidence

Main control drift is +0.87% for lifecycle CPU and +0.85% for stop CPU. Thirty non-overlapping 1,000-operation blocks have stop CPU standard deviations of 34.223 / 35.615 / 33.019 us. Candidate minus main stop CPU differences are -7.120 / -9.582 us. Approximate normal 95% intervals using block means are [-24.795, +10.555] and [-26.962, +7.797] us. These are descriptive and assume independent blocks; correlated runtime/host activity can widen uncertainty. Neither improvement nor equivalence is established.

Previous run https://github.com/thomhurst/Dekaf/actions/runs/34151392676 reported lifecycle CPU 538.685 / 547.371 / 531.517 us: candidate +1.6–3.0%. Its 1,000-operation CPU blocks ranged roughly 506–601 us across phases, and thread-pool counts differed (5 / 5 / 4). The sign reverses in the new run without a product-source change. Different instrumentation, warmup duration and sample counts prevent treating the cross-run change as an optimization. Taken together, these runs do not establish higher candidate CPU.

New measured phases kept four thread-pool threads each. JIT compiled 4 / 16 / 4 methods, taking 2.608 / 10.862 / 2.544 ms, below 0.1% of lifecycle CPU in every phase. This residual JIT activity is recorded rather than assuming that 30 seconds guarantees complete steady state. Gen2 counts increased by 904 / 905 / 907 during fixture lifetimes; setup repeatedly constructs large record arrays. Warmup and measurement series are preserved for inspecting trends.

The original instrumented latency run had stop p99 0.231230 / 0.232011 / 0.233562 ms and maximum 0.318702 / 0.386720 / 0.323442 ms. New latency values cannot be substituted for an otherwise identical acceptance run: the new CPU instrumentation perturbs execution and the sample count triples. The new maximum also has 28.43% control drift, so it does not isolate a candidate regression. Existing stop-only MemoryDiagnoser evidence (51,680 / 51,600 / 51,680 B per stop) remains separately scoped historical evidence; no new BDN allocation claim is made here.

## Decision

Full acceptance remains **INCONCLUSIVE**. No confirmed CPU regression, no claimed CPU win, no protected-metric tradeoff, and no identical repeat dispatched. CPU alone does not justify changing product code or deadline semantics. The remaining decision needs narrower uncertainty and reliable tail-latency controls in an improved experiment, not selective removal of samples or another unchanged short run.

Validation: both Release fixture/product builds passed with zero warnings/errors locally; 100-sample baseline/candidate dry checks and the 100-sample A–B–A smoke passed; all ten existing evidence-handling tests passed. Hosted dry validation and all 90,000 measured shutdown correctness assertions passed. Raw metrics were checked against CSV row counts, exact completion counts, warmup duration, nonnegative CPU values and recomputed stage means.

Reproduce analysis with `python analyze_cpu.py raw`. Raw inputs and this analysis are listed with SHA256 digests in `manifest.json`.
