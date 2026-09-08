# Retained deserialized-null evidence

Product correction: fa2c5b88b262a342d08a9f9315fdf4fd3047bd31. Baseline: 766b9c605104906e4580c44a95392c94942c089b. The maintained fixture is ea67f9b17b8b35b73f7ba7a7df62ca7a6356f3bc; its src tree is identical to the product correction. The standalone entry point, driver, logger and assessment are retained under runtime-flag/fixture and runtime-flag. Their hashes bind task-local source; the host informational version is not a claim that those task-local files were committed in that checkout.

[Complete metrics](runtime-flag/summary.md), [interpretation](runtime-flag/interpretation.md), [predeclared replacement plan](runtime-flag/experiment-plan.md), [raw statistics](runtime-flag/summary.json), and [retention scope](validation/retention-notes.md). All raw files preserve their original bytes; source files renamed .txt retain their original contents. The original project-relative paths document the build layout and need that layout when rebuilding.

Acceptance is INCONCLUSIVE. Every measured allocation result is 0 B per key operation, but large control drift, ongoing compilation/thread-pool activity and missing whole-client metrics block acceptance. NullKinds intentionally changes equality and is not equivalent successful dispatch work. The original constant-flag comparison stopped during A1 and contributes no samples to the replacement.

Full executable hosts, test reports, sources, ZIPs and raw results are also retained at C:/git/Dekaf-evidence/pr-3082/fa2c5b88b262a342d08a9f9315fdf4fd3047bd31/deserialized-null-20260908/. The initial durable copy contains 3,244 files (1,438,242,985 bytes), each SHA-256 verified against its original before this publication. Executable binaries are excluded from this Git branch but retained in that archive. The raw source branch is evidence only and must not be merged.

Main was 5df2f0d03607389384b5c1466e17812a9084fac9 when pinned. It moved to 9eec358dad2a081dedbbfc75f02aee743e6bdad9 after the campaign; that tooling merge changes no src files. This local preceding-product comparison is not fresh-main hosted acceptance for either main revision.
