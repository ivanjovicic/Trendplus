# Analytics SQL Second Pass Review

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only review

## Scope

This is a second, broader pass after `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`.

Reviewed areas:

- Supplier decision endpoint raw SQL and precomputed/live selection.
- Supplier decision startup repair and materialized-view readiness checks.
- Dashboard cached aggregate fallbacks.
- Validation/lost-sales SQL helpers.
- Decision board consumption of supplier decision SQL output.
- Existing SQL prompt queue.

No runtime SQL or endpoint behavior was changed in this review.

## Additional findings

### F07 - Supplier decision startup readiness checks only prove all-time cache readiness

Files:

- `Infrastructure/Seed/DatabaseInitializer.cs`
- `Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs`

Observed:

- `AreSupplierDecisionHubCachesReadyAsync` checks only:
  - `mv_supplier_markdown_dependency_cache`
  - `mv_supplier_decision_score_cache`
  - `mv_supplier_recommendations_cache`
- `RefreshSupplierDecisionHubCachesAsync` also refreshes only those three all-time cache objects.
- The nightly refresh options do include `mv_supplier_decision_score_cache_90d` and `mv_supplier_decision_score_cache_180d`, so worker refresh is aware of the windowed views.

Risk:

- Startup repair can consider the supplier-decision cache stack “ready” even if 90d/180d windowed materialized views are missing or stale.
- The worker can later refresh them if they exist, but readiness and cache-count logs do not prove they exist.

Recommended prompt:

- Add a contract-test and readiness audit for windowed MV objects.
- Do not make startup do heavy refresh by default.
- Decide whether startup should only log missing windowed MVs, build them if missing, or leave them fully worker-owned.

Decision after Q75:

- Startup readiness remains gated by the all-time cache stack only.
- `mv_supplier_decision_score_cache_90d` and `mv_supplier_decision_score_cache_180d` are logged separately as readiness context, not as hard startup gates.
- Missing windowed MVs now surface as an explicit startup warning instead of a silent healthy state.

### F08 - Supplier decision has two query contracts that can drift

File: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

Observed:

- Precomputed path uses `BuildPrecomputedSupplierRowsSql` and reads from `mv_supplier_decision_score_cache`, `mv_supplier_decision_score_cache_90d`, or `mv_supplier_decision_score_cache_180d`.
- Live path uses `BuildSupplierRowsSql` and recomputes a large CTE chain from `vw_supplier_fullprice_signals`.
- The precomputed path is only allowed for no category, no gender, no season, no store, no OOS exclusion and `dataScope=all`.

Risk:

- Recommendation, confidence, filter behavior and null handling can drift between precomputed and live paths.
- A customer changing filters can unknowingly switch SQL contracts.

Recommended prompt:

- Add a parity test matrix before changing formulas.
- Document where parity must be exact and where path-specific behavior is intended.
- Keep any behavior change separate from performance work.

### F09 - 30d request intentionally uses 90d helper data, but needs stronger tests

File: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

Observed:

- Requested dataset can be `30d`.
- Effective dataset becomes `90d` because there is no 30d MV.
- The code documents this as a helper signal, with no silent final recommendation fallback.

Risk:

- The wording and `RecommendationAllowed=false` behavior must stay locked.
- Future refactors could make 30d appear as a final 30d scorecard even though the source is 90d.

Recommended prompt:

- Add tests for requested/effective dataset metadata.
- Add route-level tests that 30d uses `no_mv_30d` and does not silently allow final recommendation when fallback is used.

### F10 - Supplier decision reader helpers convert DB nulls to zero/string empty

File: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

Observed:

- `GetInt32` returns `0` for `DBNull`.
- `GetDecimal` returns `0m` for `DBNull`.
- `GetString` returns empty string for `DBNull`.

Risk:

- SQL nulls that were meant to mean “unknown/not applicable” can become valid-looking zeros in API DTOs.
- This is especially risky for impact, confidence, margin, return-rate and ML-related fields.

Recommended prompt:

- Do not globally change helpers.
- Add a field-by-field nullability audit and only introduce nullable DTO/read helpers for fields where null has business meaning.

### F11 - Supplier decision endpoint still contains user-facing mojibake strings

File: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

Observed:

- Some recommendation labels/reasons contain mojibake such as `Å¡`, `Ä`, `Å¾`.

Risk:

- Supplier report and decision board explanations can look unprofessional in Serbian UI.
- This also indicates the encoding guardrail still needs broader backend/source coverage, not only docs/frontend hotspots.

Recommended prompt:

- Add backend/source encoding coverage for user-facing strings.
- Fix only visible copy; do not change recommendation codes or thresholds.

### F12 - Cached dashboard endpoints mix explicit meta errors with silent empty-array fallbacks

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- Main dashboard endpoints often return `Meta` error objects when aggregate relations are missing or SQL times out.
- Some filter endpoints return an empty array on timeout/database issue without a meta payload.

Risk:

- Empty filter lists can look like “no suppliers/stores” instead of “filter query failed”.
- This can hide ancillary failures and make dashboards appear valid when the filter surface is degraded.

Recommended prompt:

- Add a small response contract review for filter/list endpoints.
- Keep backward-compatible arrays if required, but add warning/meta wrapper or documented UI handling.

### F13 - Lost-sales validation fallback can return clean zero when evidence is unavailable

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `GetLostSalesSnapshotAsync` returns `(0, 0m)` when connection is unavailable.
- It first tries `vw_analytics_oos_lost_sales`; if unavailable, it falls back to recent sales and current stock.

Risk:

- Lost-sales can look clean when the real state is “cannot compute”.
- That is high-risk because OOS/replenishment decisions use similar trust semantics.

Recommended prompt:

- Add explicit confidence/source metadata for validation/lost-sales results.
- Do not change the formula first; lock source status and unavailable state first.

### F14 - DataScope and store/supplier filtering should be audited across raw SQL helpers

Files:

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- Supplier decision precomputed path only applies for `dataScope=all`, no store and no several other filters.
- Live path applies `DataOrigin` filters through `Artikli`.
- Lost-sales fallback applies scope filters in both recent-sales and article-stock parts.

Risk:

- Different helpers can interpret `existing`, `imported`, `all`, store and supplier scope differently.
- That can create inconsistent decision board sections for the same requested filter set.

Recommended prompt:

- Add a cross-endpoint dataScope/store/supplier SQL audit.
- Do not change query behavior until mismatches are listed and testable.

## What was improved in this pass

Added documentation and queue entries only:

- A second-pass review document.
- New SQL analytics queue prompts for findings not covered by Q69-Q74.
- Kept Q69 as the only READY prompt so Codex/Cursor do not split into overlapping SQL work prematurely.

## New recommended queue entries

Added to `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md` as WAITING prompts:

- Q75 - Supplier decision windowed MV startup readiness audit
- Q76 - Supplier decision precomputed/live SQL parity matrix
- Q77 - Supplier decision nullable reader and detail-query trust audit
- Q78 - Backend encoding guardrail for analytics decision strings
- Q79 - Dashboard filter/list fallback meta contract
- Q80 - Lost-sales validation source/confidence contract
- Q81 - Analytics dataScope/store/supplier SQL consistency audit
- Q82 - SQL timeout, cancellation and observability consistency audit

## Do not implement yet

Do not implement Q75-Q82 until Q69 is DONE. Q69 should produce exact string/test evidence and may re-order or split these follow-ups.
