# Analytics Reliability Prompt Priority Review

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: planning/review only; no runtime code changed

## Purpose

This document consolidates the most important analytics reliability prompts across the existing queues and reviews whether the prompts are precise enough for Codex/Cursor/manual execution.

Reviewed queues:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`

This review does not change queue statuses. The main queue still exposes `RQ01` as the active reliability prompt, and the SQL queue still exposes `Q69` as the SQL audit prompt. If one global sequential execution path is desired, use the order below.

## Scoring rules used for priority

Prompts were ranked by:

1. Business-decision risk: can it create a wrong action, wrong recommendation or wrong financial impact?
2. Display/export risk: can it show a materially wrong number in tables, charts, detail or export?
3. Fake confidence risk: can missing evidence look like true zero, good, maintain, fresh or measured?
4. Blast radius: one endpoint vs many pages/reports/actions.
5. Likelihood: based on code evidence already found, not theoretical possibility.
6. Implementation dependency: can the prompt be done safely without needing a broader product decision?
7. Testability: can it be locked with small fixtures and regression tests?

## Global recommended execution order

### Phase 0 - stop the most dangerous active Decision Board issue

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 1 | RQ01 | Prevents Decision Board from attaching lost-sales impact to product rows where PDC intentionally left expected impact null. This can directly create wrong action/impact ranking. | Strong. It has scope, tests, acceptance and explicit do-not-touch list. | Add exact before/after assertion names in implementation notes. Prefer no fallback at board level unless PDC supplies impact. Record tests run or explicitly mark not run. |

### Phase 1 - high-impact UI/export errors that can show wrong numbers

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 2 | RQ39 | Derived category `revShare` ratio vs percent-unit mismatch can create a 100x display/export error. | Strong, but it still says “decide canonical contract”. | Make the prompt choose a default contract before coding: legacy `CategoryStat.revShare` is percent units unless a breaking migration is explicitly approved. Add table/chart/detail/export fixture for 25%. |
| 3 | RQ40 | Supplier Decision percent display and export/detail can disagree between raw ratio and percent units. | Strong. Has good fixture value `0.35`. | Add explicit test target: UI display, detail snapshot payload, export request payload and generated document value must all agree. |
| 4 | RQ51 | Color backend `insufficient_data` can appear as `Zadrzi`, turning lack of evidence into a valid recommendation. | Strong and small. | Add local enum/status changes to scope; verify counts, sort order, badge tone and export/detail. |
| 5 | RQ57 | Inventory OOS/overstock risk sorting is page-local, not global. User can miss highest-risk SKUs. | Good but needs contract decision. | Prompt must require either true server-side global risk sort or an explicit visible label “sortira trenutnu stranu”. Do not silently keep current behavior. |
| 6 | RQ58 | `CSV ekran` can export a different order than the risk-sorted table. | Good and small. | Make it dependent on RQ57’s final sort contract; use `displayedRows` for screen export if sort remains client-side. |

### Phase 2 - date/period correctness before trend interpretation

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 7 | RQ13 | Advanced/V2 date-only `toDate` can exclude the selected day. | Strong. Has half-open range rule. | Add shared date helper only if it does not expand scope; otherwise duplicate minimal normalization and create follow-up. |
| 8 | RQ25 | Legacy Advanced has the same date boundary class as RQ13. | Strong but duplicates RQ13 concept. | Run after RQ13 or explicitly create a shared date-boundary helper task. Do not fix V2 and legacy in one prompt unless a new combined prompt is created. |
| 9 | RQ26 | KPI current/previous period can overlap at the boundary. | Strong. Very testable. | Add exact fixture with sale at `from` and sale at previous end. Lock period length semantics. |
| 10 | Q69 | SQL trust audit/tests before SQL runtime fixes. | Strong for SQL; it is docs/tests only and safe. | Keep as a separate SQL lane. If one agent is working globally, run after RQ01 or in parallel only with strict non-overlap. |
| 11 | Q70 | Nivelacija zero-baseline percent semantics in SQL views. | Good but depends on Q69 evidence. | Do not start before Q69 notes define the zero-baseline contract. |

### Phase 3 - fake-zero/fake-profit and missing-cost trust

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 12 | RQ17 | Backend V2 smart reorder can inflate expected profit when cost is missing. | Strong. Clear tests and acceptance. | Add ranking assertion: missing-cost rows cannot sort/rank as high-profit solely due missing cost. |
| 13 | RQ38 | Frontend derived smart reorder can reintroduce the same missing-cost profit bug. | Good but should depend on RQ17 contract. | After RQ17, reuse the same terms: `profitReliable=false`, nullable profit, or source warning. |
| 14 | RQ59 | Inventory `SIGNAL_REVIEW` can still carry expected impact. | Good. Similar trust class to RQ01. | Rename/mark value as potential exposure, not expected impact, unless recommendation is actionable and evidence is sufficient. |
| 15 | RQ60 | Inventory missing cost/value can become fake zero. | Strong. | Require row-level missing-value metadata and ensure supplier value charts/CSV do not sum unknown as zero without warning. |
| 16 | RQ03 | Lost sales unavailable can look like true zero/good. | Strong. | Coordinate with Q80 to avoid duplicate fixes. Choose one canonical source-status contract. |
| 17 | Q80 | SQL-side lost-sales source/confidence explicitness. | Likely overlaps RQ03. | Treat as SQL evidence/foundation for RQ03 or mark one follow-up obsolete after contract is chosen. |
| 18 | RQ04 | Data Quality no-revenue can look green. | Strong and central. | Add Decision Board regression: no evidence must not show clean health. |
| 19 | RQ27 | Hard-coded 35% margin fallback can create fake benchmark. | Strong. | Include supplier and category fixtures with zero cost coverage. |
| 20 | RQ36 | Frontend derived margin defaults missing margin to 0%. | Good. | Align with backend margin contract from RQ27/RQ17. |

### Phase 4 - scope, lineage and consistency across pages

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 21 | RQ05 | Canonical dataScope consistency audit across modules. | Good but audit-shaped. | Must produce a concrete matrix and create follow-up fixes; do not patch random endpoints in this task. |
| 22 | Q81 | SQL-level dataScope/store/supplier filtering consistency. | Good but overlaps RQ05. | Treat Q81 as SQL-specific input to RQ05 or run after RQ05 defines global semantics. |
| 23 | RQ53 | Color/ShoeType list/detail can disagree on dataScope. | Strong and concrete. | If RQ05 is not done, at least preserve current global dataScope consistently; document assumed contract. |
| 24 | RQ54 | Vendor pre/post supports dataScope/store but page omits them. | Strong. | Add both current and previous request tests; previous and current must use identical filter lineage. |
| 25 | RQ61 | Inventory freshness can use secondary panel timestamps. | Good. | Show panel-specific freshness if timestamps differ materially. |
| 26 | RQ62 | Vendor previous-period request failure can look like genuine no-baseline. | Strong enough. | Add explicit `previousComparisonError` state and no “Nova baza” label on transport failure. |

### Phase 5 - report/export/data trust polish after numeric contracts are fixed

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 27 | RQ41 | XLSX cells are strings, weakening Excel analysis. | Good. | Do after RQ40 so percent raw values are already normalized before typing cells. |
| 28 | RQ42 | Detail snapshot stringifies raw values and can disagree with table. | Good. | Must reuse the same percent-unit contract from RQ39/RQ40. |
| 29 | RQ45 | KPI margin card hides margin coverage. | Good. | Add a visible trust badge, not only hidden tooltip. |
| 30 | RQ46 | Exports can drop trust metadata. | Good. | Add visible or export-only trust columns. Do not overload business columns. |
| 31 | RQ43 | Browser report preview can look current when backend report fails. | Good. | Add watermark/savedAt and disable or confirm export/print in preview mode. |
| 32 | RQ47 | Supplier action source key omits some filters. | Good. | Decide identity-defining vs display-only filters before adding all filters blindly. |
| 33 | RQ48 | Duplicate guard checks only first 200 actions per status. | Good but lower risk if backend upsert is safe. | Verify backend idempotency before expanding UI pagination. |

### Phase 6 - lower-priority semantics and naming cleanup

| Global rank | Prompt | Why now | Quality review | Required hardening |
|---|---|---|---|---|
| 34 | RQ14 | Heatmap transaction can mean lines, not receipts. | Good. | Must choose label vs formula; do not change both without a test. |
| 35 | RQ15 | Basket affinity denominator can include baskets not eligible for pairs. | Good. | Add `pairEligibleTransactions` if keeping old denominator for compatibility. |
| 36 | RQ16/RQ20/RQ44 | No-baseline and zero change display issues. | Good but distributed. | Consider one shared baseline-status display helper after backend contracts are fixed. |
| 37 | RQ28/RQ31/RQ23 | No-data empty meta in ABC/daily/supplier-score V2. | Good. | Standardize with RQ24 meta contract. |
| 38 | RQ29/RQ30/RQ32/RQ33/RQ37/RQ49 | Legacy Advanced aging/statistical/value-label issues. | Valid but less urgent than current operational Decision Board/export/sort bugs. | Fix only after date/percent/cost contracts are stable. |
| 39 | RQ55/RQ56/RQ63 | Denominator/cost fallback/naming clarity. | Valid but lower risk. | Good candidates for small cleanup PRs once P0s are done. |
| 40 | RQ50 | Top-N chart semantics. | Useful UX trust improvement. | Do after numeric correctness; cosmetic labels should not block formula fixes. |

## Prompt quality assessment

### Strong prompts

These are ready-quality prompts: RQ01, RQ03, RQ04, RQ13, RQ17, RQ25, RQ26, RQ39, RQ40, RQ51, RQ57, RQ58, RQ60, Q69, Q70.

Common strengths:

- They name the exact failure mode.
- They have narrow scope and do-not-touch lists.
- They include fixtures/tests.
- They define acceptance in observable terms.

### Prompts that need contract tightening before execution

- RQ39: choose canonical percent units before coding.
- RQ57: choose global server sort vs explicit page-local sort before coding.
- RQ05/Q81/RQ53/RQ54: avoid fragmented dataScope fixes; create/obey one canonical dataScope matrix.
- RQ03/Q80: avoid duplicate lost-sales source-status contracts.
- RQ13/RQ25: avoid two incompatible date helper implementations.
- RQ17/RQ38/RQ59/RQ60: use one shared vocabulary for `expectedImpact`, `potentialExposure`, `profitReliable`, `costMissing`.

### Prompts that are valuable but should stay later

- RQ41/RQ42 are important for exports, but only after percent/value contracts are fixed.
- RQ43/RQ50 improve interpretation and trust, but do not fix the underlying numbers.
- RQ55/RQ63 are mostly semantic clarity, not first-order wrong values.

## Recommended next READY sequence

If a single global queue is desired, the safest sequence is:

1. RQ01
2. RQ39
3. RQ40
4. RQ51
5. RQ57
6. RQ58
7. Q69
8. RQ13
9. RQ25
10. RQ26
11. RQ17
12. RQ38
13. RQ59
14. RQ60
15. RQ03 or Q80, but not both independently
16. RQ04
17. RQ27
18. RQ05/Q81 audit before concrete dataScope fixes
19. RQ53
20. RQ54

Reason: this sequence first removes wrong impact/recommendation displays, then fixes obvious UI/export mismatches, then date/period correctness, then fake cost/zero semantics, then cross-surface lineage.

## Prompt writing improvement checklist

Every future analytics reliability prompt should include these extra sections, in addition to the existing protocol template:

### Evidence already found

- Files and functions already inspected.
- Exact observed behavior.
- Whether the issue is confirmed, likely, suspicious or only a contract gap.

### Contract decision required

State whether the prompt is allowed to decide the product contract. If yes, list the default contract. If no, the task should add tests/docs and stop as `BLOCKED` or `PARTIAL`.

### Unit and denominator contract

For any percent/share/rate/average:

- ratio or percent units?
- numerator?
- denominator?
- visible rows, all analyzed rows, filtered rows, or pair-eligible rows?
- true zero vs missing baseline?

### Evidence status and fake-confidence guard

For any zero/good/maintain/healthy/fresh/measured output:

- Can this state occur because evidence is missing?
- If evidence is missing, what explicit status/metadata is returned?
- Does UI/export preserve that metadata?

### Surface parity

The prompt must say which surfaces must agree:

- API response
- frontend table
- chart/tooltip
- detail drawer
- CSV/XLSX/PDF/export payload
- action queue payload

### Test matrix

At minimum, include:

- normal positive case
- true zero case
- missing evidence case
- no-baseline case
- low-coverage/fallback case
- export/detail case when the issue affects UI

### Stop conditions

The agent must stop and mark `BLOCKED`/`PARTIAL` if:

- a required contract is ambiguous
- tests require a real DB and no fixture path exists
- the fix touches files outside scope
- two prompt families need to be merged
- the task would change business semantics without before/after documentation

## Final recommendation

Do not start randomly from each addendum's local P0. Treat P0 across all queues as candidates, then apply dependency and blast-radius ordering. The best next move remains RQ01. After RQ01, fix the high-risk UI/export correctness prompts RQ39/RQ40/RQ51/RQ57/RQ58 before lower-priority chart polish.
