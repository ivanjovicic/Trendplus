Task ID: operations-audit-prompts-2026-09-25
Queue: direct-user-request
Date: 2026-09-25
Agent/tool: Grok Bot (executor subagent, local machine via Shell/Read; no cloud agent)
Delivery target: none
Working branch / PR: local `main` docs commit; no branch, no PR, no push per user request
Main commit SHA: pending
Main verification: skipped — committed locally on `main` (on top of `720a785a`), not pushed per user request; audit base `00accd93`
Evidence state: pending - local commit on `main`; push not approved

## What was done
- Audited the eight Operacije menu screens (Inventory, Supplier Sales alias, Shoe Type, Daily Sales, Pre/Post, Color, Pre-Nivelacija Priority, Supplier Footwear alias) from page to API client, endpoint and handler/SQL, and ran focused frontend/backend checks.
- Deduplicated findings against `RQ301`-`RQ426`, the 2026-09-18/21/22/23 audit notes and `docs/ai/OPERATIONS_SECOND_PASS_CLASSIFICATION_2026-09-25.md`.
- Added `RQ427`-`RQ437` to the canonical RQ queue (sections + status summary rows). Promoted `RQ427` (primary Current READY), `RQ428`, `RQ432`, `RQ435` and `RQ437` as collision-safe READY lanes in distinct feature families without shared owned files; kept `RQ429`-`RQ431`, `RQ433`, `RQ434`, `RQ436` WAITING behind same-file or owner-decision gates.
- Appended the residual English/technical copy, raw data-scope code, developer-copy and post-RQ306 ASCII residual list to `RQ325` (Evidence addendum, Scope, Dependencies). `RQ306` is DONE; only a post-completion routing note was added, its accepted scope was not rewritten.
- Mechanical same-owner repairs: section `Status:` lines of `RQ301`, `RQ302`, `RQ306`, `RQ308`, `RQ371` (WAITING -> DONE) and summary rows of `RQ306` (IN_PROGRESS -> DONE) and `RQ307` (WAITING -> DONE), each backed by an existing DONE completion note with synchronized evidence.
- Updated the queue header (Current READY pointer + owner audit note) and the `MASTER_ROADMAP.md` RQ routing row and owner note.
- Added the Serbian audit note `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-25.md`.

## Files changed
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-25.md` (new)
- `.ai/runs/2026-09-25-operations-audit-prompts-evidence.md` (new)

## Validation run
- Audit checks (before queue edits): `npm run typecheck` -> pass; targeted Vitest 74 files / 537 tests -> 533 pass, 4 fail (`InventoryPage.queueStatus.spec.tsx:331` -> RQ427; `DailySalesStatsPage.premium.spec.tsx:605-644` -> RQ431; `ColorSalesStatsPage.premium.spec.tsx:623` and `ExportSchedulerPanel.spec.tsx:91` -> RQ437); targeted `dotnet test Api.Tests` 245 tests -> 225 pass, 15 skipped, 5 fail (three stale assertions -> RQ437; two `CachedAnalyticsCriticalEndpointsIntegrationTests` fail on PostgreSQL authentication 28P01 -> environment, not proven).
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass (12 canonical files)
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (558 tasks)
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass (78 planning tasks)

## Validation not run
- Live browser/API smoke and PostgreSQL-backed integration tests -> not run; no database host with valid credentials was available and this task changes only queue/docs.
- Full frontend/backend suites -> not run; only targeted Operacije suites were executed.
- Remote CI -> not applicable; nothing was pushed.

## Documentation impact
- Canonical RQ queue: 11 new prompts, 5 READY lanes, RQ325 addendum, RQ306 residual routing note, 7 mechanical status repairs, new Current READY pointer `RQ427`.
- `MASTER_ROADMAP.md` RQ routing row now points to `RQ427` and names the additional READY lanes.
- New dated Serbian audit note.

## What was missed
- No runtime fix was implemented for any finding.
- Older legacy section/table status mismatches without completion notes (`RQ128`, `RQ132`, `RQ141`, `RQ143`, `RQ145`-`RQ149`, `RQ191`, `RQ192`) and the `RQ135`/`RQ138` completion-status oddities were left untouched because they are not clear mechanical repairs.
- Pre-existing stale local locks `.ai/task-locks/RQ352..RQ358-cursor.lock.md` were observed and left untouched (not created by this run).
- Supplier hub (canonical `/analytics/supplier`) was not re-audited in depth.

## Risks
- Changes are committed locally on `main` but not pushed; another agent reading `origin/main` will not see the new prompts until the user approves a push.
- `RQ431` requires a business decision on the signed concentration contract before implementation.
- Line numbers in prompts refer to `00accd93`; implementers must re-verify after intervening commits.

## Next
- Implement `RQ427` from the Current READY pointer; `RQ428`, `RQ432`, `RQ435` and `RQ437` are independently claimable.
- Commit this docs change when the user approves (one docs commit), then synchronize `Main commit SHA`/`Evidence state`.
