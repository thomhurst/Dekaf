# Dekaf

Read [CLAUDE.md](../../../../CLAUDE.md) for validation, public contracts, and performance acceptance. It owns the zero-allocation hot-path rules, benchmark evidence, stress-run budget, and Pareto gate; review/simplification must preserve them.

- Docs-site changes require `npm run build --prefix docs`.
- There is no Aspire AppHost. Integration tests use Docker/Testcontainers Kafka; stop only resources created for this work.
- Before heavy local work, read the current shared checkout's [performance lock workflow](../../../../scripts/PerformanceLock.md). All agents reserve the same Redis `performance` lock for benchmarks, builds, tests, and other heavy jobs; acquire it after the item lock and release it first.
