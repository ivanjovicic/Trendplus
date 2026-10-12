#!/usr/bin/env bash
# Self-test for scripts/vercel-should-build.sh (RQ633).
# Uses only commits present in the local clone (CI-friendly).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="${ROOT}/scripts/vercel-should-build.sh"
PATHS=(vercel.json Klijent/clientapp)

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

find_docs_only_pair() {
  local child parent
  while read -r child; do
    parent="$(git -C "${ROOT}" rev-parse "${child}^" 2>/dev/null)" || continue
    if git -C "${ROOT}" diff --quiet "${parent}" "${child}" -- "${PATHS[@]}" \
      && ! git -C "${ROOT}" diff --quiet "${parent}" "${child}"; then
      echo "${parent} ${child}"
      return 0
    fi
  done < <(git -C "${ROOT}" rev-list --max-count=200 HEAD)
  return 1
}

find_frontend_pair() {
  local child parent
  while read -r child; do
    parent="$(git -C "${ROOT}" rev-parse "${child}^" 2>/dev/null)" || continue
    if ! git -C "${ROOT}" diff --quiet "${parent}" "${child}" -- "${PATHS[@]}"; then
      echo "${parent} ${child}"
      return 0
    fi
  done < <(git -C "${ROOT}" rev-list --max-count=200 HEAD)
  return 1
}

# Missing SHAs -> build (exit 1)
run "missing previous sha" 1 VERCEL_GIT_COMMIT_SHA=abc
run "missing commit sha" 1 VERCEL_GIT_PREVIOUS_SHA=abc

docs_pair="$(find_docs_only_pair)" || {
  echo "FAIL: could not find a docs-only parent/child pair in local history (need fetch-depth > 1 in CI)" >&2
  exit 1
}
docs_prev="${docs_pair%% *}"
docs_cur="${docs_pair#* }"
run "docs-only pair ${docs_cur:0:8}" 0 \
  VERCEL_GIT_PREVIOUS_SHA="${docs_prev}" \
  VERCEL_GIT_COMMIT_SHA="${docs_cur}"

frontend_pair="$(find_frontend_pair)" || {
  echo "FAIL: could not find a frontend-touching parent/child pair in local history" >&2
  exit 1
}
fe_prev="${frontend_pair%% *}"
fe_cur="${frontend_pair#* }"
run "frontend pair ${fe_cur:0:8}" 1 \
  VERCEL_GIT_PREVIOUS_SHA="${fe_prev}" \
  VERCEL_GIT_COMMIT_SHA="${fe_cur}"

echo "vercel-should-build self-test passed"
