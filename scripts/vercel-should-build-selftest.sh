#!/usr/bin/env bash
# Self-test for scripts/vercel-should-build.sh (RQ633).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="${ROOT}/scripts/vercel-should-build.sh"

run() {
  local label="$1"
  local expected="$2"
  shift 2
  set +e
  local out
  out="$(env "$@" bash "${SCRIPT}" 2>&1)"
  local code=$?
  set -e
  if [[ "${code}" -ne "${expected}" ]]; then
    echo "FAIL: ${label} expected exit ${expected}, got ${code} (output: ${out})" >&2
    exit 1
  fi
  echo "ok: ${label}"
}

# Missing SHAs -> build (exit 1)
run "missing previous sha" 1 VERCEL_GIT_COMMIT_SHA=abc
run "missing commit sha" 1 VERCEL_GIT_PREVIOUS_SHA=abc

# Docs-only commit f35e5656 -> skip (exit 0)
run "docs-only f35e5656" 0 \
  VERCEL_GIT_PREVIOUS_SHA="$(git -C "${ROOT}" rev-parse f35e5656^)" \
  VERCEL_GIT_COMMIT_SHA=f35e5656

# Recent frontend change -> build (exit 1)
run "clientapp change" 1 \
  VERCEL_GIT_PREVIOUS_SHA=fd2d0fb0242f5c105ef90d01a0e33c0da053c33e \
  VERCEL_GIT_COMMIT_SHA=a3e621f74f9b3af1391714280fdd20e8b2b6f41a

echo "vercel-should-build self-test passed"
