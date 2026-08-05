# Analytics Executive/Data Quality Reliability Audit

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: mixed audit; one small runtime/export fix landed separately

## Scope

This pass focuses on executive decision board and data-quality/reporting surfaces that can mislead users even when underlying backend data is partially correct.

Reviewed files:

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx`
- `Klijent/clientapp/src/types/analytics.ts`
- `Api/Endpoints/DataQualityEndpoints.cs`
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

## Fixed in this pass

### F02 - Pilot intake CSV/export converted optional missing counters to zero

File changed:

- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx`

Observed before:

- `missingColorCount`, `missingSizeCount` and `duplicateSkuCount` are optional fields for compatibility.
- CSV/export displayed missing values as `0` using `?? 0`.

Risk before:

- A missing/unavailable optional counter could look like true zero issues.

Fix:

- Added `formatOptionalCount`.
- CSV and server-export payload now render missing optional counters as `-`, not `0`.

Commit:

- `2b1dff8adff57dafccda6d2a3850de594b0d6f1e`

Tests were not run in this environment.

## New findings

### R72 - Executive fallback product cards still use lost-sales fallback as expected impact

File:

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

Observed:

- `buildProductCards` sorts by `a.expectedImpactRsd ?? a.lostSalesEstimate ?? 0`.
- It also sets `expectedImpact = row.expectedImpactRsd ?? row.lostSalesEstimate ?? null`.

Risk:

- This repeats the same trust-class bug as RQ01 in a legacy/fallback Executive Board builder.
- Even if the current page primarily uses backend aggregate, keeping fallback code with a different contract is a regression risk.

Classification: fixed in RQ72 (2026-08-05).

Fix notes:

- `buildExecutiveFallbackProductCards` uses only `expectedImpactRsd` for display, ranking and `impactScore`.
- `lostSalesEstimate` is no longer promoted into expected impact (RQ01 parity).
- Unit tests cover FIX_DATA/INSUFFICIENT_DATA null impact, preserved PDC impact, and sort without lost-sales promotion.

Recommended prompt: RQ72 (DONE).

### R73 - Executive inventory cards inherit signal-review expected impact from InventoryPage helper

File:

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

Observed:

- `buildInventoryCards` calls `buildInventorySignalActionSpec(row)`.
- The shared helper can return `SIGNAL_REVIEW`/insufficient-data action specs with `expectedImpactRsd`.
- The Executive card then uses `actionSpec.expectedImpactRsd ?? row.estimatedValueAmount ?? row.estimatedValue ?? null` as impact.

Risk:

- A weak inventory signal can become a financially ranked Executive Board card.
- This overlaps RQ59 but appears on a separate surface.

Classification: likely fake-impact cross-surface bug.

Recommended prompt: RQ73.

### R74 - Executive supplier cards rank by revenue while exposing no expected impact

File:

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`

Observed:

- Supplier cards calculate `impact = item.revenue > 0 ? item.revenue : null` and use it in `computePriorityScore` and `impactScore`.
- But the card payload sets `expectedImpactRsd: null`.

Risk:

- The card can be ranked as high impact using revenue, while user-visible expected impact says unavailable.
- Revenue is not necessarily expected impact.

Classification: likely ranking/display contract mismatch.

Recommended prompt: RQ74.

### R75 - Data Quality health card can show green when health meta says insufficient/no-sales

File:

- `Klijent/clientapp/src/pages/DataQualityPage.tsx`

Observed:

- Backend health meta can set `DataQualityStatus = insufficient_data` and `EmptyReason = no_sales_in_period` when total revenue is zero.
- Frontend `healthStatus` ignores health meta and checks only thresholds with null shares defaulted to zero.
- If thresholds are not exceeded, it returns `Podaci su u zelenoj zoni`.

Risk:

- No revenue / insufficient evidence can appear as green health.

Classification: likely fake-green bug.

Recommended prompt: RQ75.

### R76 - Data Quality trend line treats one-point trend as improving

File:

- `Klijent/clientapp/src/pages/DataQualityPage.tsx`

Observed:

- `trendTone` returns `improving` when `points.length < 2`.
- A one-point trend can be styled as improving even though no trend exists.

Risk:

- Insufficient history can look like improving data quality.

Classification: small fake-green trend semantics bug.

Recommended prompt: RQ76.

### R77 - Data Quality top-offender count is returned count, not total matching offenders

Files:

- `DataQualityPage.tsx`
- `DataQualityEndpoints.cs`
- `AnalyticsDataQualityHealthService.cs`

Observed:

- Backend `top-offenders` returns `Count = items.Count` after `LIMIT`.
- UI shows `Top {result.count}`.

Risk:

- Users can read this as total count of top offenders for that issue type.
- The panel does not expose truncation or total matching offenders.

Classification: count/truncation semantics bug.

Recommended prompt: RQ77.

### R78 - Data Quality top-offender 30d sales ignores sale-header dataScope

File:

- `AnalyticsDataQualityHealthService.cs`

Observed:

- `sales_30d` CTE filters only by date and groups all sale items.
- `quality_source` later filters article `DataOrigin` by requested dataScope.

Risk:

- When scoped to imported/existing data, revenue impact can still include sales from outside the intended sale-header origin.
- This is related to prior dataScope findings but specific to top-offender impact ranking.

Classification: likely dataScope/revenue-impact bug.

Recommended prompt: RQ78.

### R79 - Pilot intake durable report rows store ratio values without percent display/unit

File:

- `Api/Endpoints/DataQualityEndpoints.cs`

Observed:

- `BuildPilotIntakeRows` writes `report.Impact.RevenueWithoutCostPercent.ToString("0.####")`.
- Backend has converted health percent to ratio for `RevenueWithoutCostPercent`.

Risk:

- Durable report row can show `0.1234` where user expects `12.34%`, unlike frontend summary/export that uses `fmtPctFromRatio`.

Classification: likely report unit mismatch.

Recommended prompt: RQ79.

### R80 - Data Quality issue list does not expose missing-cost issue type despite health/intake tracking it

Files:

- `DataQualityPage.tsx`
- `DataQualityEndpoints.cs`
- `AnalyticsDataQualityHealthService.cs`
- `types/analytics.ts`

Observed:

- `DataQualityIssueType` supports only `missingSupplier`, `missingShoeType`, `invalidName`.
- Frontend issue tabs do not include missing cost.
- Backend top-offender normalization also falls back unknown issue type to missing supplier.
- Health/intake track missing cost count and revenue share separately.

Risk:

- Users see missing-cost as a key blocker but cannot inspect rows through the same issue-list/top-offender workflow.

Classification: product gap / data-quality workflow mismatch.

Recommended prompt: RQ80.

## Recommended order

1. RQ72 - remove Executive fallback lost-sales impact fallback. (DONE 2026-08-05)
2. RQ75 - no-sales/insufficient health must not show green.
3. RQ79 - durable pilot intake percent unit mismatch.
4. RQ73/RQ74 - Executive inventory/supplier impact ranking/display mismatch.
5. RQ77/RQ78 - top-offender count/dataScope revenue impact.
6. RQ80 - add missing-cost issue workflow.
7. RQ76 - one-point trend should be neutral/no-trend.
