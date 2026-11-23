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

github_env=${GITHUB_ENV:-/dev/null}
github_output=${GITHUB_OUTPUT:-/dev/null}

if [ "${GITHUB_EVENT_NAME:-}" = "release" ]; then
  tag=${RELEASE_TAG:-}
  # SemVer 2.0 without build metadata: no leading zeros in major.minor.patch or in a numeric
  # prerelease identifier (beta.0 and beta01 are fine, beta.01 and 01 are not), no empty
  # identifiers. The version is the package version only; AssemblyVersion stays as
  # Directory.Build.props sets it, so the components have no 16-bit limit.
  number='(0|[1-9][0-9]*)'
  identifier='(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
  pattern="^v?$number\\.$number\\.$number(-$identifier(\\.$identifier)*)?\$"
  if [[ ! "$tag" =~ $pattern ]]; then
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
