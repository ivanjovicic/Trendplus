# Analytics Reliability Audit Prompts - 2026-09-07 Round 12

- Queue: `direct-user-request`
- Scope: production analytics screens and their direct API/backend contracts; standalone Trend, forecast, Shopify/vendor integrations and test-only functionality excluded.
- Status: audit completed; four new deduplicated `WAITING` prompts added to the canonical queue.
- Canonical queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

## Audit Method

Read before inspection:

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

The audit followed the runtime owner from page to API and backend builder, inspected the nearest tests, and checked `git log`/`git blame` for the exact lines. Existing queue ownership was checked before creating each prompt.

## New Findings

### RQ254: PDC query time is exposed as last refresh

`BuildProductDecisionCenterAsync` creates `nowUtc` for the response and passes it as `LastRefreshAtUtc` for both populated and empty PDC results. `DecisionBoardEndpoints` calls this builder directly, so it can expose a query timestamp even when no successful refresh record exists. The generic `RQ187` cache-hit prompt does not cover this direct builder/cache-miss path.

Evidence:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5661-5678,6048-6056`
- `Api/Endpoints/DecisionBoardEndpoints.cs:40-75`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:2634-2656`

### RQ255: PDC nullable stock is coerced to zero

PDC maps `Artikli.Kolicina` and `Artikli.MinimalnaKolicina` through `?? 0`. The substituted values feed stock gap, slow-stock capital, opening stock, stock-cover/sell-through input and the evidence chain. Existing `RQ158` repaired inventory list/detail semantics, but the PDC-specific snapshot and evidence path remains separate and non-nullable.

Evidence:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5481-5485,5708-5709`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5847,5923-5940,5973-5975`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6656-6669`

### RQ256: PDC no-sales margin coverage is a fake measured zero

PDC calculates cost coverage as `0m` when current-period revenue is zero. Since revenue is the denominator, this state is undefined/unavailable, not measured `0%`. The row is labelled `Nizak kvalitet` and the evidence chain says `Pokrivenost nabavnom cenom 0%`, which presents absence of sales as measured cost coverage.

Evidence:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5831-5845,5880,5973`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6667-6669`
- The PDC row DTO at `:8038-8048` is non-nullable, while the separate top-product contract already uses nullable coverage at `:7975`.

### RQ257: Supplier numeric classification accepts Infinity

Supplier Sales Stats and Supplier Decision Hub use `Number.isNaN` for PoP/trend and ratio-to-percent availability checks. `Infinity` is not `NaN`, so it can enter an available/classified branch while the shared formatter renders it as unavailable. This creates contradictory sort, class, tooltip, detail, action and export behavior. Prior pre/post fixes changed selected helpers to `Number.isFinite` but did not cover these branches.

Evidence:

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:254-258,320-331,396-407`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:102-106,130-135`
- `Klijent/clientapp/src/utils/analyticsFormatters.ts:6-58` correctly rejects non-finite display values.

## Deduplication Decisions

- `RQ187` remains owner for generic cache-hit metadata; `RQ254` is limited to PDC direct-builder/cache-miss metadata.
- `RQ158` remains owner for general inventory null quantity/minimum behavior; `RQ255` is limited to the PDC article snapshot, stock signal and evidence-chain projection.
- `RQ157` remains owner for missing PDC trend/margin/split decision evidence; `RQ256` is limited to the separate no-sales margin-coverage denominator state.
- `RQ191` remains owner for shared confidence/reliability range formatting; `RQ257` is limited to supplier-specific classification and ratio adapters.
- `RQ143` and `RQ145` remain broad decision-ownership and parity gates. The new prompts provide concrete entry points and regression reproductions, not alternative owners.

## Queue State

- Added: `RQ254`, `RQ255`, `RQ256`, `RQ257`, all `WAITING`.
- Preserved: canonical current `READY` remains `RQ169`.
- No queue claim, lock, promotion or implementation was performed because this was a direct audit request, not an assigned queue execution.

