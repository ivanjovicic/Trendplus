Task ID: analytics-audit-prompts-2026-09-06
Queue: direct-user-request
Date: 2026-09-06
Agent/tool: Codex
Delivery target: none
Working branch / PR: main / none
Main commit SHA: pending
Main verification: not applicable - docs-only audit, no delivery commit created
Evidence state: synchronized

## What was done
- Re-read the required repository guidance and queue protocol.
- Audited current Dashboard, supplier and supplier-footwear analytics code,
  nearest tests and recent file history.
- Recorded four new, bounded prompt proposals in
  `docs/ai/ANALYTICS_RELIABILITY_AUDIT_PROMPTS_2026-09-06.md`.
- Kept the active `RQ163` queue status, lock and unrelated local changes
  untouched.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_AUDIT_PROMPTS_2026-09-06.md
- .ai/runs/2026-09-06-analytics-audit-prompts-evidence.md

## Validation run
- `rg` source/queue/history audit -> pass
- `git diff --check` -> pass

## Validation not run
- analytics guardrails -> not run - no canonical queue/runtime code change
- frontend/backend tests and builds -> not run - this was a docs-only audit
- browser console/theme/chart smoke -> not run - no runtime implementation
- queue validators -> not run - canonical queue was not edited because `RQ163`
  is active in the shared workspace
- live endpoint/schema/migration/refresh proof -> not run - outside this
  read-only audit delivery

## Documentation impact
- Added a dated audit prompt document with Problem, Evidence, Scope, Read first,
  Do, Tests, Acceptance and Dependencies for each candidate.
- The canonical queue was intentionally not edited to avoid colliding with the
  active `RQ163` owner and violating the one-READY rule.

## What was missed
- No production code was changed; the four proposals require separate claimed
  implementation runs.
- Live database, refresh, endpoint 404, migration and browser evidence remain
  owned by existing queue prompts such as `RQ145`/`RQ146` and were not claimed.

## Risks
- The proposed prompts are not runnable queue entries until the canonical
  queue owner integrates them after `RQ163`.
- Existing active local modifications are from another task owner and were not
  included in this audit evidence.

## Next
- After `RQ163` closes, integrate RQ229-RQ232 as WAITING entries and promote
  only one when the canonical queue pointer is advanced.
