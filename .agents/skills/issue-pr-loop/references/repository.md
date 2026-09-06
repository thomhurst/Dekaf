# Dekaf

Read [CLAUDE.md](../../../../CLAUDE.md) for validation, public contracts, and performance acceptance. It owns the zero-allocation hot-path rules, benchmark evidence, stress-run budget, and Pareto gate; review/simplification must preserve them.

- Docs-site changes require `npm run build --prefix docs`.
- There is no Aspire AppHost. Integration tests use Docker/Testcontainers Kafka; stop only resources created for this work.
