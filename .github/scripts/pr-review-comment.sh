#!/usr/bin/env bash
# Review-posting helper for the Claude Code Review workflow.
#
# That workflow runs on pull_request_target so it can review pull requests from
# forks, which means the diff it analyses is untrusted while the job holds real
# `pull-requests: write`. This script is the only write path exposed to the
# model: the pull request number comes from the environment rather than an
# argument, so an injected instruction cannot retarget another PR, and the body
# is passed directly rather than read from a path, so no file on the runner can
# be turned into a public comment.
#
# Usage:
#   pr-review-comment.sh "<markdown body>"
set -euo pipefail

: "${PR_NUMBER:?PR_NUMBER must be set by the workflow}"
: "${GH_REPO:?GH_REPO must be set by the workflow}"

: "${REVIEW_RECEIPT:?REVIEW_RECEIPT must be set by the workflow}"

python3 "$(dirname -- "$0")/pr_review_comment.py" post "${1:-}"
