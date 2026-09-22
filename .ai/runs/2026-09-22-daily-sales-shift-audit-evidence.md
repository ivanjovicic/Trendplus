Task ID: daily-sales-shift-audit-prompts
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-22
Agent/tool: GPT-5.6 Luna / Cloud Agent
Delivery target: main
Working branch / PR: cursor/daily-sales-audit-prompts-52eb / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Audited the `Prodaja po smeni` frontend, API service/endpoint/DTO, runtime Zod schema and focused tests.
- Confirmed four distinct follow-up contracts: signed quantity/revenue and reconciliation, data-scope diagnostics/denominator parity, off-shift/no-time assignment provenance and safe traceable endpoint errors.
- Added RQ381 as the current `READY` prompt and RQ382-RQ384 as dependent `WAITING` prompts.
- Routed Daily Sales ASCII Serbian and residual English findings to the existing RQ306/RQ325 localization owners.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-22-daily-sales-shift-audit-evidence.md

## Validation run
- Source/contract inspection of DailySalesStatsPage.tsx, dailySalesStatsApi.ts, analyticsResponseSchemas.ts, DailySalesStatsService.cs, DailySalesStatsEndpoints.cs, DailySalesStatsDto.cs and focused tests -> pass
- `git diff --check` -> pending
- Queue/instruction/planning validators -> pending

## Validation not run
- Runtime frontend/backend tests -> not run - this task records audit prompts only; no runtime implementation was made.
- Live browser/API/database smoke -> not run - no live environment evidence was requested or available for this audit.
- CI -> not inspected - not an acceptance gate for prompt creation.

## Documentation impact
- Updated the canonical RQ queue and master roadmap current pointer.
- Preserved existing RQ306/RQ325 ownership and added Daily Sales evidence to those prompts.

## What was missed
- No runtime fix was implemented; RQ381-RQ384 are implementation follow-ups.
- No live dataset was queried; reproductions are based on current code, DTO/schema contracts and focused fixtures.

## Risks
- The current runtime still rejects some legitimate signed Daily Sales metadata and can misstate fallback shift evidence until RQ381-RQ383 are implemented.
- Endpoint exception details may still expose raw provider text until RQ384 is implemented.

## Next
- Promote/implement RQ381 after this planning change reaches `main`, then RQ382/RQ383/RQ384 in dependency order.
