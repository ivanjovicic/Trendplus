# Analytics PR Status

Datum: 2026-06-16

## PR #1

- PR number: `1`
- title: `[codex] publish all changes`
- state: `open`
- draft: `true`
- mergeable: `false`
- head branch: `codex/publish-all-changes`
- head SHA: `6278af4f1ab4ca255faf4cbb85ae2525e1edb1e2`
- base branch: `main`
- base SHA: `783adbc3858ccaf5ea9edc9f3cdd34b90aaf9f1e`
- stale: `yes`
- contains latest commits:
  - `783adbc` `fix(analytics): satisfy guardrails` -> `no`
  - `0cddd32` `test(inventory): cover decision summary quality warning` -> `no`
  - `ecb6b22` `feat(analytics): add pilot readiness checklist` -> `no`
- recommendation: `close/recreate`

Notes:
- PR #1 is still pointed at `codex/publish-all-changes` with head `6278af4`, while `main` has moved to `783adbc`.
- Local ancestry checks confirm that the three latest analytics commits requested for verification are not present in the PR head.
- The PR should not be merged in its current state.
