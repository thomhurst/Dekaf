# Dekaf

Read [CLAUDE.md](../../../../CLAUDE.md) for validation, public contracts, and performance acceptance. It owns the zero-allocation hot-path rules, benchmark evidence, stress-run budget, and Pareto gate; review/simplification must preserve them.

- Docs-site changes require `npm run build --prefix docs`.
- There is no Aspire AppHost. Integration tests use Docker/Testcontainers Kafka; stop only resources created for this work.
- Run before/after benchmarks on the same GitHub Actions `ubuntu-latest` runner VM as required by [CLAUDE.md](../../../../CLAUDE.md#benchmark-execution). Local measurements are diagnostic only. No local Redis `performance` lock is required for heavy work; retain PR/issue ownership locks.
