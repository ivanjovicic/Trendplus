Task ID: direct-user-request-analytics-queue-scope
Queue: direct-user-request
Date: 2026-09-06
Agent/tool: Codex / local PowerShell
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: pending
Main verification: pending until documentation cleanup is committed and pushed
Evidence state: pending

## What was done
- Narrowed the active Analytics Reliability queue to production analytics screens and their directly supporting API/DTO, SQL/EF, cache/refresh, export/report and regression-test contracts.
- Marked standalone Trend Models/forecast evaluation, GMROI roadmap and embedding/similarity work obsolete for the current user scope.
- Removed the unintegrated 2026-09-06 additions file containing Python embedding, generic SQL security and admin API-key prompts.
- Corrected roadmap and historical queue next-pointer text so agents route to the current `RQ160` analytics prompt instead of completed RQ158/RQ159 work.
- Preserved historical DONE prompt bodies as audit evidence; obsolete prompts are no longer runnable candidates.

## Files changed
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NEW_ADDITIONS_2026-09-06.md` (removed)

## Validation run
- `node scripts/check-prompt-queues.mjs` -> pass, 371 tasks.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 planning tasks.
- `git diff --check` -> pass before final documentation commit.
- Scope scan of active RQ rows -> no READY/WAITING/IN_PROGRESS/PARTIAL standalone forecast, Trend Models, embedding/Python or GMROI prompt remains; historical DONE entries remain non-claimable.

## Validation not run
- Backend/frontend tests and builds -> not run; this change only curates queue and roadmap documents and does not change runtime code.
- Browser/production smoke -> not run; no runtime behavior changed.

## Documentation impact
- The owning queue now states the analytics-only scope and the rule that tests remain evidence, not standalone product functionality.
- `MASTER_ROADMAP.md` points to current `RQ160` and records excluded prompt families.

## What was missed
- Other global program queues (BCI, STAB, QDB, MT, GAI, PERF, OBS, SEC) were intentionally not deleted; they are separate governance programs, not analytics prompt candidates.

## Risks
- Historical addenda still contain older trend/forecast references for audit context, but their headers have no current READY pointer and the canonical queue marks excluded future prompts obsolete.

## Next
- Execute only the current analytics-screen prompt `RQ160`; do not promote excluded forecast, Trend Models, Python/embedding, vendor or generic platform prompts without a new explicit scope decision.
