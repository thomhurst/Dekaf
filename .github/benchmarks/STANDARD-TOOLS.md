# Standard performance tooling

All benchmark fixtures belong in `tools/Dekaf.Benchmarks`. Use the existing project,
normal project references and BenchmarkDotNet's out-of-process toolchain. Do not add
standalone harness projects, custom timing/statistics engines, runtime/JIT samplers,
generated project templates or DLL-rebinding scripts. Do not extend or dispatch the
legacy executable harnesses under `.github/benchmarks` or `tools/*Evidence`.

The [performance gate](../workflows/performance-gate.yml) pins exact product revisions,
selects fixtures from changed paths, builds and Dry-validates both, then partitions
identical expanded case sets into bounded batches. Each batch builds and validates
both revisions again and runs A1/B/A2 sequentially on one `ubuntu-latest` VM. Its thin
shell/Python orchestration and jq comparison read original BenchmarkDotNet exports;
they do not replace measurement.
If a fixture is missing on main, land compatible coverage in the benchmark project
before comparison. Never compare different fixture work as equivalent performance.

Use the repository SDK, Release configuration, normal runtime settings,
`[MemoryDiagnoser]`, 50 workload warmup iterations targeting one second, 25 measured
iterations, one launch and `DontRemove` outliers identically in all phases. Verify at
least 20 seconds of actual workload warmup from full JSON. Increase declared warmup
when needed for all phases; iteration counts alone do not prove steady state.
The existing 5% protected-metric limits and 8 B/op micro allocation-noise floor remain.
Each batch retains the 48-case budget. At most four batches run, with two concurrent
jobs; larger selections still require narrower affected-workload filters. Every
selected case belongs to exactly one batch. BDN's original full names preserve each
parameter combination, and every measured phase must match its planned case set.
The final check requires all batches and combines verdicts, never metrics across VMs.
Missing fixtures or differing A/B case sets stop preparation with named diagnostics;
they cannot silently shrink the measured scope. Original discovery and batch artifacts
are retained separately. Preparation failure is not a measured regression.

Use original BenchmarkDotNet JSON/Markdown/CSV and logs as GitHub Actions artifacts.
Put identities, absolute metrics, deltas, control drift, limitations and the verdict
in the job summary. The PR needs a concise verdict and link. Do not commit reports,
logs, raw results, traces, inventories, copied snapshots or per-PR evidence Markdown.
Keep actual product documentation and benchmark/correctness fixture source in Git.

Use the existing stress project/workflow for loaded Kafka scenarios; obey its lane,
duration and paid-run limits. Do not infer per-message p99, CPU or long-run stability
from BenchmarkDotNet iteration statistics. Missing applicable metrics remain missing.

For separate diagnostics, use the pinned `dotnet-counters` and `dotnet-trace` tools,
original exports and standard viewers. Do not introduce a custom profiler or sum
sampled allocation events as total allocation. JIT activity is not an acceptance
metric. All A1/B/A2 and correctness requirements in [AGENTS.md](../../AGENTS.md) apply.
