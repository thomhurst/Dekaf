# First manual-commit broker exchange — 2026-09-08

Both retained versions encounter the same transient broker errors and recover in these instrumented four-second runs. This identifies commit-readiness behavior; it does not reproduce the earlier terminal failure with request-level timing or prove a PR regression.

Producer A and consumer A/B use the exact hosted binaries, constant topic/group identity and fresh Kafka 4.3.1 brokers. Each sends and handles 4,000 records and verifies committed offsets `[1000, 1000, 1000, 1000]`. Broker request DEBUG logging is enabled before each run and can perturb timing. No client failure was retried, and these are not performance measurements.

| Phase | UTC time | API/version | Correlation | Partition | Broker error codes |
|---|---|---|---:|---|---|
| A | 2026-09-08 03:35:22,835 | FIND_COORDINATOR v6 | 333 | [] | [15] |
| A | 2026-09-08 03:35:22,943 | FIND_COORDINATOR v6 | 334 | [] | [0] |
| A | 2026-09-08 03:35:22,969 | OFFSET_COMMIT v10 | 335 | [0] | [16] |
| A | 2026-09-08 03:35:22,981 | FIND_COORDINATOR v6 | 337 | [] | [0] |
| A | 2026-09-08 03:35:23,081 | OFFSET_COMMIT v10 | 338 | [0] | [16] |
| A | 2026-09-08 03:35:23,093 | FIND_COORDINATOR v6 | 340 | [] | [0] |
| A | 2026-09-08 03:35:23,353 | OFFSET_COMMIT v10 | 341 | [0] | [16] |
| A | 2026-09-08 03:35:23,363 | FIND_COORDINATOR v6 | 343 | [] | [0] |
| A | 2026-09-08 03:35:23,814 | OFFSET_COMMIT v10 | 344 | [0] | [0] |
| A | 2026-09-08 03:35:23,819 | OFFSET_COMMIT v10 | 345 | [1] | [0] |
| A | 2026-09-08 03:35:23,823 | OFFSET_COMMIT v10 | 346 | [2] | [0] |
| A | 2026-09-08 03:35:23,826 | OFFSET_COMMIT v10 | 347 | [3] | [0] |
| B | 2026-09-08 03:35:42,234 | FIND_COORDINATOR v6 | 333 | [] | [15] |
| B | 2026-09-08 03:35:42,354 | FIND_COORDINATOR v6 | 334 | [] | [0] |
| B | 2026-09-08 03:35:42,367 | OFFSET_COMMIT v10 | 335 | [0] | [16] |
| B | 2026-09-08 03:35:42,374 | FIND_COORDINATOR v6 | 337 | [] | [0] |
| B | 2026-09-08 03:35:42,476 | OFFSET_COMMIT v10 | 338 | [0] | [16] |
| B | 2026-09-08 03:35:42,480 | FIND_COORDINATOR v6 | 340 | [] | [0] |
| B | 2026-09-08 03:35:42,673 | OFFSET_COMMIT v10 | 341 | [0] | [16] |
| B | 2026-09-08 03:35:42,678 | FIND_COORDINATOR v6 | 343 | [] | [0] |
| B | 2026-09-08 03:35:43,167 | OFFSET_COMMIT v10 | 344 | [0] | [0] |
| B | 2026-09-08 03:35:43,172 | OFFSET_COMMIT v10 | 345 | [1] | [0] |
| B | 2026-09-08 03:35:43,173 | OFFSET_COMMIT v10 | 346 | [2] | [0] |
| B | 2026-09-08 03:35:43,174 | OFFSET_COMMIT v10 | 347 | [3] | [0] |

Error 15 is `CoordinatorNotAvailable`; error 16 is `NotCoordinator`; zero is success. In both runs, discovery returns node 1 at `localhost:9092` before the group coordinator finishes loading offsets metadata. Three partition-0 commits receive error 16; the fourth succeeds after loading. Partitions 1–3 then commit successfully.

| Phase | First/last coordinator loading completion (UTC) | Final correctness |
|---|---|---|
| A | 2026-09-08 03:35:23,565 / 2026-09-08 03:35:23,590 | PASSED |
| B | 2026-09-08 03:35:42,928 / 2026-09-08 03:35:42,943 | PASSED |

Each `FindCoordinator` v6 request carries key type 0 and the same group key `dispatch-commit-diagnosis-group`. Each `OffsetCommit` v10 request carries that same group ID, generation/member epoch -1, empty member ID, null static instance ID, the expected topic ID and offset 1000. The retry requests preserve these fields. Full request/response objects, connection identities and request timing components are retained in `commit-exchanges.json` and the unmodified broker logs.

The retry sequence agrees with the existing bounded retry policy. There is no evidence here of malformed group identity, wrong coordinator endpoint, dropped retry, or a candidate-only transient response. The previous failed attempts did not capture these request timestamps; their final error is not retrospectively assigned a proven cause.

## Consequence for the JIT diagnostic

Topic-list readiness and a successful coordinator lookup do not prove that offset commits are ready. The revised local setup first uses the bundled Kafka Java admin CLI to commit zero offsets for a separate seed group/topic, with a 30-second timeout and all four partition results checked. A separate Dekaf seed must still process and commit its 4,000 records without swallowing or retrying a failed process. The measured consumer then starts in a fresh process on a fresh topic/group, retaining its original actual-workload warmup and every correctness check. All measured phases receive the same preparation; request DEBUG logging is not enabled there.

The new prepared traced smoke passes, with zero lost events in its 4.443-second trace. This validates the revised setup and collection mechanics only. Longer diagnostic results are reported in [the completed prepared sequence](LINUX-JIT-READY-RESULTS.md). No production retry policy, acceptance criterion or hosted performance gate changes.

## Reproduction and retention

Initial request-capture plan/driver commit: `016a0c2e1cb0481486f0d5a6fb71fb6a5d633f56`. Readiness plan/driver commit: `462f0e214df9525c1815c6db545e7331473ac2d4`. Consumer product A is `5df2f0d03607389384b5c1466e17812a9084fac9`; B is `2a550007ccb091e8f2bf9275a7646277e984dff8`; hosted fixture is `7614406221b63c0b758f25e0722c8a3bf3ef9d0f`. Current main is tooling-only successor `9eec358dad2a081dedbbfc75f02aee743e6bdad9`. These pinned old binaries are used for diagnosis, not new-head acceptance.

The local Linux image, runtime, CPU assignments and immutable input archive are specified in `COMMIT-EXCHANGE-PLAN.md`, `commit_exchange.py` and `LINUX-JIT-PLAN.md`. The consumer wrapper changes only the selected retained product directory through `DIAG_CONSUMER_LABEL`; the producer stays A. All task-owned request-capture containers and their anonymous volumes are removed after their request/server/GC/controller logs and inspection state are copied.

Durable evidence is retained outside removable worktrees at `C:/git/Dekaf-evidence/pr-3117/commit-exchange-20260908/`, including raw logs, exact source and wrapper inputs, parsed exchanges and the later diagnostic results. The earlier two untraced failures remain at `C:/git/Dekaf-evidence/pr-3117/linux-jit-20260908/`. This harness branch must not be merged into the product.
