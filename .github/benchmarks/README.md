# Benchmark workflow

All fixtures live in [tools/Dekaf.Benchmarks](../../tools/Dekaf.Benchmarks) and run with
standard BenchmarkDotNet. See [standard tooling](STANDARD-TOOLS.md) and the repository's
[performance guidance](../../AGENTS.md).

The [performance gate](../workflows/performance-gate.yml) runs on every pull request that
changes `src/`, the benchmark project or the build inputs:

- `.github/scripts/performance_gate.py select` picks benchmark classes: four hot-path
  sentinels for any product change, a few extra classes per hot component directory, and
  every fixture class the PR adds or edits. Non-steady-state fixtures are skipped with a
  reason.
- One `ubuntu-latest` job per class builds the PR's fixture source against the baseline
  (merge base with `main`) and the candidate, then measures A1, B and A2 back to back on
  that VM. If the PR fixtures need APIs the baseline lacks, the baseline builds its own
  fixtures; cases whose fixture file differs between the revisions are then reported as
  `NOT COMPARED` rather than screened, because matching names do not prove equal work, and a
  class the PR adds is measured on the candidate alone. Nothing has to land on `main` first.
- `performance_gate.py screen` compares medians against both controls with a tolerance
  that widens for small operations (20% under 100 ns, 15% under 1 µs, 10% from 1 µs) and an
  allocation floor of one object (24 B/op) and 1%. A regression is re-measured
  immediately and only fails the job when it reproduces.
- The job summary holds the per-case table; the job artifact holds the original
  BenchmarkDotNet exports and logs. A typical class finishes in 10 to 25 minutes; a
  repeated regression roughly doubles the measurement time.

For a targeted manual run, dispatch the workflow from any branch with optional
`base_sha`, `head_sha` and `filters` (space-separated BenchmarkDotNet globs).

Loaded producer/consumer behaviour is not covered by microbenchmarks; use the
[stress workflow](../workflows/stress-tests.yml) and `tools/Dekaf.StressTests`.

Do not commit benchmark reports, logs, raw exports or evidence Markdown. Job summaries and
artifacts are the evidence store; PR comments link them.
