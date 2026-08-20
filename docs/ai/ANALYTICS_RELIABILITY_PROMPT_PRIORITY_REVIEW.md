# Analytics Reliability Prompt Priority Review

Date: 2026-06-28
Routing reviewed: 2026-08-10
Repo: `ivanjovicic/Trendplus`
Status: planning/review only; no runtime code changed

## Purpose

This document is the compact execution index for analytics reliability prompts. Use it to avoid reading every queue addendum before each agent run.

Primary goals:

- reduce token usage
- prevent duplicate work across queues
- make the next task obvious
- keep runtime changes small and testable
- preserve analytics trust: no fake zero, no fake green, no fake measured, no fake recommendation, no hidden fallback

## Reviewed queues

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` - RQ01-RQ13 + RQ106 + RQ106
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md` - Q69-Q82
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md` - RQ13-RQ24
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md` - RQ25-RQ38
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md` - RQ39-RQ50
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md` - RQ51-RQ63
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` - RQ64-RQ71 + RQ89 + RQ96-RQ98
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md` - RQ72-RQ80
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ACTION_OUTCOME_ADDENDUM.md` - RQ81-RQ88 + RQ90
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` - RQ100-RQ104 (`RQ100`-`RQ104` DONE)
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `docs/ai/ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md`

## How an agent should use this document

For a normal implementation run, read only:

1. `docs/ai/AGENT_START_HERE.md`
2. `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
3. this file
4. the single target prompt section from its queue file
5. the audit file named by that prompt
6. `docs/ai/ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md` only when this index says the prompt was contract-gated or has a prepared default
7. source/test files in `Scope only`

Do not read every addendum unless the target prompt's `Merge / split rule` says to read a sibling prompt.

## Current BCI assertion-repair override (2026-08-10)

The generic analytics lane order below is temporarily superseded by a concrete backend-CI repair sequence produced by `BCI04` and refreshed by `BCI05` evidence.

1. `RQ89`–`RQ95`, `BCI08`, and `BCI09` are DONE.
2. `BCI05` is READY: local `Api.Tests` Release build is restored on `469acbf`. Capture green GHA on a commit that includes that stub fix.
3. Only a later green GHA run on current backend-equivalent `main` may move `BCI01` from PARTIAL to DONE.
4. If the suite exposes a new root-cause family, create/reuse one focused prompt; do not weaken tests.
5. Post-BCI inventory-foundation prompt `RQ96` is DONE after the observed snapshot foundation landed on 2026-08-19. `RQ97`-`RQ98` remain WAITING.
6. Routing update 2026-08-19: `MASTER_ROADMAP.md` is authoritative. Sequential refill is complete. Current execution is `RL10`. Parallel-safe planning READYs are `OBS10` and `RL10`. `PERF16` is BLOCKED on `MT10`. Do not revive `RQ89`/`RQ90` as READY.

Evidence: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-10.md`, `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-10_REENTRY.md`, `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11.md`, `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11_RQ95_REENTRY.md`, `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-13.md`, `docs/qa/BACKEND_CI_CACHE_FOOTPRINT_STUB_EVIDENCE_2026-08-13.md`

## Global execution lanes

Run one prompt per feature family unless explicitly marked as a read-together family below.

### Lane A - actionable impact and recommendation trust

Do first because these can create wrong actions or wrong financial priority.

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| A1 | RQ01 | Fix backend Decision Board product expected-impact fallback. | Keep runtime scope backend/tests only. Read RQ72 for same failure class but do not mix frontend fallback unless task is explicitly expanded. |
| A2 | RQ72 | Fix Executive fallback product lost-sales expected-impact fallback. | Run after RQ01 so frontend fallback follows the backend contract. |
| A3 | RQ51/RQ52 | Color insufficient/missing recommendation authority. | Can be one small frontend authority task if enum/count/export surfaces are tested together. |
| A4 | RQ59/RQ73 | Inventory signal-review expected impact in Inventory + Executive. | Prefer shared vocabulary from hardening addendum. If touching same helper, fix both surfaces in one scoped task; otherwise RQ59 first, RQ73 after. |
| A5 | RQ74 | Supplier revenue ranking vs expected impact display. | Prepared default exists: treat revenue as context, not expected impact. Read `ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md`. |

### Lane B - 100x numeric/display/export risk

Do immediately after Lane A because these can show materially wrong values.

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| B1 | RQ39 | Derived category `revShare` ratio vs percent-unit mismatch. | Contract fixed by hardening addendum: `CategoryStat.revShare` is percent unit unless breaking migration is approved. |
| B2 | RQ40 | Supplier Decision percent export/detail parity. | Do before RQ41/RQ42 XLSX/detail typing. |
| B3 | RQ79 | Pilot intake durable report percent ratio rendered as raw number. | Same unit family as RQ40, but separate backend report file. Small after RQ40 or immediately if report is being used. |
| B4 | RQ41/RQ42/RQ46 | Export/detail type/trust metadata preservation. | Read together, but implement only after percent units are normalized. |

### Lane C - fake zero / fake green / fake measured

This lane prevents missing evidence from looking safe.

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| C1 | RQ64 | Inventory snapshot null evidence must not become `0/info/false`. | Covers forecast, rebalance, alerts and size-curve. Fold RQ71 into this unless size-curve needs a separate DTO contract. |
| C2 | RQ60/RQ67 | Inventory missing cost/value and forecast workflow value trust. | RQ60 establishes row value contract; RQ67 follows for workflow payload. |
| C3 | RQ04/RQ75 | Data Quality no-data/no-sales must not show green. | Treat as one fake-green family. Backend/Decision Board first if RQ04 is active; DataQualityPage surface from RQ75 after or in same scoped PR if tests are small. |
| C4 | RQ03/Q80 | Lost-sales unavailable vs true zero/source confidence. | Do not implement independently. Choose one source-status vocabulary and mark the other as SQL-specific follow-up or obsolete. |
| C5 | RQ81 | `not_measured` must not get fake measured timestamp. | Ready-quality contract, but remains WAITING while the BCI repair override is active. |
| C6 | RQ86 | Authoritative outcome status needs evidence or qualitative label. | Prepared staged default exists, but hard validation remains contract-gated. Read prep doc; run after RQ81. |

### Lane D - date, period and denominator contracts

Do before interpreting trends and outcome rates.

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| D1 | RQ13 | Advanced/V2 date-only `toDate` whole-day/half-open fix. | Use shared date contract, but do not expand beyond allowed scope. |
| D2 | RQ25 | Legacy Advanced same date boundary class. | Run after RQ13 or create one shared helper task if safe. |
| D3 | RQ26 | Current/previous period boundary overlap. | Separate from D1/D2 because comparison semantics need exact fixture. |
| D4 | RQ02/RQ12 | Product Decision Center returned/top/all-row denominators. | Can be one PDC denominator contract task if scope stays backend/tests/docs. |
| D5 | RQ65/RQ77 | Returned count vs total matching count/truncation. | Same count vocabulary prepared in prep doc; different modules. Do not share DTOs unless natural. |
| D6 | RQ82/RQ83/RQ84 | Action outcome lifecycle/rate/impact-sample denominators. | Prepared additive contract exists. Read prep doc. Prefer docs/tests first, then additive runtime fields. |
| D7 | RQ85 | Outcome summary default created/resolved/measured window. | Prepared default exists: keep current created cohort but label it clearly. Changing default remains contract-gated. |

### Lane E - dataScope, store and filter lineage

Do after immediate wrong-impact and fake-green fixes unless the user is testing scoped data.

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| E1 | RQ05/Q81 | Canonical dataScope/store/supplier matrix. | Prepared first task: matrix/audit only, no runtime fixes. Read prep doc. |
| E2 | RQ53/RQ54 | Color/ShoeType and Vendor list/detail/current/previous scope parity. | Run after E1 if possible; otherwise document assumed contract. |
| E3 | RQ68/RQ69 | Inventory signal search/store lineage. | Can be one frontend/API contract task if labels and requests are tested. |
| E4 | RQ78/RQ06 | Data Quality top-offender revenue impact dataScope. | RQ78 is the more precise later prompt. Do not run both independently. |

### Lane F - workflow completeness and lower-risk trust UX

| Rank | Prompt | Action | Merge / split rule |
|---|---|---|---|
| F1 | RQ80/RQ07 | Missing-cost issue workflow. | RQ80 is the updated workflow prompt; RQ07 is older evidence. Use RQ80 as implementation prompt. |
| F2 | RQ87 | Self-contained outcome resolution ledger. | After RQ86 settles evidence contract. |
| F3 | RQ88 | Split/relabel done vs rejected KPI. | Small UX task. |
| F4 | RQ76 | One-point trend should be neutral/no-trend. | Small UX trust task. |
| F5 | RQ43/RQ50/RQ55/RQ56/RQ63 | Report preview, top-N labels, denominator/naming cleanup. | Later cleanup; do not block P0/P1 correctness. |

### SQL lane

SQL queue remains a separate lane:

1. Q69 first: docs/tests audit only.
2. Q70 after Q69: nivelacija zero-baseline.
3. Q71/Q77/Q80/Q81 only after Q69 defines shared SQL trust vocabulary.

SQL prompts should not be merged with frontend fixes unless a new prompt explicitly permits it.

## Updated top-20 recommended sequence

The list below is the generic analytics order retained for when the BCI assertion-repair override is closed. While RQ89/RQ90 are active, the override above wins.

1. RQ01
2. RQ72
3. RQ39
4. RQ40
5. RQ51/RQ52
6. RQ57/RQ58
7. RQ64
8. RQ81
9. RQ86 staged qualitative-outcome labelling after RQ81; hard validation can block
10. RQ13
11. RQ25
12. RQ26
13. Q69
14. RQ17
15. RQ38
16. RQ59/RQ73
17. RQ60/RQ67
18. RQ03/Q80
19. RQ04/RQ75
20. RQ05/Q81 matrix

Parallel-safe options if separate agents are careful:

- Q69 can run in a SQL-only branch while a higher-priority backend task runs, but it must not displace the BCI repair override.
- Frontend-only tasks may run only when path-safe and must not change backend contracts under RQ89/RQ90.
- RQ76/RQ88 are safe small UX tasks, but should not displace P0/P1 tasks.

## Prompt quality assessment

### Ready-quality prompts

These are clear enough for direct execution with minimal extra research when their routing/dependencies permit them:

- RQ89 (current READY)
- RQ90 (ready-quality, serialized after RQ89)
- RQ01
- RQ39
- RQ40
- RQ51/RQ52
- RQ57/RQ58 with prep doc phase-1 contract
- RQ64
- RQ72
- RQ74 with prep doc context-revenue contract
- RQ75
- RQ79
- RQ81
- RQ85 copy/label phase only
- Q69

They name concrete files/behavior, have a bounded scope and include a meaningful test matrix or a prepared default in `ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md`.

### Prompts that still need a contract decision before full runtime behavior change

- RQ05/Q81: runtime dataScope/store fixes wait for matrix output.
- RQ82/RQ83/RQ84: additive denominator metadata is prepared; breaking/renaming fields still needs compatibility decision.
- RQ86: qualitative labelling is prepared; hard validation still needs product decision.

Agents should produce a short contract note or mark `BLOCKED/PARTIAL` instead of guessing.

### Prompts that should be treated as replacements/refinements

- RQ78 refines older RQ06. Use RQ78 for implementation and cite RQ06 as older evidence if needed.
- RQ80 refines older RQ07. Use RQ80 for implementation.
- RQ71 is mostly covered by RQ64. Run separately only if RQ64 cannot safely change size-curve booleans.
- RQ72 is the Executive frontend companion to RQ01, not a separate business contract.
- RQ75 is the DataQualityPage surface companion to RQ04.

### Prompts that are too broad unless split during execution

- RQ64 touches four handlers. If the patch gets large, split by DTO contract first, then handler-by-handler.
- RQ05 can become too broad. It should produce a matrix first, not runtime fixes.
- RQ82/RQ83/RQ84 should be one denominator contract note first, then additive implementation.

## Token-saving instructions for agents

1. Do not read all queue files.
2. Start from this file and the target prompt only.
3. Read sibling prompts only when `Merge / split rule` says so.
4. Read `ANALYTICS_WAITING_PROMPTS_EXECUTION_PREP.md` only for prompts listed as prepared/gated here.
5. Prefer `git grep`/search for the exact function names from `Evidence already found`.
6. Do not open broad pages end-to-end unless the prompt touches that page.
7. Do not inspect SQL migrations for frontend-only tasks.
8. Do not inspect frontend pages for SQL-only Q69/Q70 unless the prompt says surface parity is in scope.
9. Do not re-audit old findings before coding unless evidence contradicts the prompt.
10. If evidence appears stale, verify the exact file/function and update the prompt note instead of expanding scope.
11. Final answer must list checks run; if none, say so.

## Execution checklist for every prompt

Before coding, fill the safety gate from `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

After coding, record:

```md
Prompt:
Files changed:
Runtime behavior changed: yes/no
Contract changed: yes/no
True-zero test:
Missing/unknown test:
UI/detail/export/action parity test:
Checks run:
Checks not run:
Remaining risk:
Next prompt:
```

## Final recommendation

The queues are strong enough for agent execution, but current routing is not the old generic lane order. Close the BCI04-derived assertion sequence first: `RQ89` -> `RQ90` -> full backend suite/GitHub Actions evidence. After that, return to the general lane order and owner-gated promotions.

### Current next runnable pointers (2026-08-13)

The 2026-08-10 `RQ89`/`RQ90`/`BCI05` pointers below this heading were historical and are obsolete. Use `MASTER_ROADMAP.md`.

- Backend CI: none READY (`BCI01`/`BCI05`/`BCI06` DONE).
- Analytics correctness: current RQ READY is none. `RQ96` DONE. `RQ106` Decision Pulse DONE. `RQ97`/`RQ98` WAITING. Strategy: `docs/ai/ANALYTICS_TEST_STRATEGY.md`.
- Do not revive `RQ100`/`RQ101`/`RQ102`/`RQ103`/`RQ104`/`RQ105`. `QDB06` is DONE. Current execution READY is none.
- Premium UI: none READY (`P-UI-22` DONE, queue complete).
- GenAI: dormant until core release gates are clear.
- Validators: `node scripts/check-prompt-queues.mjs` and `node scripts/check-planning-architecture.mjs`.
