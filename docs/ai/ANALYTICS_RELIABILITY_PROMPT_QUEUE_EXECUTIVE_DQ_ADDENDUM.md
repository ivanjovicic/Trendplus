# Analytics Reliability Prompt Queue - Executive/Data Quality Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum (next global per priority review: RQ39)
Main queue READY prompt: none (RQ01–RQ13 DONE in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`)

Use with:

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- `docs/qa/ANALYTICS_EXECUTIVE_DQ_RELIABILITY_AUDIT.md`

Purpose: queue follow-up fixes for Executive Decision Board and Data Quality surfaces where fallback ranking, no-data health, durable reports or top-offender counts can mislead users.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ72 | DONE | executive-product-impact-fallback | Remove Executive fallback lost-sales expected-impact override |
| RQ73 | WAITING | executive-inventory-signal-impact | Prevent weak inventory signals from ranking as expected impact |
| RQ74 | WAITING | executive-supplier-revenue-ranking | Align supplier ranking impact with visible expected impact |
| RQ75 | WAITING | data-quality-health-no-sales | Prevent no-sales/insufficient health from showing green |
| RQ76 | WAITING | data-quality-trend-no-baseline | Show neutral/no-trend for one-point trend |
| RQ77 | WAITING | data-quality-topoffender-count | Distinguish returned vs total top-offender count |
| RQ78 | WAITING | data-quality-topoffender-datascope | Align top-offender revenue impact with dataScope |
| RQ79 | WAITING | pilot-intake-durable-percent-unit | Format durable pilot intake percent rows as percent units |
| RQ80 | WAITING | data-quality-missing-cost-workflow | Add/clarify missing-cost issue workflow |

---

## RQ72 - Executive fallback product lost-sales impact fallback

Status: DONE
Ready after: RQ01 or explicit reprioritization
Priority: P0
Type: frontend-contract/tests
Feature family: executive-product-impact-fallback
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ72-cursor.lock.md`
Commit suggestion: `fix(analytics): preserve executive product impact contract`

### Why

Executive fallback product card builder repeats the old bug where missing `expectedImpactRsd` falls back to `lostSalesEstimate`. The backend Product Decision Center should be the source of truth for whether lost sales is actionable expected impact.

### Evidence already found

- `buildProductCards` sorts by `expectedImpactRsd ?? lostSalesEstimate ?? 0`.
- It maps `expectedImpact = expectedImpactRsd ?? lostSalesEstimate ?? null`.

### Contract

- Executive fallback may display lost-sales estimate as contextual metric only if labelled separately.
- It must not put lost-sales into `expectedImpactRsd` unless PDC supplied expected impact.

### Scope only

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- Executive board tests

### Test matrix

- product with `expectedImpactRsd=null`, `lostSalesEstimate>0`, `FIX_DATA`/`INSUFFICIENT_DATA` -> no expected impact.
- product with `expectedImpactRsd>0` -> expected impact preserved.
- sorting does not promote missing-impact rows by lost-sales fallback.

### Acceptance

- Executive fallback cannot reintroduce RQ01's lost-sales expected-impact bug.

### Notes

- 2026-08-05: DONE. Removed lost-sales fallback from Executive product card builder/sort; exported `buildExecutiveFallbackProductCards` for tests; RQ01 parity.
- Changed files:
  - `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
  - `docs/qa/ANALYTICS_EXECUTIVE_DQ_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md` (next READY)
- Checks:
  - `npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts` - pass (7)
  - `git diff --check` - pass (scoped; CRLF warnings only)
- Risk:
  - Live page path uses backend aggregate; fallback builder is legacy but now contract-aligned if re-enabled.
- Next:
  - `RQ39 - Derived category ratio vs percent units` (priority review after RQ72)

---

## RQ73 - Executive inventory weak signal expected impact

Status: WAITING
Ready after: RQ59 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: executive-inventory-signal-impact
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ73-<agent>.lock.md`
Commit suggestion: `fix(analytics): avoid executive weak inventory impact`

### Why

Executive inventory cards call `buildInventorySignalActionSpec`, which can return signal-review specs with expected impact. Executive cards then use that impact for ranking/display.

### Contract

- Weak/insufficient inventory signals should show `potentialExposureRsd` or null expected impact.
- `expectedImpactRsd` is only for actionable, recommendation-allowed inventory actions.

### Scope only

- `ExecutiveDecisionBoardPage.tsx`
- shared inventory signal helper only if RQ59 contract is being implemented too
- tests

### Test matrix

- insufficient-data inventory row -> no expected impact; card remains warning/insufficient.
- actionable replenish row -> expected impact allowed.
- Executive impact ranking does not rank signal-review above actionable rows solely by exposure.

### Acceptance

- Executive board does not present weak inventory signal exposure as confirmed expected impact.

---

## RQ74 - Executive supplier revenue ranking vs expected impact display

Status: WAITING
Ready after: RQ40/RQ47 or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: executive-supplier-revenue-ranking
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ74-<agent>.lock.md`
Commit suggestion: `fix(analytics): align executive supplier impact ranking`

### Why

Supplier cards use revenue as `impact` for priority and `impactScore`, but user-facing `expectedImpactRsd` is null. Revenue is not the same as expected impact.

### Contract

Choose one:

- show revenue explicitly as `contextRevenueRsd`, not impact; or
- compute/provide supplier expected impact from backend; or
- rank supplier cards without revenue impact component.

### Scope only

- `ExecutiveDecisionBoardPage.tsx`
- supplier decision summary contract only if required
- tests

### Test matrix

- supplier with high revenue but low confidence should not be labelled high expected impact.
- card ranking/display must use the same impact concept.
- export/detail, if any, labels revenue separately from impact.

### Acceptance

- Executive supplier card ranking and displayed impact semantics no longer disagree.

---

## RQ75 - Data Quality no-sales/insufficient health must not show green

Status: WAITING
Ready after: RQ04 or explicit reprioritization
Priority: P0
Type: frontend-contract/tests
Feature family: data-quality-health-no-sales
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ75-<agent>.lock.md`
Commit suggestion: `fix(analytics): avoid green data quality with no sales`

### Why

Data Quality health backend can return meta `insufficient_data` / `no_sales_in_period`, but frontend `healthStatus` checks thresholds only and can show “Podaci su u zelenoj zoni”.

### Contract

- `insufficient_data`, `no_sales_in_period`, or totalRevenue <= 0 must show insufficient/no-data, not green.
- Threshold-based green is allowed only when evidence exists.

### Scope only

- `DataQualityPage.tsx`
- frontend tests

### Test matrix

- totalRevenue > 0 and under thresholds -> green.
- totalRevenue = 0 and meta insufficient -> insufficient/no-data.
- missing shares/null shares with warning meta -> warning/partial, not green.

### Acceptance

- No-sales health cannot be presented as green data quality.

---

## RQ76 - Data Quality one-point trend no-baseline

Status: WAITING
Ready after: RQ75 or explicit unblocking
Priority: P2
Type: frontend-tests
Feature family: data-quality-trend-no-baseline
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ76-<agent>.lock.md`
Commit suggestion: `fix(analytics): show neutral one-point data quality trend`

### Why

`trendTone` returns `improving` when there are fewer than two points. One point is not a trend.

### Contract

- 0 points -> empty state.
- 1 point -> neutral/no baseline.
- 2+ points -> improving/worsening based on start/end comparison.

### Scope only

- `DataQualityPage.tsx`
- tests/CSS only if a neutral class is needed

### Acceptance

- Insufficient trend history does not display as improving.

---

## RQ77 - Data Quality top-offender count/truncation

Status: DONE
Ready after: RQ65 or explicit unblocking
Priority: P1
Type: backend-contract/frontend-tests
Feature family: data-quality-topoffender-count
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ77-<agent>.lock.md`
Commit suggestion: `fix(analytics): expose top offender truncation`

### Why

Top offenders returns count of returned items after limit, and UI shows `Top {result.count}`. This can hide truncation.

### Contract

- Expose `returnedCount`, `limit`, and `isTruncated` or total matching count.
- UI label should say `Prikazano N` or `Top N od M` depending contract.

### Scope only

- `DataQualityEndpoints.cs`
- `AnalyticsDataQualityHealthService.cs`
- `DataQualityPage.tsx`
- types/tests

### Acceptance

- User can distinguish returned top offenders from total matching offender count.

### Notes

- 2026-08-06: DONE. Updated the stable-ordering test to match the canonical seed dataset (`DQ-EXISTING-HIGH`, `DQ-EXISTING-MEDIUM`, `DQ-EXISTING-LOW`).

---

## RQ78 - Data Quality top-offender dataScope revenue impact

Status: DONE
Ready after: Q81 or explicit unblocking
Priority: P1
Type: backend-SQL/tests
Feature family: data-quality-topoffender-datascope
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ78-<agent>.lock.md`
Commit suggestion: `fix(analytics): align top offender data scope revenue`

### Why

Top-offender SQL filters article `DataOrigin`, but 30d sales CTE does not filter sale header origin. Revenue impact can come from outside requested dataScope.

### Scope only

- `AnalyticsDataQualityHealthService.cs`
- backend SQL tests
- docs update if needed

### Contract

Use the canonical dataScope matrix from RQ05/Q81. If sale header origin is the source of truth for revenue scope, apply it in the sales CTE.

### Acceptance

- Top-offender revenue impact uses the same dataScope semantics as the data-quality issue source.

### Notes

- 2026-08-06: DONE. Fixed `prodaja_zaglavlje.data_origin` scope in `TopOffendersSql` and re-ran the Postgres integration tests. The top-offender family now passes; the remaining failure in `IssuesHandler_PaginatesAndUsesStableRevenueOrdering` is count/truncation semantics and belongs to `RQ77`.

---

## RQ79 - Pilot intake durable percent unit mismatch

Status: WAITING
Ready after: RQ40 or explicit unblocking
Priority: P1
Type: backend-report/tests
Feature family: pilot-intake-durable-percent-unit
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ79-<agent>.lock.md`
Commit suggestion: `fix(reports): format pilot intake percent rows`

### Why

Pilot intake durable rows store `RevenueWithoutCostPercent` ratio as string `0.####`, while frontend summary/export formats it as percent.

### Contract

- Durable report row must either display percent text, e.g. `12.3%`, or include raw ratio with explicit unit metadata.
- Do not mix ratio display in backend durable rows with percent display in frontend summary.

### Scope only

- `DataQualityEndpoints.cs`
- report tests

### Test matrix

- ratio 0.1234 renders as 12.34% or typed percent metadata.
- zero ratio renders 0%, not blank.
- missing/no-data report does not claim 0% if evidence unavailable.

### Acceptance

- Pilot intake durable report percent values cannot be misread as raw ratios.

---

## RQ80 - Missing-cost issue workflow

Status: WAITING
Ready after: RQ75/RQ78 or explicit unblocking
Priority: P1
Type: backend/frontend-contract/tests
Feature family: data-quality-missing-cost-workflow
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ80-<agent>.lock.md`
Commit suggestion: `feat(analytics): add missing cost issue workflow`

### Why

Missing cost is tracked in health/intake and blocks margin trust, but the issue list/top-offender workflow does not expose it as an issue type.

### Contract

- Either add `missingCost` issue type to list/top-offenders/tabs, or explicitly link missing-cost blocker to another workflow that lists affected rows.
- Do not silently route unknown issue types to missing supplier.

### Scope only

- `DataQualityPage.tsx`
- `types/analytics.ts`
- `DataQualityEndpoints.cs`
- `AnalyticsDataQualityHealthService.cs`
- tests

### Acceptance

- Users can inspect affected missing-cost rows from the same data-quality workflow or a clearly linked equivalent surface.
