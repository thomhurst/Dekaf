# PR #3109: CPU attribution

Previous instrumented run: https://github.com/thomhurst/Dekaf/actions/runs/34151392676

Warmed A1/B/A2 lifecycle CPU was 538.6849 / 547.3712 / 531.5168 us per shutdown lifetime (+1.6% / +3.0%). These boundaries include fixture construction, reflection, filling 1,024 queued records, stop/drain, disposal and diagnostic snapshots. Stop-only BDN mean improved, so attribution is needed before changing product code. The previous verdict remains INCONCLUSIVE.

This is a changed diagnostic experiment, not an identical repeat or full acceptance run. Pin fresh main and the rebased PR head (product source unchanged by documentation-only rebase). Build both once; run A1/B/A2 sequentially on one ubuntu-latest VM, CPUs 2/3, default tiered compilation enabled. Each fresh process has 30 seconds of workload warmup, then 30,000 measured shutdown lifetimes. Keep every sample and maximum. Separate setup, stop and cleanup process CPU with Environment.CpuUsage; retain user/kernel contributions and 1,000-operation blocks. Diagnostic CPU reads add overhead and can be quantized; never interpret individual CPU samples as exact instruction attribution. No profiler is attached during these measurements.

Fixture work and correctness assertions match the previous experiment. Stage timestamps are disabled in the shared fixture; the probe brackets stop latency externally. Capture warmup and measurement JIT activity, thread-pool growth, GC and heap trends. Full-lifecycle allocations remain distinct from per-message allocations and existing MemoryDiagnoser results.

CPU attribution requires stable controls and a candidate difference larger than block uncertainty. Investigate control drift exceeding 2% or continuing startup transitions; do not force attribution or claim acceptance. Report absolute CPU, latency, throughput and allocation results against both controls. No change to the PR performance gate follows automatically. If a causal optimization is identified, validate it against this exact candidate and fresh main with all applicable protected metrics before accepting it.

CPU API: https://learn.microsoft.com/en-us/dotnet/api/system.environment.cpuusage?view=net-10.0
