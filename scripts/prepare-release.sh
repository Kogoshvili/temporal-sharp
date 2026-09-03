#!/usr/bin/env bash
# Prepares a release entirely locally: updates CHANGELOG.md, commits + tags
# (one commit, keeping MinVer's derived version correct), and pushes both.
# publish.yml picks the tag up automatically (build, test, pack, NuGet/GitHub
# release). Requires the local pusher to bypass the main-branch ruleset —
# the repo owner's account is a bypass actor; the github-actions app is not.
set -euo pipefail

version="${1:?Usage: prepare-release.sh <version> (e.g. 1.0.3)}"
version="${version#v}"
tag="v$version"

if [ -n "$(git status --porcelain)" ]; then
  echo "Working tree is not clean — commit or stash first." >&2
  exit 1
fi

git fetch origin main --tags --force
if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
  echo "Tag $tag already exists." >&2
  exit 1
fi

git checkout main
git pull --ff-only origin main

bash "$(dirname "$0")/update-changelog.sh" "$version"
git add CHANGELOG.md
if git diff --cached --quiet; then
  echo "CHANGELOG.md is up to date."
else
  git commit -m "Prepare $tag release"
fi

git tag "$tag"
git push origin main "$tag"
echo "Pushed $tag — publish.yml will build, test, pack, and publish."
