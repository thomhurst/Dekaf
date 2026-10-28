# Dekaf

Read [AGENTS.md](../../../../AGENTS.md) for validation, public contracts, and performance acceptance. It owns the zero-allocation hot-path rules, benchmark evidence, stress-run budget, and Pareto gate; review/simplification must preserve them.

- Docs-site changes require `npm run build --prefix docs`.
- There is no Aspire AppHost. Integration tests use Docker/Testcontainers Kafka; stop only resources created for this work.
- Run before/after benchmarks on the same GitHub Actions `ubuntu-latest` runner VM as required by [AGENTS.md](../../../../AGENTS.md#performance-gate). Local measurements are diagnostic only. No local Redis `performance` lock is required for heavy work; retain PR/issue ownership locks.

## Draft review lifecycle

Draft policy: [AGENTS.md](../../../../AGENTS.md) applies the skill's draft review lifecycle to every agent-created PR, inside or outside the loop. Before creating or promoting a PR from an existing worktree, re-read `AGENTS.md` and this skill from the shared checkout; branch copies can predate the current policy.

Required reviewers are Claude Code Review and Codex. Other review bots (Greptile, CodeRabbit) are not approvers, but their findings are review surfaces like any other.

- **Claude:** `.github/workflows/claude-code-review.yml` reviews drafts on open, push, and reopen, not on `ready_for_review`. It posts one `github-actions[bot]` issue comment per run whose first line is `<!-- REVIEW_RUN: <run>/<attempt>; HEAD: <full-head-SHA> -->` and whose last line is `<!-- REVIEW_VERDICT: CLEAR -->` or `<!-- REVIEW_VERDICT: BLOCKING -->`. A positive verdict is `CLEAR` with `HEAD` equal to the current head. When a draft head has no review (a failed or cancelled run), dispatch it once: `gh workflow run claude-code-review.yml -f pr_number=<N>`. The workflow runs on `pull_request_target`, so it always uses `main`'s workflow file; a PR that changes the workflow is reviewed by the current `main` version.
- **Codex:** `chatgpt-codex-connector[bot]` reviews automatically when a PR opens ready or leaves draft, and on drafts only on request. Its mutable `<!-- codex-pull-request-review-summary -->` issue comment carries `<!-- codex-review-state:v1 {...} -->`; a positive verdict has `code_review.commit_sha` equal to the full current head, `status: completed`, and `has_any_findings: false`. A thumbs-up from `chatgpt-codex-connector[bot]` on this head's request (verify the author through `issues/comments/<id>/reactions`) also counts. Inline findings arrive as review threads. Re-read the summary on each visit; never infer the reviewed commit from timing.

Automated review rounds (rework budget): count Claude review comments with `gh api --paginate "repos/{owner}/{repo}/issues/<N>/comments" --jq '.[] | select(.user.login == "github-actions[bot]" and (.body | startswith("<!-- REVIEW_RUN:"))) | .id' | wc -l`.

Draft CI: `ci.yml`, `performance-harness-tests.yml`, and `review-helper-tests.yml` skip their jobs on draft PR events before renting a build runner, and `performance-gate.yml` never measures a draft. `ready_for_review` starts them on the approved head; `converted_to_draft` cancels an in-flight CI, performance-harness, or performance-gate run through workflow concurrency. The required `CI Passed` check reports skipped on drafts and runs when the PR is marked ready. `Dependency Review` (the other required check), `Repository artifact policy`, and the Claude review still run on drafts. `Assert-PrGreen.ps1` denies drafts (`mergeStateStatus` is `DRAFT`).
