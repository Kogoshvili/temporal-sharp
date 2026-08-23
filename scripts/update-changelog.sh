#!/usr/bin/env bash
set -euo pipefail

# Regenerates CHANGELOG.md from git history. Each release section summarizes the
# commits since the previous version tag.
#
#   scripts/update-changelog.sh          # [Unreleased] holds commits since the latest tag
#   scripts/update-changelog.sh 1.0.0    # promote HEAD to a [1.0.0] release section
#
# The release workflow calls this with a version *before* tagging, so the
# CHANGELOG.md update and the version tag are the same commit. That avoids the
# extra post-tag commit which would otherwise bump MinVer's derived version.

cd "$(git rev-parse --show-toplevel)"

readonly OUT="CHANGELOG.md"

NEXT_VERSION="${1:-}"

# All version tags, oldest first. Sort by creation date (commit date for
# lightweight tags) so pre-release tags like v1.0.0-beta.1 order correctly,
# unlike `version:refname` which mis-orders SemVer pre-releases.
mapfile -t tags < <(git tag --sort=creatordate --list 'v*')

header() {
  cat <<'EOF'
# Changelog

All notable changes to this project are documented in this file. Each section
summarizes the commits since the previous release, and is generated
automatically when a release is prepared.

EOF
}

commits_in() {
  local range="$1"
  git log --no-merges --format='- %s' "$range" \
    | grep -v -E '^- Regenerate RULES\.md$' || true
}

print_section() {
  local tag="$1" prev="$2"
  local range
  if [[ -n "$prev" ]]; then
    range="$prev..$tag"
  else
    range="$tag"
  fi

  printf '## [%s] - %s\n\n' "${tag#v}" "$(git log -1 --format=%cs "$tag")"
  commits_in "$range"
  printf '\n'
}

latest_tag() {
  [[ ${#tags[@]} -gt 0 ]] && printf '%s' "${tags[${#tags[@]}-1]}"
}

{
  header

  latest="$(latest_tag)"

  if [[ -n "$NEXT_VERSION" ]]; then
    # Promote HEAD into a release section for the version about to be tagged.
    printf '## [%s] - %s\n\n' "$NEXT_VERSION" "$(date +%F)"
    if [[ -n "$latest" ]]; then
      commits_in "$latest..HEAD"
    else
      commits_in HEAD
    fi
    printf '\n'
  elif [[ -n "$latest" ]]; then
    if [[ "$(git rev-parse "$latest")" != "$(git rev-parse HEAD)" ]]; then
      printf '## [Unreleased]\n\n'
      commits_in "$latest..HEAD"
      printf '\n'
    fi
  else
    printf '## [Unreleased]\n\n'
    commits_in HEAD
    printf '\n'
  fi

  # Release sections, newest first.
  for ((i = ${#tags[@]} - 1; i >= 0; i--)); do
    tag="${tags[i]}"
    prev=""
    if ((i > 0)); then
      prev="${tags[i-1]}"
    fi
    print_section "$tag" "$prev"
  done
} > "$OUT"

echo "Wrote $OUT"
