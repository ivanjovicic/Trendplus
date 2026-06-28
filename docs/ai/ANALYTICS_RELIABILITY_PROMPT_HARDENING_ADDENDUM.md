# Analytics Reliability Prompt Hardening Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: planning/prompt-quality addendum only; no runtime code changed

## Purpose

This file hardens prompt entries that were marked as under-specified, insufficiently researched, or at risk of producing inconsistent contracts across queues.

Use this addendum together with:

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- the original queue file that contains the target RQ/Q entry

This addendum does not change statuses. It is a mandatory read-before-work supplement for the prompts listed below.

## General hardening rule

If an original prompt conflicts with this addendum, use the stricter rule from this addendum and record the conflict in the queue notes. If a contract remains ambiguous after reading this addendum, stop and mark the task `BLOCKED` or `PARTIAL`; do not invent business semantics silently.

---

# 1. Hardened shared contracts

## 1.1 Percent/share unit contract

Default contract unless explicitly overridden:

- Public UI percent/share fields should use percent units, e.g. `35` means `35%`.
- Raw ratio fields must be named with `Ratio`, e.g. `marginRatio = 0.35`.
- If an API already returns a raw ratio under an old `Pct` field, frontend/export must normalize at the boundary and document the compatibility behavior.
- Table, chart, detail snapshot, CSV, XLSX, PDF and print/report must show the same displayed unit.

Applies to: RQ39, RQ40, RQ42, RQ44, RQ45, RQ46, RQ55, RQ63.

## 1.2 Impact/value vocabulary contract

Default vocabulary:

- `expectedImpactRsd`: actionable impact backed by enough evidence and aligned with recommendation type.
- `potentialExposureRsd`: financial exposure that may be useful for triage but is not yet an actionable expected impact.
- `estimatedValueRsd`: valuation based on a documented estimate source.
- `unknownValue`: cost/value evidence is missing and must not be summed as zero without warning.
- `profitReliable=false`: profit exists only as estimate or cannot be computed because cost is missing.
- `costMissing=true`: cost evidence missing for the row/calculation.

Forbidden:

- signal-review/insufficient-data actions carrying confirmed `expectedImpactRsd`
- missing cost becoming reorder cost `0` and inflating profit
- missing inventory cost/value becoming silent capital value `0`

Applies to: RQ01, RQ17, RQ27, RQ36, RQ38, RQ49, RQ56, RQ59, RQ60.

## 1.3 Date range contract

Default contract for UI date-only filters:

- `fromDate`: inclusive start at selected date start.
- `toDate`: exclusive end at the day after selected date.
- Use half-open intervals: `>= fromDate.Date` and `< toDate.Date.AddDays(1)`.
- Previous period must be non-overlapping and equal in duration unless the endpoint explicitly documents a different comparison rule.
- A sale/row must never be counted in both current and previous windows.

Applies to: RQ13, RQ25, RQ26, Q70 when date windows are part of test evidence.

## 1.4 Lost-sales source-status contract

Default contract:

- `sourceStatus=view`: value came from trusted view/query evidence.
- `sourceStatus=fallback`: value came from fallback calculation and must carry reduced confidence.
- `sourceStatus=unavailable`: evidence could not be loaded; value should be null or explicitly not actionable.
- `sourceStatus=true_zero`: evidence loaded successfully and proved zero.

Forbidden:

- unavailable lost-sales evidence returning `good`
- unavailable/fallback-zero becoming true zero
- Decision Board/PDC using unknown lost-sales evidence as expected impact

Applies to: RQ03 and Q80. Do not implement RQ03 and Q80 independently with different status names.

## 1.5 dataScope/store lineage contract

Default contract:

- A page must declare the active `dataScope` and store filter used by its list/table view.
- Detail links, print/report/export payloads and action metadata must carry the same scope.
- Current and previous comparison requests must use identical dataScope/store lineage.
- If a page intentionally uses `all`, it must label this explicitly.

Applies to: RQ05, Q81, RQ53, RQ54, RQ55.

---

# 2. Hardened prompt supplements

## RQ39 supplement - Derived category `revShare` percent contract

Original queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`

### Evidence already found

- Derived category intelligence calculates `revShare` as ratio-like value, e.g. `approxRevenue / totalRevenue`.
- Legacy category intelligence/UI expects percent units for `revShare` and formats with `fmtPct`.
- `mergeCategorySignalsAsPrimary` can make derived data primary.

Risk class: confirmed/likely high-impact display/export bug.

### Fixed/default contract

- `CategoryStat.revShare` remains percent units for compatibility: `25` means `25%`.
- Any ratio value must be named `revShareRatio` or converted before it enters `CategoryStat`.
- `fmtPct` must receive the same unit on all paths.

### Required test matrix

- normal: two categories, 25% and 75%, derived source active.
- legacy parity: same fixture through legacy shape displays identical shares.
- export/detail: exported/detail value for 25% is not `0.25%` and not raw `0.25`.
- chart: chart tooltip/axis uses same 25% value.
- no-data: total revenue zero returns explicit no-data/insufficient or 0 with denominator note.

### Stop condition

If existing consumers require ratio units, stop and create a migration prompt. Do not silently switch some surfaces to ratio and others to percent.

---

## RQ40 supplement - Supplier Decision percent export/detail parity

Original queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`

### Evidence already found

- Supplier Decision visual table multiplies some raw ratio fields for display.
- Export/detail payloads can use raw row values while the column is marked `percent`.

Risk class: confirmed/likely high-impact export mismatch.

### Fixed/default contract

- Table display, detail snapshot, export payload and generated document must all represent the same displayed percent.
- For `preMarkdownMarginPct = 0.35`, user-facing output must be `35%` or numeric `35` with percent formatting, never raw `0.35` without label.

### Required test matrix

- fixture: `preMarkdownMarginPct = 0.35`.
- visual: table cell renders 35%.
- detail: snapshot stores display value 35% or raw+unit metadata that renders 35%.
- export request: exported row uses normalized value or explicit unit metadata.
- generated doc: CSV/XLSX/PDF/print output is not `0.35` unless the column explicitly says ratio.

### Stop condition

If document renderer cannot format percent metadata yet, mark RQ40 `PARTIAL` and create/link RQ41/RQ42 follow-up. Do not call it DONE with only table display fixed.

---

## RQ03/Q80 supplement - Lost-sales zero vs unavailable unified contract

Original queues:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`

### Evidence already found

- Lost-sales validation can classify `lostSalesEstimate <= 0` as good.
- Lower-level fallback can return zero when evidence is unavailable.
- SQL queue has a related Q80 about source/confidence.

Risk class: confirmed/likely fake-zero/fake-green bug.

### Fixed/default contract

Use a shared source-status model:

- `view`: trusted value loaded.
- `fallback`: fallback value loaded; reduced confidence.
- `unavailable`: evidence missing; not good, not true zero.
- `true_zero`: trusted evidence loaded and value is zero.

### Required test matrix

- trusted view positive lost sales.
- trusted view true zero lost sales.
- fallback positive lost sales.
- fallback zero but evidence incomplete.
- unavailable view/connection returns no value.
- Decision Board/card validation must not show unavailable as good.

### Stop condition

If Q80 is executed first, RQ03 must reuse Q80 names/fields. If RQ03 is executed first, Q80 must become SQL-specific evidence/follow-up or be marked partially obsolete. Do not introduce two separate status vocabularies.

---

## RQ05/Q81/RQ53/RQ54 supplement - dataScope/store lineage contract

Original queues:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`

### Evidence already found

- Some analytics surfaces apply dataScope by article origin.
- Some apply it by sale header origin.
- Color/ShoeType services support `dataScope`, but list pages can omit it while detail URLs include it.
- Vendor pre/post API supports `storeId` and `dataScope`, but the page does not expose/pass them.

Risk class: likely cross-surface lineage bug.

### Fixed/default contract

Before changing many endpoints, produce a dataScope matrix with these columns:

| Surface | Endpoint/service | Filter source | Sale header origin | Article origin | Store filter | Detail/export/action carries same scope? | Current behavior | Target behavior |
|---|---|---|---|---|---|---|---|---|

Default target:

- List/table, detail, export and action metadata use identical dataScope/store filters.
- If dataScope is intentionally ignored or forced to `all`, UI must say so.
- Current and previous comparison requests use identical filters.

### Required test matrix

- `dataScope=all` returns current behavior.
- `dataScope=imported` changes list request query and detail URL consistently.
- `dataScope=existing` changes list request query and detail URL consistently.
- store filter present in both current and previous requests for vendor pre/post.
- export metadata includes dataScope/store.

### Stop condition

Do not patch RQ53/RQ54 randomly if RQ05/Q81 reveals different canonical rules. If canonical dataScope semantics are not decided, stop with matrix and proposed follow-up prompts.

---

## RQ13/RQ25/RQ26 supplement - shared date boundary contract

Original queues:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md`

### Evidence already found

- Advanced/V2 and legacy Advanced endpoints can parse `toDate` as exact UTC instant and use `<= toDate`.
- Legacy KPI current and previous windows can overlap at `from` boundary.

Risk class: likely/confirmed date-window correctness bug.

### Fixed/default contract

- UI date-only filter means whole selected dates.
- Query window is half-open: `>= from.Date` and `< to.Date.AddDays(1)`.
- Previous period is immediately before current period and non-overlapping.
- Equal duration must be verified in ticks/days.

### Required test matrix

- sale at `toDate 00:00` included.
- sale at `toDate 12:00` included.
- sale at next day `00:00` excluded.
- sale exactly at `from` counted only in current, not previous.
- previous-period fixture has equal duration and no boundary overlap.

### Stop condition

Do not introduce separate helper behavior for V2 and legacy unless documented. If a shared helper expands scope too much, fix one surface locally and create a follow-up for shared helper consolidation.

---

## RQ17/RQ38/RQ59/RQ60 supplement - cost, profit and impact reliability

Original queues:

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`

### Evidence already found

- Smart reorder can calculate expected profit using zero reorder cost when cost is missing.
- Derived smart reorder can repeat this issue in frontend-derived data.
- Inventory signal review actions can carry expected impact even when recommendation is not actionable.
- Inventory row value can become zero when cost and estimated value are missing.

Risk class: confirmed/likely fake-profit/fake-impact/fake-zero valuation bug.

### Fixed/default contract

Use these fields/meanings consistently:

- `expectedImpactRsd`: actionable, recommendation-aligned impact with enough evidence.
- `potentialExposureRsd`: triage exposure; not guaranteed action impact.
- `profitReliable`: false when cost evidence missing/estimated beyond allowed threshold.
- `costMissing`: true when cost is not reliable for the row.
- `valuationStatus`: `known`, `estimated`, `missing`.

### Required test matrix

- normal known cost: profit and expected impact valid.
- missing cost: profit null/unreliable, not ranked as high profit.
- estimated cost: shown as estimated, lower confidence.
- signal review: no confirmed expected impact; exposure may be shown separately.
- inventory quantity > 0 with missing cost: value is unknown/missing, not zero.
- export/action payload preserves status fields.

### Stop condition

If backend and frontend need a shared DTO field name, do not invent different frontend-only names. Add a small contract doc or mark dependent prompt blocked until field names are chosen.

---

## RQ51/RQ52 supplement - Color recommendation authority

Original queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`

### Evidence already found

- Color backend `insufficient_data` can map to local `Zadrzi`.
- When backend recommendation is missing, frontend can compute a local recommendation.

Risk class: confirmed/likely fake recommendation bug.

### Fixed/default contract

- Backend `insufficient_data` maps to distinct UI status `Nedovoljno podataka`.
- Missing backend recommendation means informational row, not authoritative `Pojacaj/Zadrzi/Smanji`.
- If a local heuristic is temporarily retained, it must be labelled `heuristic/non-authoritative` and excluded from final recommendation counts/actions/export status.

### Required test matrix

- backend `increase_focus` -> `Pojacaj`.
- backend `maintain` -> `Zadrzi`.
- backend `do_not_trust` -> `Smanji` or distinct risk label depending current mapping.
- backend `insufficient_data` -> `Nedovoljno podataka`, not `Zadrzi`.
- missing recommendation -> informational/non-authoritative.
- counts/export/detail preserve status authority.

### Stop condition

If current UI enum cannot represent insufficient data, extend the enum before fixing labels. Do not solve by string-only display while sort/count/export still treat it as `Zadrzi`.

---

## RQ57/RQ58 supplement - Inventory risk sort and screen CSV parity

Original queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`

### Evidence already found

- `oosRisk`/`overstockRisk` server sort falls back to `kolicina`.
- UI sorts loaded page by forecast risk after paging.
- `CSV ekran` exports `rows`, not necessarily `displayedRows`.

Risk class: confirmed/likely table/export semantics bug.

### Fixed/default contract

Choose one and document it:

Option A preferred:

- OOS/overstock risk sort is global server/API sort across the filtered dataset.
- Pagination returns globally risk-sorted rows.
- Server export uses the same sort.

Option B acceptable only as interim:

- UI label says `sortira trenutnu stranu`.
- Screen CSV exports exactly `displayedRows` in the same order.
- Filtered server export labels that it uses server sort, not page-local risk sort.

### Required test matrix

- high-risk SKU that would be on page 2 under quantity sort appears on page 1 if Option A.
- if Option B, UI has visible page-local label.
- screen CSV order equals displayed table order.
- filtered CSV/XLSX/PDF sort semantics are explicit in metadata.

### Stop condition

Do not mark RQ57 DONE by only changing local sort. That is current behavior. Either implement global sort or make page-local semantics explicit and create follow-up for global sort.

---

## RQ41/RQ42/RQ46 supplement - Export/detail trust preservation

Original queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`

### Evidence already found

- XLSX renderer writes inline strings.
- Detail snapshot stringifies raw values.
- Export payload is built from configured columns only, so hidden trust fields can be dropped.

Risk class: likely report/export trust gap.

### Fixed/default contract

- Detail snapshot must have `rawValue`, `displayValue`, `unit`, and `dataType` or must reuse the same formatter as table display.
- XLSX numeric/currency/percent/date cells must be typed when renderer supports it.
- Export must include reliability metadata either as visible columns or export-only columns.

### Required test matrix

- currency value table/detail/export parity.
- percent ratio/percent-unit value table/detail/export parity.
- date value table/detail/export parity.
- trust metadata field included in export even if hidden in table.
- Excel numeric cells behave as numbers.

### Stop condition

Do not type XLSX cells before percent/value units are normalized for the affected tables. Otherwise the renderer can faithfully encode the wrong number as a number.

---

# 3. Prompts that should remain lower priority unless owner explicitly promotes them

These prompts are valid but should not block P0/P1 numeric correctness:

- RQ43 stale browser preview: important trust UX, after percent/detail/export contracts.
- RQ44 zero/no-baseline badge: best after backend baseline status exists.
- RQ47/RQ48 action source key/duplicate guard: important after expected impact semantics are fixed.
- RQ50 top-N chart semantics: useful, but after numeric correctness.
- RQ55/RQ56/RQ63 denominator/naming cleanup: useful but lower urgency than wrong recommendations and wrong numeric values.

# 4. Final execution guard

Before starting any hardened prompt, the agent must write in local notes:

```md
Hardened addendum checked: yes
Contract used:
Unit/denominator used:
Surfaces verified:
Fake-confidence cases tested:
Stop conditions encountered: none / details
```

If those fields cannot be filled, the prompt is not ready for implementation.
