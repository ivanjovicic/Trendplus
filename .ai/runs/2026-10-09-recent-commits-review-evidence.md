Task ID: recent-commits-review-2026-10-09 (owner request; registers RQ601-RQ603)
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md (+ UI table/chart addendum note on RQ46)
Date: 2026-10-09
Agent/tool: Grok Bot (box)
Delivery target: main (pushed from the owner's PC)
Review range: `02a539b1..da826635` (94 commits, 2026-10-08 11:00 - 2026-10-09 14:51 Europe/Belgrade)
Evidence state: pending (final main SHA recorded by the pushing agent)

## Review summary

- ~65 of 94 commits are queue/evidence/routing documentation; 29 carry code or tests.
- Code-bearing work is mostly sound: RQ481 Pulse lineage + PR #119 fail-closed URL IDs, RQ319 explicit Apply, RQ320 draft chip, RQ140 detail-export parity (ea31b2b0/3ebf57f8), RQ46/RQ50 exports and chart scope, RQ597 Color oracle (e5e25e90/56923cf8), BCI15/BCI16 test hygiene, PR #112 dependent return MV, RQ556 v9 shadow, RQ585 digest, RQ593/RQ596/RQ594 follow-ups.
- BCI16 (eb37682f) removed only tautological assertions on local constants; no production behaviour lost. BCI15 (e5e2f161) strengthened coverage. 56923cf8 replaced a presence check with exact units/revenue for the boundary bucket.
- CI: latest Analytics Quality Gates (`4f765418`, run 37925923933) and Analytics Tests & Data Integrity (`9a35f3e6`, run 37824273816) are green; red runs in the range (4c66ebec, 739815e3, ff53b481, c2e35ae5, e5e25e90, f3eb7ec4, 4c8ed885/4f765418 governance) were fixed by later commits. Backend-path workflow did not run after 9a35f3e6 because later commits touched no backend paths.
- Queue hygiene: no `.ai/task-locks` remain, no IN_PROGRESS prompts, validators pass. Non-conventional commit subjects: `rq598-*` (5 commits), `Claim RQ481 ...`, `close RQ481 ...`, `Add Pre-Nivelacija v9 shadow comparison`, `Promote RQ585 ...`, `Close RQ556 ...` (reported only).

## Problems found and fixed

1. RQ319 gap: Shoe Type period preset and season still applied immediately and carried an unapplied store draft with them. Now draft-only like Color/Pre-Post (test proves it fails on the old code).
2. RQ320 gap: Shoe Type had no `Nije primenjeno` cue after becoming Apply-required. Added the shared chip.
3. RQ481 UX/semantics: every successful Pulse response is marked partial with `PULSE_FILTER_NOT_APPLIED`; the page showed the source-outage alert ("delimično dostupan", "podaci mogu biti nepotpuni", retry) and "Izvori su delimično dostupni" although all sources answered. Now a neutral note explains the inventory period; genuine partial/suppressed responses keep the alert. CSV exports Serbian labels instead of `period:inventory`/`inventory`. 11px text raised to 12px.
4. RQ46: Insight Studio export metadata used English labels and raw true/false/status codes; now Serbian labels, Da/Ne, shared quality labels. "Kriticno" diacritics fixed in the shared quality label and Insight Studio columns; chart scope label rephrased.
6. Full Vitest found `AppAnalyticsRoutes.spec.tsx` red on gh/main: `/analytics/decision-pulse` is a registered core smoke route without a test-id mapping/mock. CI misses it because Analytics Quality Gates runs only `npm run test:analytics`. Mapping added.
5. RQ556: experimental v9 methodology string was English in the UI; copy translated ("shadow", "Sell-through" removed). Missing `SeasonId` was looked up as season 0 (Access IDs can be 0); now skipped.

## Branch decisions (none merged)

- `fix/rq481-invalid-filter-id-failclosed-20261008` (+10): identical tree content to main for both files; squash-merged as PR #119 (f3eb7ec4).
- `docs/owner-decisions-rq481-rq319-20261008` (+1): same patch-id as main (`git cherry` "-").
- `codex/product-value-plan-refinement` (+2): branch tip tree equals main commit d6039395 (rebased/squashed).
- `cursor/p-ui-52-recovery-61ea` (+1) and `cursor/p-ui-52-close-61ea` (+2): P-UI-52 is DONE with synchronized evidence on main; branch versions are older drafts of the same closure.
- `cursor/queue-idle-recovery-1156-61ea` (+1): a 2026-10-08 zero-READY proof superseded by later recoveries and by today's READY registrations; merging it would add a stale routing claim.

## New prompts

- RQ601 READY P1 - purchase-cost scale reconciliation before inventory capital is shown in RSD.
- RQ602 READY P1 - RQ592 pre-registration kit and reproducible measurement dry-run on historical data.
- RQ603 WAITING P2 - reduce primary analytics navigation (owner decision; revises RQ507; includes duplicate "Prodaja po dobavljačima" label).
- RQ592 dependency note: RQ545 is DONE, so STAB16 is the only start gate.

## Validation run

- Vitest focused: Shoe Type (6 files, 74), DecisionPulsePage 21, InsightStudioPage 6, PreNivelacijaPriorityPage 59, AnalyticsDashboard.operationalFallback 2 -> pass. Full suite: 241 files / 1934 tests; the only 2 failures were the pre-existing route-smoke gap (item 6), now fixed (21/21).
- `npm run check:analytics-guardrails` (encoding, guardrail self-test, 39 known / 0 new, typecheck) -> pass; `check:ui-ratchets` -> pass; eslint on touched files -> no new errors versus gh/main (pre-existing errors unchanged).
- `dotnet test --filter DecisionPulse|PreNivelacija` with Testcontainers (CI=true) -> 98/98 pass.
- `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs`, `git diff --check` -> pass.

## Validation not run

- Live production/browser checks (no provider access); full backend suite (CI owns it).

## Risks / next

- STAB16 remains the owner-only blocker for fresh data; RQ601/RQ602 are the highest-value repo-local work until then.
