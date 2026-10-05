# Review recertify 2026-10-05

Task ID: review-recertify-2026-10-05
Queue: direct-user-request
Date: 2026-10-05
Agent/tool: Cursor Cloud
Delivery target: main
Working branch / PR: `cursor/review-recertify-daily-e050`, direct delivery to main
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

- Refetched `origin/main` at `dfdaf5a5` before edits. Today's C#/React commits through RQ588 claim were already on that SHA, including the earlier Daily Sales half-open HTTP path, startup `[FromServices]` binding, and reorder revenue/cost split.
- Recertified those areas against current code. Remaining defect: `DailySalesStatsService` still expanded an equal `from`/`to` into a one-day inclusive window unless `requestedToIsExclusive` was set. Direct service tests and receipt reconciliation still used that old window.
- Service now treats `requestedToUtc` as the exclusive end. Tests that previously passed the last included day now pass the next midnight. A new service test proves a sale exactly at `2026-06-16T00:00` is outside `[2026-06-15, 2026-06-16)`.
- Daily Sales meta sets `dateBoundaryConvention=half_open_utc` before source freshness. Freshness reads `meta.DateBoundaryConvention` before the context fallback, so a half-open label uses `DatumProdaje < requestedToUtc`.
- Date-only query strings still expand the named calendar day into that exclusive end, and the response convention is `half_open_utc` rather than a second inclusive label.
- Reorder presentation test covers revenue `null` with procurement cost `2000`: revenue stays `N/D`.
- RQ49 marked DONE because the RQ591 field split plus this presentation proof meets its acceptance. RQ588 remains the canonical IN_PROGRESS owner and was not taken.

## Files changed

- `Api/Services/DailySalesStatsService.cs`
- `Api/Services/OperationsSourceFreshnessService.cs`
- `Api/Endpoints/DailySalesStatsEndpoints.cs`
- `Api.Tests/DailySalesStatsServiceTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/DailySalesReceiptReconciliationTests.cs`
- `Klijent/clientapp/src/pages/__tests__/insightStudioTrustPresentation.spec.ts`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- `.ai/runs/2026-10-05-review-recertify-evidence.md`

## Validation run

- Focused Api.Tests filter (Daily Sales service/integration/receipt, freshness, startup/admin readiness) -> pass, 96/96.
- Follow-up filter (admin repair, cache invalidate, critical routes, inventory insights/balance, valuation policy) -> pass, 23/23.
- Frontend focused vitest (trust presentation, derived metrics, supplier open-action keys, Daily Sales premium) -> pass, 49/49.
- `npm run typecheck` and `npm run check:analytics-guardrails` -> pass after restoring the missing `ReorderPlan` import and retargeting drifted baseline lines.
- Full `Api.Tests` on this environment -> 1817 passed, 40 skipped, 23 failed before the binding/valuation repairs. After those repairs the named product failures in that set were re-run green. Remaining full-suite failures that were not product regressions of this pass: RQ561 and nivelacija normalization require PostgreSQL Testcontainers; admin data-source tests require an initialized SQL connection string; `SimulateScenarios_WithReliableEqualCost_KeepsGenuineZeroMargin` expects markdown margin 0 while the existing discount formula returns about -832 and that scorer was not part of today's commit set.

## Validation not run

- Live Render/provider probes. Not a start gate for this repository-local recertification.

## Documentation impact

- RQ49 summary and detail status are both DONE. Main queue primary remains RQ588 IN_PROGRESS.

## What was missed

- Date-only HTTP `toDate=2026-01-02` still includes that calendar day by expanding to the next exclusive instant. Timestamp `toDate=2026-01-02T00:00:00Z` stays exclusive. The stored convention for both is now `half_open_utc`.

## Risks

- Any unpublished caller that passed an inclusive last day without adding one day will now drop that day. In-repo callers were updated.

## Post-close routing recovery

- Recovery base: pending post-push `origin/main`.
- RQ588 stays IN_PROGRESS. No new READY promotion from this review.

## Next

- Leave RQ588 to its existing claim.
