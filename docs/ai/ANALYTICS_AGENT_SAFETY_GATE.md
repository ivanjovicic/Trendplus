# Analytics Agent Safety Gate

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`

## Purpose

This is a mandatory checklist for agents working on analytics, reporting, dashboards, tables, charts, exports, recommendations, action queues and outcome summaries.

The goal is to reduce repeated semantic bugs such as fake zero, fake green, wrong percent units, wrong denominators, filter drift and expected-impact misuse.

Read this with:

- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`

## Mandatory pre-code gate

Before writing runtime code, answer:

```md
Analytics safety gate:
- Surface:
- Source of truth:
- Contract changed? yes/no
- If yes, old contract:
- If yes, new contract:
- Unit:
- Numerator:
- Denominator:
- True zero case:
- Missing/unknown case:
- No-baseline case:
- Freshness/fallback case:
- DataScope/store/search filters:
- User-visible surfaces affected:
- Export/detail/action payload affected? yes/no
- Tests proving true-zero vs unknown:
- Tests proving table/detail/export/action parity:
- Stop condition hit? no / details
```

If the answer is unknown, do not implement a runtime fix. Add tests/docs, mark the queue item `BLOCKED`/`PARTIAL`, or create a follow-up prompt.

## Semantic contracts agents must not violate

### 1. Unknown is not zero

Do not convert missing evidence into:

- `0 RSD`
- `0 kom`
- `0%`
- empty array that looks like successful no-data
- false boolean that looks like healthy state

Use null, explicit status, or response meta.

### 2. Missing is not good

Do not map missing/unknown/stale/fallback data to:

- `good`
- `healthy`
- `fresh`
- `normal`
- `maintain`
- `measured`
- `info`

unless there is separate evidence that proves the state.

### 3. Revenue is not expected impact

Use strict vocabulary:

| Field | Meaning |
|---|---|
| `expectedImpactRsd` | actionable expected impact backed by decision contract |
| `potentialExposureRsd` | financial exposure / triage value, not yet action impact |
| `contextRevenueRsd` | revenue context only |
| `estimatedValueRsd` | estimated valuation with source/quality metadata |
| `measuredImpactRsd` | measured outcome value with evidence/date/method |

Never rank a recommendation as high expected impact by substituting revenue, lost sales, inventory value or missing cost fallback unless the backend contract explicitly says it is expected impact.

### 4. Ratio is not percent unit

Every percent/share/rate field must say whether it is:

- ratio, e.g. `0.35`
- percent unit, e.g. `35`
- formatted text, e.g. `35%`

Do not pass ratios into helpers expecting percent units. Do not export raw ratios when the UI shows percentages.

### 5. Returned count is not total count

A `LIMIT`, page size, top-N, or client-loaded page produces `returnedCount`, not total matching count.

Required labels:

- `Prikazano N`
- `Top N od M`
- `sortira trenutnu stranu`
- `isTruncated=true`

when applicable.

### 6. One surface is not enough

If a value appears in a table and can also appear in detail/export/action/report, all affected surfaces must be checked.

Required parity list:

- API DTO
- TypeScript type
- table cell
- chart/tooltip
- detail drawer
- CSV/XLSX/PDF/report
- action payload / outcome ledger

### 7. Frontend is not the recommendation engine

Frontend may map labels and style, but must not invent:

- recommendation status
- confidence score
- expected impact
- reason codes
- actionability
- data quality state

If frontend fallback exists for old data, it must be labelled non-authoritative and queued for removal.

### 8. Outcome status is not measurement evidence

For action outcomes:

- `pending` means still not measured.
- `not_measured` means measurement evidence is unavailable and must not get a fake measurement timestamp.
- `success`/`neutral`/`negative` need evidence, or must be labelled qualitative.
- `measuredImpactRsd` needs measured date/method/evidence or explicit warning.

### 9. Date-only filters are whole-day filters

Default rule:

- `fromDate`: inclusive day start
- `toDate`: exclusive next day start
- use half-open intervals: `>= from` and `< toExclusive`

Do not use `<= toDate at 00:00` for a date-only `toDate`.

### 10. Fail closed on ambiguity

If an agent cannot prove the contract, it must choose safety:

- show unavailable/insufficient
- add warning
- preserve null
- block recommendation
- mark prompt `BLOCKED`/`PARTIAL`

Do not silently choose optimistic output.

## Review checklist for PRs

A PR touching analytics should answer yes/no:

1. Does it preserve null/unknown separately from zero?
2. Does it preserve stale/fallback/partial visibly?
3. Does it specify ratio vs percent unit?
4. Does it specify denominator?
5. Does it avoid frontend-invented recommendations/confidence?
6. Does it avoid using revenue/inventory value/lost sales as expected impact without contract?
7. Does it keep table/detail/export/report/action payload consistent?
8. Does it test true zero and missing evidence separately?
9. Does it test no-baseline if trend or comparison changed?
10. Does it state checks not run if checks were skipped?

If any answer is no, the PR should not be marked DONE.
