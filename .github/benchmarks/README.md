# Benchmark workflow

Use [tools/Dekaf.Benchmarks](../../tools/Dekaf.Benchmarks) and standard BenchmarkDotNet.
See [standard tooling and evidence storage](STANDARD-TOOLS.md) and the repository's
[performance requirements](../../AGENTS.md).

The [automatic performance gate](../workflows/performance-gate.yml) selects maintained
fixtures for changed product paths and runs A1/B/A2 sequentially on one hosted Ubuntu
VM. It retains original exports and the reviewed screen in GitHub job output/artifacts.
For a targeted manual run, dispatch from main with `pr`, exact fresh-main `base_sha`,
current PR `head_sha` and the affected standard-project `filters`. The workflow rejects
stale pins and closed PRs. Fresh main, candidate ancestry, identical
fixtures, elapsed warmup, protected metrics and repeat limits still apply.

Add missing coverage to the existing benchmark project. Land a compatible fixture on
main before an acceptance comparison if the baseline lacks it. Do not copy or create
a separate project, swap product DLLs or add a custom measurement engine.

For loaded workloads use the existing [stress workflow](../workflows/stress-tests.yml)
and `tools/Dekaf.StressTests`, with the declared lane, duration and exact-SHA controls.
Use standard diagnostic tools separately when investigating results.

The standalone harnesses under this directory and `tools/*Evidence` have been removed.
Do not restore them when an older workflow or archived instruction refers to them.
Historical results remain in Git history and their original GitHub runs; do not
recommit them or translate their observations into new acceptance verdicts.

Do not commit benchmark reports, logs, raw result exports, evidence manifests,
experiment/acceptance Markdown or copied source/binary snapshots. Publish evidence in
job summaries and artifacts; PR comments link the job and state a concise decision.
