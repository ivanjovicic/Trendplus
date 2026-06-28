# Analytics Waiting Prompts Execution Prep

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: planning/prep only; no runtime code changed

## Purpose

This document prepares the analytics prompts that were previously flagged as not fully ready because they needed a contract decision, merge/split decision, or narrower execution plan.

Use this with:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- the specific queue prompt being executed

Goal: make agents faster and cheaper by turning ambiguous prompts into small, safe execution steps.

## Global rule for these prompts

When a prompt below says `Prepared default`, an agent may use that default without asking for another product decision.

When it says `Still contract-gated`, the agent must not implement runtime behavior yet. It should add/adjust tests/docs or mark the task `BLOCKED`/`PARTIAL` with the missing decision.

---

## RQ57/RQ58 - Inventory risk sort and screen CSV

### Current evidence

Relevant code:

- `InventoryPage.tsx` maps risk sorts to server `kolicina` sort via `serverSortBy = sortBy === "oosRisk" || sortBy === "overstockRisk" ? "kolicina" : sortBy`.
- Inventory list request uses `serverSortBy`.
- `displayedRows` applies OOS/overstock risk sorting only to the currently loaded `rows`.
- `exportVisibleCsv` exports `rows`, not `displayedRows`.

### Prepared default

Use a two-step contract to keep the first implementation small and safe:

1. **RQ57 phase 1, ready:** keep risk sort page-local, but make it explicit in UI and export metadata.
   - Label: `Sortira trenutno učitanu stranu`.
   - Keep server list sort as `kolicina` for now.
   - Add a warning/help text near risk sort when `sortBy` is `oosRisk` or `overstockRisk`.
   - Do not claim this is global risk ranking.
2. **RQ58, ready:** change screen CSV to export `displayedRows`, not `rows`.
3. Create a later separate prompt only if true global risk sort is required.

### Why not global sort now

True global risk sort needs a backend contract that can rank inventory rows by forecast risk across all filtered rows before pagination. Current frontend risk data comes from a separate forecast snapshot and is merged after list pagination. Implementing global sort in the same task would likely touch forecast/list contracts and increase token/runtime risk.

### Minimal execution scope

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- frontend tests if available

### Test matrix

- sort `kolicina`: table and CSV use server order.
- sort `oosRisk`: table order is `displayedRows`; CSV matches table order.
- sort `overstockRisk`: table order is `displayedRows`; CSV matches table order.
- UI shows page-local sort label for risk sort.

### Stop condition

Stop if product owner requires true global risk ordering in this prompt. Create a new backend/API prompt instead.

---

## RQ74 - Executive supplier revenue ranking vs expected impact

### Current evidence

Relevant code:

- `buildSupplierCards` sets `impact = item.revenue > 0 ? item.revenue : null`.
- It sets `expectedImpactRsd: null`.
- It uses `impact` in `computePriorityScore(...)` and `impactScore: impact ?? 0`.

### Prepared default

Supplier revenue is **context**, not expected impact.

Default contract:

- Do not put supplier revenue into `expectedImpactRsd`.
- Do not call a revenue-weighted supplier card a high expected-impact card.
- If revenue remains in priority scoring, expose it as context and label the ranking as `revenue-weighted priority`, not expected impact.
- Safer first patch: remove revenue from `impactScore`/impact wording and show revenue only in summary/context copy.

### Ready implementation option

Make RQ74 a small frontend contract fix:

1. Keep `expectedImpactRsd: null`.
2. Rename/label revenue in summary as context, e.g. `Prihod kao kontekst`.
3. Set `impactScore` to `0` or introduce a clearly named `contextRevenueScore` only if BoardCard type already supports it.
4. Update priority copy so it does not imply expected impact.

### Still contract-gated part

If the team wants supplier cards ranked by revenue, the contract must say whether this is:

- context ranking,
- risk exposure,
- or expected business impact.

Do not invent a backend expected-impact formula in the frontend.

### Test matrix

- high-revenue supplier with no expected impact does not display expected impact.
- card summary labels revenue as context if shown.
- priority/impact fields do not imply actionable expected impact.

---

## RQ82/RQ83/RQ84 - Action outcome denominator contract

### Current evidence

Relevant code:

- `BuildSummaryAggregate` counts `measuredItems` as all actions where normalized outcome status is not `pending`.
- This can include open actions.
- Existing tests intentionally include an open action with a success outcome in totals.
- `outcomeCoverageRate` uses closed actions as denominator.
- `positiveOutcomeRate` and `negativeOutcomeRate` use all non-pending outcomes as denominator.
- money impact and realization ratio use only rows with `MeasuredImpactRsd`.

### Prepared default

Use a **lifecycle-aware additive contract** instead of breaking existing fields immediately.

Default contract:

- Existing fields may stay for compatibility in first patch.
- Add explicit naming/metadata so the UI can distinguish:
  - created action count
  - closed action count
  - outcome-recorded count
  - closed outcome-recorded count
  - measured-impact sample count
- Do not call all non-pending outcomes `measured` unless evidence exists.
- UI must label realization ratio as based on `measuredImpactSampleCount`, not all outcomes.

### Recommended execution split

1. **RQ82/RQ83 prep task, ready as docs/tests:** add/adjust tests that document current denominator mismatch and desired additive fields. Do not rename public DTO fields yet.
2. **RQ84 implementation task:** add visible sample-size labels/warnings around measured money KPIs, using existing `measuredImpactSampleCount` if available.
3. Later compatibility task: introduce renamed DTO fields if needed.

### Stop condition

Stop before runtime behavior change if the implementation would remove open outcomes from existing `measuredCount` without compatibility note and regression tests.

### Test matrix

- open success outcome.
- closed success outcome.
- closed pending action.
- non-pending outcome with no measured impact.
- measured impact sample smaller than outcome-recorded count.

---

## RQ85 - Outcome summary default period window

### Current evidence

Relevant code:

- Outcome summary endpoint defaults to created window when no date filters are supplied: `createdFrom = DateTime.UtcNow.AddDays(-90)` and `createdTo = DateTime.UtcNow`.
- UI copy says the summary tracks source, priority and data quality, but does not solve the product expectation that an outcome summary might mean resolved/measured outcomes.

### Prepared default

Keep current default for now, but relabel it as a **created action cohort**.

Ready first patch:

- UI text should say: `Akcije kreirane u poslednjih 90 dana`.
- Do not imply `ishodi izmereni u poslednjih 90 dana`.
- Add help text explaining that resolved/measured windows require explicit filters.

Still contract-gated:

- Changing default to resolved or measured window requires product decision and should be a separate prompt.

### Test matrix

- no date filters -> created cohort label.
- createdFrom/createdTo supplied -> created period mode label.
- measuredFrom/measuredTo supplied -> measured period mode label.
- resolvedFrom/resolvedTo supplied -> resolved period mode label.

---

## RQ86 - Evidence requirements for authoritative outcomes

### Current evidence

Relevant code:

- Frontend allows success/neutral/negative outcome with empty measured amount/date.
- Backend accepts non-pending outcome with `MeasuredImpactRsd = null` and auto-fills measurement date unless pending.
- RQ81 should first stop `not_measured` from getting a fake measured date.

### Prepared default

Use a safe staged contract:

1. **After RQ81**, non-measured statuses must not get fake measured date.
2. Keep qualitative success/neutral/negative allowed only if visibly labelled as qualitative/unmeasured.
3. `measuredImpactRsd` must require measured date or evidence source if it is entered.
4. Summary must not count qualitative outcomes as measured-impact evidence.

### Ready first patch

After RQ81:

- Add UI copy: `Bez iznosa/dokaza, ishod je kvalitativan`.
- Add backend warning metadata or audit note segment when outcome status is non-pending but impact/evidence is missing.
- Do not block save yet unless product decision says evidence is mandatory.

### Still contract-gated

Blocking success/neutral/negative without evidence is a product workflow decision. Do not enforce hard validation until approved.

---

## RQ05/Q81 - dataScope/store/supplier semantics

### Current evidence

Across existing audits, dataScope is applied inconsistently:

- some list calls omit dataScope while detail links include it,
- some SQL helpers filter article origin but not sale-header origin,
- some pages support store/dataScope in service but do not pass it from the page.

### Prepared default

Do not patch endpoints randomly. First deliver a **dataScope matrix**.

Required matrix columns:

| Surface | Endpoint/query | Entity scope | Sales scope | Store scope | Supplier scope | Current behavior | Desired behavior | Risk | Follow-up prompt |
|---|---|---|---|---|---|---|---|---|---|

Definitions:

- `Entity scope`: article/product/master data origin.
- `Sales scope`: sales header/source origin.
- `Store scope`: whether selected store is applied to all relevant queries.
- `Supplier scope`: whether selected supplier is applied to all relevant queries.

Default rule until matrix says otherwise:

- If a list/detail/export/action surface is presented as the same view, it must use the same `dataScope`, `storeId`, `supplierId`, date range and search scope.
- If a query intentionally mixes entity scope and wider sales scope, it must expose metadata saying so.

### Ready first task

Make RQ05 a docs/tests-only matrix task. It should not change runtime endpoints.

### Follow-up split

After matrix:

- RQ53/RQ54 for frontend scope lineage.
- RQ78 for Data Quality top-offender revenue impact scope.
- Q81 for SQL helper audit.

---

## RQ57/RQ65/RQ77 count vocabulary

### Prepared shared vocabulary

Use these field names in future prompts:

- `returnedCount`: rows returned to this response/page/top-N.
- `totalMatchingCount`: total rows matching filters before limit/page.
- `limit`: requested cap.
- `isTruncated`: true when more matching rows may exist than returned.
- `visibleCount`: rows currently visible after client-side sort/filter.

Do not use `totalCount` unless the endpoint truly counts all matching rows before pagination/limit.

---

## Final readiness result

After this prep:

- RQ57/RQ58 are ready as small frontend page-local sort/CSV parity tasks.
- RQ74 is ready as a frontend contract/copy/ranking cleanup if revenue is treated as context.
- RQ81 is ready as a small fake-measured fix.
- RQ82/RQ83/RQ84 are ready as a denominator contract/tests task, but runtime DTO changes should be additive.
- RQ85 is ready as a copy/label clarification task; changing default period remains contract-gated.
- RQ86 is ready only after RQ81 for qualitative-outcome labelling; hard validation remains contract-gated.
- RQ05/Q81 are ready as a matrix/audit task, not runtime fixes.
