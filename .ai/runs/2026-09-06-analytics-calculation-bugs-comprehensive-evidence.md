# 2026-09-06 - Comprehensive analytics calculation bugs audit

Owner: direct-user-request (Ivan)
Task: Final comprehensive scan of analytics calculations, SQL views, DTOs and formatters for undocumented bugs
Date: 2026-09-06

## Execution

Subagent [Explorer](361b8476-75a8-41e7-81bf-7badf9476c89) performed deep analysis of:
- SQL view definitions (Database/Analytics/Intelligence/*.sql)
- Backend calculation code (Application/Analytics/Queries/*)
- DTO mapping and serialization (CachedAnalyticsEndpoints.cs, SupplierDecisionHubEndpoints.cs)
- Frontend metrics and formatters (analyticsQuality.ts, etc.)

## Findings: 10 new calculation bugs seeded as prompts

All added to canonical queue as RQ183-RQ192 (WAITING, P1/P2):

1. **RQ183** - Journal-derived opening stock for sell-through denominator (P1)
   - Risk: Incomplete journal movements produce plausible but wrong sell-through ratios
   - Root: CachedAnalyticsEndpoints.cs line 629, 5785, 7604-7659

2. **RQ184** - Fixed 30-day divisor for velocity (P1)
   - Risk: Always dividing by 30, not actual elapsed days; sparse sellers understated
   - Root: CachedAnalyticsEndpoints.cs line 596, 631

3. **RQ185** - Active-selling-days vs calendar-days velocity confusion (P1)
   - Risk: Metric labeled "per day" but calculated as active-days only; intermittent sellers overstated
   - Root: CachedAnalyticsEndpoints.cs line 3202, 3497

4. **RQ186** - PDC lost-sales formula lacks velocity factor (P1)
   - Risk: Fast and slow movers with same stock gap get identical lost-sales ranking
   - Root: CachedAnalyticsEndpoints.cs line 5712-5715, 5808

5. **RQ187** - Cache creation time published as last-refresh (P1)
   - Risk: Dashboard shows "fresh" label on cache hits even if underlying data is 10+ hours old
   - Root: CachedAnalyticsEndpoints.cs line 2634-2656 (ApplyStaleCacheWarning)

6. **RQ188** - Discount depth encodes missing list price as 0% (P2)
   - Risk: Invalid/missing list price becomes measured zero discount in price analytics
   - Root: Database/Analytics/Intelligence/023_price_intelligence_v1.sql line 109-112

7. **RQ189** - Demand acceleration hardcodes 1.0 for new products (P2)
   - Risk: New SKUs indistinguishable from truly accelerating products in ranking
   - Root: Database/Analytics/Intelligence/021_product_demand_signals_v1.sql line 198-207

8. **RQ190** - Forecast freshness aggregated optimistically (P1)
   - Risk: Mixed-trust rows appear uniformly fresh when using MAX aggregation
   - Root: GetInventoryForecastHandler.cs line 81, 88-92, 119-126

9. **RQ191** - Frontend percent clamp hides out-of-bounds signals (P2)
   - Risk: Negative/>100 confidence values display as 0% instead of unavailable
   - Root: analyticsQuality.ts line 21-24, 26-35

10. **RQ192** - ML return rate feature missing-data encoding (P2)
    - Risk: No-sales suppliers encoded as 0% return (best-case) not missing data
    - Root: 015_AddSupplierMlRanking.sql line 294, 357

## Validation

- All prompts follow protocol statuses (WAITING, P1/P2)
- No duplicates of RQ176-RQ180 (inventory freshness/actionability/pre-post)
- No duplicates of RQ154-RQ170 (existing queue work)
- Cross-references recorded to RQ141 (lineage), RQ03 (lost-sales), RQ139 (numeric states)

## Delivery

- All 10 prompts added to canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Updated queue status summary table
- Evidence file: this run log

Next steps:
- Analytics/Backend owners review RQ183-RQ192 by priority
- P1 (RQ183-RQ187, RQ190): address before pilot release
- P2 (RQ188-RQ189, RQ191-RQ192): roadmap for next cycle

