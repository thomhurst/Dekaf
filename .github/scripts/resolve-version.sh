#!/usr/bin/env bash
# Resolves the version CI stamps on the build and the NuGet packages, and exports it as
# DEKAF_VERSION for tools/Dekaf.Pipeline (GenerateVersionModule).
#
# A published release (GITHUB_EVENT_NAME=release) takes the version from its tag, so editing the
# tag of the draft release is how a version is chosen. The tag must be a semantic version with an
# optional v prefix (v1.2.3, 1.2.3, v2.0.0-beta.1), and the tagged commit must be on the default
# branch: a release can only ship what main has.
#
# Every other run uses the version GitVersion computed (GITVERSION_SEMVER). When it is empty (PR
# runs tolerate a GitVersion failure), nothing is exported and the pipeline falls back to its own
# version lookup.
#
# Inputs (environment): GITHUB_EVENT_NAME, GITHUB_SHA, RELEASE_TAG, GITVERSION_SEMVER,
# DEFAULT_BRANCH (default main), GITHUB_ENV, GITHUB_OUTPUT.
set -euo pipefail
# Command substitutions inherit set -e (bash 4.4+, which the runners have; older bash skips it).
shopt -s inherit_errexit 2>/dev/null || true

# shellcheck source=semver.sh
source "$(dirname "${BASH_SOURCE[0]}")/semver.sh"

github_env=${GITHUB_ENV:-/dev/null}
github_output=${GITHUB_OUTPUT:-/dev/null}

if [ "${GITHUB_EVENT_NAME:-}" = "release" ]; then
  tag=${RELEASE_TAG:-}
  # The grammar lives in semver.sh.
  if ! is_version "$tag"; then
    echo "::error::Release tag '$tag' is not a version. Use a tag such as v1.2.3 or v1.2.3-beta.1."
    exit 1
  fi

  branch=${DEFAULT_BRANCH:-main}
  if ! git merge-base --is-ancestor "$GITHUB_SHA" "origin/$branch"; then
    echo "::error::Release $tag points at $GITHUB_SHA, which is not on $branch."
    exit 1
  fi

  version=${tag#v}
else
  version=${GITVERSION_SEMVER:-}
  if [ -z "$version" ]; then
    echo "::warning::GitVersion produced no version; the pipeline computes its own."
    exit 0
  fi
fi

echo "Version: $version"
echo "DEKAF_VERSION=$version" >> "$github_env"
echo "semVer=$version" >> "$github_output"
