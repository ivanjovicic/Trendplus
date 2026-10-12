#!/usr/bin/env bash
# Vercel ignoreCommand helper: exit 0 to skip the build, exit 1 to run it.
# Skips when the frontend surface and root deploy config are unchanged between SHAs.

set -euo pipefail

if [[ -z "${VERCEL_GIT_PREVIOUS_SHA:-}" ]]; then
  exit 1
fi

if [[ -z "${VERCEL_GIT_COMMIT_SHA:-}" ]]; then
  exit 1
fi

paths=(
  vercel.json
  Klijent/clientapp
)

if git diff --quiet "${VERCEL_GIT_PREVIOUS_SHA}" "${VERCEL_GIT_COMMIT_SHA}" -- "${paths[@]}"; then
  exit 0
fi

exit 1
