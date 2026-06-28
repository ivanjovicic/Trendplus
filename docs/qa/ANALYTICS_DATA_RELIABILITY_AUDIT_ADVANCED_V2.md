# Analytics Data Reliability Audit - Advanced/V2 and Outcome Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only audit addendum

## Scope

This addendum continues `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md` and focuses on additional analytics surfaces not deeply covered in RQ01-RQ12:

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- `Klijent/clientapp/src/services/insightStudioV2Api.ts`
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- selected cached analytics helpers where they affect date/metric reliability

No runtime behavior was changed.

## Additional findings

### R13 - Date-only `toDate` can exclude the selected day in Advanced/V2 analytics

Files:

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- `Klijent/clientapp/src/services/insightStudioV2Api.ts`

Observed:

- The frontend passes `fromDate`/`toDate` query parameters as strings without expanding `toDate`.
- Multiple Advanced/V2 endpoints parse `toDate` with `DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)` and then query `<= to`.

Risk:

- If the UI sends a date-only value like `2026-06-28`, the backend can interpret it as `2026-06-28T00:00:00Z`, excluding almost the whole selected day.
- This can undercount revenue, units, transactions, baskets, lifecycle and reorder signals.

Classification: likely bug.

Recommended prompt: RQ13.

### R14 - Weekly heatmap transaction count appears to count sale lines, not receipts

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- Weekly heatmap selects sale rows joined to sale lines and aggregates `transactions = g.Count()`.
- It does not select or distinct-count sale header IDs in the heatmap aggregation.

Risk:

- A sale with 3 lines counts as 3 transactions.
- Heatmap can overstate transaction count and distort day/week demand patterns.

Classification: likely metric semantics bug.

Recommended prompt: RQ14.

### R15 - Basket affinity denominator can count multi-line baskets that do not have multiple distinct categories

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- SQL uses `HAVING COUNT(*) >= 2` to include multi-line sales.
- It aggregates `array_agg(DISTINCT category)` and later builds category pairs from distinct categories.
- `totalMultiItemTransactions` is based on all returned baskets, even if a basket has only one distinct category.

Risk:

- Pair support denominator can include baskets that could never produce a category pair.
- Support percentage may be understated or confusing.

Classification: suspicious/likely semantic bug.

Recommended prompt: RQ15.

### R16 - Product lifecycle zero-baseline trend maps new sales to 100%

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- Lifecycle trend uses `100` when first-half units are zero and second-half units are positive.
- This is then used to classify lifecycle stage.

Risk:

- No-baseline/new-item behavior is encoded as a normal 100% growth value.
- This repeats the same trust problem found in nivelacija SQL: no baseline should be explicit, not a normal percent.

Classification: suspicious; needs explicit contract.

Recommended prompt: RQ16.

### R17 - Smart reorder can overstate expected profit when cost is missing

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- `reorderCost` is `0` when unit cost is missing.
- `expectedProfit = expectedRevenue - reorderCost`.

Risk:

- Missing cost can make expected profit look artificially high.
- This can rank a reorder as attractive even though profit is unknown.

Classification: likely bug.

Recommended prompt: RQ17.

### R18 - Frontend V2 types drop backend margin/cost coverage metadata

Files:

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- `Klijent/clientapp/src/services/insightStudioV2Api.ts`

Observed:

Backend returns cost/margin quality fields in several V2 endpoints, for example:

- `marginDataCoveragePct`
- `marginDataAvailable`
- `knownCostSkuSharePct`
- `revenueWithCost`

The TypeScript service types expose several metric fields, but omit some of those reliability fields from the public type definitions.

Risk:

- UI can display margin/reorder/supplier metrics without showing the evidence coverage that qualifies them.
- Future UI code may ignore available trust metadata because the TS contract hides it.

Classification: likely contract gap.

Recommended prompt: RQ18.

### R19 - Weekly changelog `new OOS this week` is actually current OOS count

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- Comment says “New OOS this week”.
- Code counts all articles with `Kolicina == 0`, with no previous-week comparison.

Risk:

- The weekly changelog can present a current stockout count as if those stockouts are newly created this week.
- Operators may misread chronic OOS as new weekly deterioration.

Classification: likely bug or label bug.

Recommended prompt: RQ19.

### R20 - Weekly changelog zero-baseline percent changes use 0/100 fallback

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- Revenue/unit changes return `0` when previous week is zero.
- Category changes return `100` when last week is zero and this week is positive.

Risk:

- No-baseline periods can look like ordinary 0% or +100% changes.
- This can hide “new activity/no baseline” semantics.

Classification: suspicious; same family as R16 and SQL zero-baseline findings.

Recommended prompt: RQ20.

### R21 - Outcome summary treats `notMeasured` as measured for coverage/rates

File: `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`

Observed:

- `measuredItems` are all items whose normalized outcome is not `pending`.
- `notMeasured` is a valid normalized outcome status and is counted separately, but still falls into measured items.

Risk:

- Outcome coverage and measured sample size can be inflated by explicit `notMeasured` outcomes.
- A report can appear to have measured outcomes even when the operator said the outcome was not measured.

Classification: likely metric semantics bug.

Recommended prompt: RQ21.

### R22 - Outcome realization ratio is calculated on measured-impact subset only

File: `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`

Observed:

- `measuredImpactItems` include only items with `MeasuredImpactRsd`.
- `expectedImpactRsd` is summed only for that measured-impact subset.
- The service emits warnings when measured impact is missing, but the realization ratio can still look valid for a subset.

Risk:

- Realization ratio can look cleaner than the whole action cohort.
- If high-risk or failed actions lack measured impact, the ratio can be biased.

Classification: suspicious; needs denominator contract.

Recommended prompt: RQ22.

### R23 - Supplier scoring V2 returns bare empty array when total revenue is zero

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- If total revenue is zero, supplier scoring V2 returns `Ok(new List<object>())`.

Risk:

- No-data/no-revenue is indistinguishable from a valid empty supplier list.
- This contradicts the reliability principle that no-data must not silently look clean.

Classification: contract gap.

Recommended prompt: RQ23.

### R24 - Advanced/V2 endpoints generally lack analytics meta/warnings/source status

File: `Api/Endpoints/InsightStudioV2Endpoints.cs`

Observed:

- Most endpoints return bare anonymous objects or arrays.
- Errors return `Problem` with exception detail; successful empty states often have no `dataQualityStatus`, `emptyReason`, `warningCode`, `sourceStatus` or `coverage` wrapper.

Risk:

- Advanced metrics can look production-grade without evidence coverage, freshness or fallback metadata.
- UI cannot consistently distinguish reliable result, helper result, no-data and degraded calculation.

Classification: broad trust-contract gap.

Recommended prompt: RQ24.

## Priority order

1. RQ13 - Date boundary correctness for Advanced/V2.
2. RQ17 - Smart reorder missing-cost expected profit.
3. RQ21/RQ22 - Outcome summary measured/not-measured denominator correctness.
4. RQ14/RQ15 - Heatmap/basket metric semantics.
5. RQ19/RQ20 - Weekly changelog semantics.
6. RQ18/RQ24 - Expose and standardize reliability metadata.
7. RQ16/RQ23 - Lifecycle zero-baseline and supplier scoring no-data contract.

## Recommendation

Keep `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` as the main active queue. `RQ01` remains the current READY prompt. The new RQ13-RQ24 prompts are added in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md` and should stay WAITING until the main queue is intentionally advanced or explicitly reprioritized.
