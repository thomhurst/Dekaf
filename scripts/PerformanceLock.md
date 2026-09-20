# Performance validation moved to GitHub Actions

The local Redis `performance` lock is no longer required for benchmarks, profiling, stress runs, builds, tests, restores, docs builds, or other heavy work.

Follow [the benchmark execution requirements in AGENTS.md](../AGENTS.md#performance-gate): run baseline → candidate → baseline (A–B–A) sequentially on the same GitHub-hosted Actions `ubuntu-latest` runner VM, using pinned fresh-main and rebased candidate SHAs and identical settings. Measure throughput, per-message latency, CPU, allocations, and stability appropriate to the affected path. Local measurements are diagnostic only and cannot establish performance acceptance.

Keep PR/issue ownership locks. The protected metrics, zero-allocation requirements, stress acceptance gates, and paid-run limits in [AGENTS.md](../AGENTS.md) still apply.
