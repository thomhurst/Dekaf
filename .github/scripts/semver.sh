#!/usr/bin/env bash
# Release-tag grammar and SemVer 2.0 precedence, shared by resolve-version.sh and
# update-release-draft.sh. Source it; it defines functions only.
#
# Grammar: an optional v, then SemVer 2.0 without build metadata. No leading zeros in
# major.minor.patch or in a numeric prerelease identifier (beta.0 and beta01 are fine, beta.01 and
# 01 are not), no empty identifiers. The version is the package version only; AssemblyVersion stays
# as Directory.Build.props sets it, so the components have no 16-bit limit.

_semver_number='(0|[1-9][0-9]*)'
_semver_identifier='(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
SEMVER_TAG_PATTERN="^v?$_semver_number\\.$_semver_number\\.$_semver_number(-$_semver_identifier(\\.$_semver_identifier)*)?\$"

# Succeeds when $1 is a valid release tag or version.
is_version() {
  [[ "$1" =~ $SEMVER_TAG_PATTERN ]]
}

# Compares two non-negative integers without leading zeros, of any length: prints -1, 0 or 1.
_semver_compare_numbers() {
  if [ "${#1}" -ne "${#2}" ]; then
    [ "${#1}" -lt "${#2}" ] && echo -1 || echo 1
  elif [ "$1" = "$2" ]; then
    echo 0
  elif [[ "$1" < "$2" ]]; then
    echo -1
  else
    echo 1
  fi
}

# Prints -1, 0 or 1 as version $1 has lower, equal or higher SemVer 2.0 precedence than $2. Both
# must satisfy is_version; a v prefix is ignored.
semver_compare() {
  local LC_ALL=C
  local a=${1#v} b=${2#v}
  local a_core=${a%%-*} b_core=${b%%-*} a_pre="" b_pre=""
  [ "$a" != "$a_core" ] && a_pre=${a#*-}
  [ "$b" != "$b_core" ] && b_pre=${b#*-}

  local IFS=.
  local -a x y
  x=($a_core)
  y=($b_core)
  local i result
  for i in 0 1 2; do
    result=$(_semver_compare_numbers "${x[$i]}" "${y[$i]}")
    if [ "$result" != 0 ]; then
      echo "$result"
      return
    fi
  done

  # A version without a prerelease is higher than the same version with one.
  if [ -z "$a_pre" ] || [ -z "$b_pre" ]; then
    if [ -z "$a_pre" ] && [ -z "$b_pre" ]; then echo 0; elif [ -z "$a_pre" ]; then echo 1; else echo -1; fi
    return
  fi

  x=($a_pre)
  y=($b_pre)
  local n=${#x[@]}
  [ "${#y[@]}" -lt "$n" ] && n=${#y[@]}
  local p q
  for ((i = 0; i < n; i++)); do
    p=${x[$i]}
    q=${y[$i]}
    if [[ "$p" =~ ^[0-9]+$ && "$q" =~ ^[0-9]+$ ]]; then
      result=$(_semver_compare_numbers "$p" "$q")
    elif [[ "$p" =~ ^[0-9]+$ ]]; then
      result=-1  # numeric identifiers are lower than alphanumeric ones
    elif [[ "$q" =~ ^[0-9]+$ ]]; then
      result=1
    elif [ "$p" = "$q" ]; then
      result=0
    elif [[ "$p" < "$q" ]]; then
      result=-1
    else
      result=1
    fi
    if [ "$result" != 0 ]; then
      echo "$result"
      return
    fi
  done

  # All shared identifiers are equal: the longer list is higher.
  _semver_compare_numbers "${#x[@]}" "${#y[@]}"
}

# Succeeds when version $1 has higher precedence than version $2.
semver_gt() {
  [ "$(semver_compare "$1" "$2")" = 1 ]
}
