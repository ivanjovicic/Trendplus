# Analytics Data Reliability Audit - Legacy Advanced and Frontend Derived Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only audit addendum

## Scope

This addendum continues:

- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT_ADVANCED_V2.md`

Focus areas:

- legacy `/api/analytics/advanced/*` endpoints in `Api/Endpoints/InsightStudioEndpoints.cs`
- frontend legacy contracts in `Klijent/clientapp/src/services/insightStudioApi.ts`
- frontend derived/fallback analytics in `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

No runtime behavior was changed.

## Additional findings

### R25 - Legacy Advanced endpoints have the same date-only `toDate` exclusion risk

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Legacy Advanced endpoints parse `toDate` as `DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)`.
- Queries use `p.DatumProdaje <= to`.

Risk:

- Date-only UI value like `2026-06-28` can include only midnight of the selected end date, excluding the rest of the day.
- This affects KPI snapshot, supplier scorecard, ABC classification, category intelligence and reorder plan.

Classification: likely bug; same family as RQ13 but separate legacy scope.

Recommended prompt: RQ25.

### R26 - KPI snapshot previous period overlaps current period at the boundary

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Current period uses `DatumProdaje >= from && DatumProdaje <= to`.
- Previous period uses `DatumProdaje >= prevFrom && DatumProdaje <= from`.

Risk:

- Sales exactly on `from` can be counted in both current and previous period.
- Period-over-period revenue and unit change can be understated or distorted.

Classification: likely bug.

Recommended prompt: RQ26.

### R27 - Supplier/category scorecards use a hard-coded 35% system margin fallback when cost evidence is missing

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Supplier scorecard sets `systemMarginPct` to `35d` when no margin cost revenue exists.
- Category intelligence uses the same fallback.

Risk:

- No cost evidence becomes a normal-looking 35% benchmark.
- Supplier/category profit scoring can look meaningful even when cost coverage is zero.

Classification: likely trust-contract bug.

Recommended prompt: RQ27.

### R28 - ABC classification returns clean zero summary for no sales/no revenue without meta

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- When no sales or total revenue is zero, ABC returns `{ items: [], summary: { countA: 0, countB: 0, countC: 0 } }`.

Risk:

- No-data is indistinguishable from a valid empty ABC result.
- Operators can read this as “there are no A/B/C products”, not “there was no revenue evidence”.

Classification: contract gap.

Recommended prompt: RQ28.

### R29 - Aging stock uses `UpdatedAt` as last-sale fallback for never-sold products

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Aging stock computes last sale per product from sales.
- If there is no sale, it falls back to article `UpdatedAt`.

Risk:

- A never-sold product updated recently can look active, even though it has never sold.
- Aging severity can be under-reported for newly imported or recently edited stock.

Classification: likely bug.

Recommended prompt: RQ29.

### R30 - Daily analysis z-score baseline includes the target day

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Daily analysis loads days from `from` through `to`, finds target day, then calculates mean/stddev from all daily revenue values.
- The target day is included in the baseline used to evaluate whether that same target day is an outlier.

Risk:

- Extreme target days can pull the mean/stddev toward themselves and reduce their z-score.
- Outlier detection can be dampened or missed.

Classification: suspicious/likely statistical bug.

Recommended prompt: RQ30.

### R31 - Daily analysis returns normal-looking zero when target day is missing

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- If target day is absent, `targetRevenue` and `targetUnits` default to zero and `zScore` can become zero.
- `outlierLabel` becomes “Normalan dan”.

Risk:

- Missing target-day data can be shown as a normal zero-sales day.
- This hides whether the store had no sales, no import, or the requested date was outside available data.

Classification: likely no-data/meta bug.

Recommended prompt: RQ31.

### R32 - Category intelligence velocity mixes period sales with all-catalog stock average

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Sales are filtered by selected period.
- Average stock by category is loaded from all current articles, without the same date/source/store/supplier context.
- Velocity is calculated as period units per day divided by current average stock.

Risk:

- A historical sales window is mixed with current catalog stock.
- Category velocity can be distorted after import, stock corrections or assortment changes.

Classification: suspicious; needs contract.

Recommended prompt: RQ32.

### R33 - Legacy reorder `totalReorderValue` uses selling price, not cost/cash-out value

File: `Api/Endpoints/InsightStudioEndpoints.cs`

Observed:

- Reorder summary calculates `totalReorderValue = recommendedQty * prodajnaCena`.

Risk:

- The label can be read as procurement/order value, but formula is potential sales revenue.
- Cash requirement for reorder is not represented.

Classification: likely label/metric semantics bug.

Recommended prompt: RQ33.

### R34 - Legacy frontend types omit backend trust metadata

Files:

- `Api/Endpoints/InsightStudioEndpoints.cs`
- `Klijent/clientapp/src/services/insightStudioApi.ts`

Observed:

- Backend returns trust fields such as `marginDataCoveragePct`, `revenueWithCost`, `marginDataAvailable` in several legacy endpoints.
- Frontend TypeScript types omit many of these fields.

Risk:

- UI tables/charts can show margin, supplier score, category lift or stock value without displaying data coverage.
- Later UI code may ignore available trust metadata because contracts hide it.

Classification: contract gap.

Recommended prompt: RQ34.

### R35 - Frontend derived category intelligence creates approximate revenue from velocity × 30 × price

File: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

Observed:

- Derived category intelligence uses `approxUnits = velocity * 30` and `approxRevenue = approxUnits * netPrice`.
- It returns these as `totalRevenue`/`totalUnits`-shaped fields.

Risk:

- Estimated derived values can look like real booked sales if not labeled clearly.
- This is high risk if derived results are merged as primary in UI.

Classification: likely trust-label bug.

Recommended prompt: RQ35.

### R36 - Frontend derived margin defaults missing margin to zero

File: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

Observed:

- Derived category/price sensitivity calculations use `marginPct ?? 0`.

Risk:

- Missing margin evidence becomes 0% margin rather than unknown.
- Categories or price bands can look low-margin because cost/margin is unavailable, not because margin is actually low.

Classification: likely fake-zero bug.

Recommended prompt: RQ36.

### R37 - Frontend derived aging stock can value stock using net selling price when cost is missing

File: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

Observed:

- Derived aging uses `(cost ?? netPrice ?? 0) * stockQty` for stock value.

Risk:

- If cost is missing, inventory value becomes selling value.
- “Critical stock value” can be overstated as cash cost/value at risk.

Classification: likely metric semantics bug.

Recommended prompt: RQ37.

### R38 - Frontend derived smart reorder has the same missing-cost profit inflation risk as backend V2

File: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`

Observed:

- Derived smart reorder uses `reorderCost = recommendedQty * (cost ?? 0)` and `expectedProfit = expectedRevenue - reorderCost`.

Risk:

- Missing cost can inflate expected profit.
- This can happen even if backend V2 is fixed, because fallback/derived UI path can reintroduce the same issue.

Classification: likely bug.

Recommended prompt: RQ38.

## Priority order

1. RQ26 - KPI period overlap.
2. RQ25 - legacy date boundary correctness.
3. RQ29/RQ31 - no-data vs true signal in aging/daily analysis.
4. RQ27/RQ36/RQ38 - missing margin/cost fake-zero/profit inflation.
5. RQ33/RQ37 - value label/cost vs revenue semantics.
6. RQ30/RQ32 - statistical/mixed-denominator correctness.
7. RQ28/RQ34/RQ35 - no-data meta and frontend trust contracts.

## Recommendation

Keep the active queue discipline:

- Main queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, current READY remains RQ01.
- Advanced/V2 addendum: RQ13-RQ24 remain WAITING.
- This legacy addendum: RQ25-RQ38 remain WAITING until explicitly reprioritized.

If business analytics reliability is the next implementation focus after RQ01, prioritize RQ26 and RQ25 before visual polish because they can directly alter reported trends.
