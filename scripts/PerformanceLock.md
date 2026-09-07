# Performance validation moved to GitHub Actions

The local Redis `performance` lock is no longer required for benchmarks, profiling, stress runs, builds, tests, restores, docs builds, or other heavy work.

Follow [the benchmark execution requirements in CLAUDE.md](../CLAUDE.md#benchmark-execution): run before/after benchmarks sequentially on the same GitHub-hosted Actions `ubuntu-latest` runner VM, using exact baseline and candidate SHAs and identical settings. Local measurements are diagnostic only and cannot establish performance acceptance.

Keep PR/issue ownership locks. The protected metrics, zero-allocation requirements, stress acceptance gates, and paid-run limits in [CLAUDE.md](../CLAUDE.md) still apply.
