# Review recertify 2026-10-05

Task ID: review-recertify-2026-10-05
Queue: direct-user-request
Date: 2026-10-05
Agent/tool: Cursor Cloud
Delivery target: main
Working branch / PR: `cursor/review-recertify-daily-e050`, direct delivery to main
Main commit SHA: close-out implementation `97d50a5b103e619cac7a7ee41eb55f91cb797e9f` on top of `90a47a5427bb37de7e7e58af99489e2497c8b70f`; the docs stamp that records this SHA is the delivery tip and is verified with `HEAD == origin/main` after push
Implementation SHA: `97d50a5b103e619cac7a7ee41eb55f91cb797e9f` (CI close-out). Half-open contract and retail-store seed remain `ee33c490b9865279d04b25146f6e38d00a891763`
RQ561 assertion SHA: `ee33c490b9865279d04b25146f6e38d00a891763` (six-screen 1/1 on run `37353827984`)
Main verification: `ee33c490` was `origin/main` before the presentation pass advanced main to `90a47a54`. This close-out is rebased onto that SHA. Tip equality is checked after push.
Evidence state: synchronized

## What was done

- Refetched `origin/main` at `dfdaf5a5` before edits. Today's C#/React commits through the RQ588 claim were already on that SHA, including the earlier Daily Sales half-open HTTP path, startup `[FromServices]` binding, and reorder revenue/cost split. During delivery, `origin/main` advanced to `4ed7012d` (RQ588 migration-discovery test). This branch was rebased onto that SHA with no conflicts.
- Recertified those areas against current code. Remaining defect: `DailySalesStatsService` still expanded an equal `from`/`to` into a one-day inclusive window unless `requestedToIsExclusive` was set. Direct service tests and receipt reconciliation still used that old window.
- Service now treats `requestedToUtc` as the exclusive end. Tests that previously passed the last included day now pass the next midnight. A new service test proves a sale exactly at `2026-06-16T00:00` is outside `[2026-06-15, 2026-06-16)`.
- Daily Sales meta sets `dateBoundaryConvention=half_open_utc` before source freshness. Freshness reads `meta.DateBoundaryConvention` before the context fallback, so a half-open label uses `DatumProdaje < requestedToUtc`.
- Date-only query strings still expand the named calendar day into that exclusive end, and the response convention is `half_open_utc` rather than a second inclusive label.
- Concrete Minimal API service parameters that the binder inferred as a body (`AnalyticsCacheAdminService`, `OperationsAnalyticsIntegrityRegistry`) are `[FromServices]`. `StartupReadinessState` was already `[FromServices]` on `main`. Narrow admin-repair hosts register the cache admin service.
- Inventory valuation aggregate returns `null` total value when stock exists but no unit cost is known. Insights tests expect `estimated_from_sale_cost` from the sale-line `NabavnaCena`, matching `InventoryValuationAndAgingPolicy`.
- Reorder presentation test covers revenue `null` with procurement cost `2000`: revenue stays `N/D`. `ReorderPlan` import restored so typecheck compiles.
- RQ49 marked DONE because the RQ591 field split plus this presentation proof meets its acceptance. RQ48 summary row corrected from READY to DONE to match its section. RQ588 remains the canonical IN_PROGRESS owner and was not taken.
- Exact-main CI on `4ed7012d` failed RQ561 because date-only Daily Sales added a day. Run `37350895055` on `4bdb4400` then counted 7 September items against Shoe Type's half-open 6. Date-only `toDate` is now the exclusive midnight. Local PostgreSQL proof of `OperationsAnalyticsAllRoutesIntegrationTests` then failed at pre-nivelacija `totalCandidates` 0 because `StoresDim` had no retail names and no `DataOrigin` column, so the store lookup failed closed into an empty candidate list. The six-screen seed now adds `Trend PLUS 1/2` and the column. That test passed 1/1 locally after the seed fix.
- The same `4ed7012d` run's `integrityRegistry` body-inference failures are the binding defect fixed in this delivery. Markdown margin `-832` and the supplier drift-evidence string mismatch (`rq487-endpoint-drift-evidence` vs null) were already red on that pre-change SHA and remained red on `4bdb4400`. Vercel on `4bdb4400` is `Deployment rate limited — retry in 24 hours`, not a build failure.

## Files changed

- `Api/Services/DailySalesStatsService.cs`
- `Api/Services/OperationsSourceFreshnessService.cs`
- `Api/Endpoints/DailySalesStatsEndpoints.cs`
- `Api/Endpoints/AdminRepairEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Application/Analytics/InventoryValuationAndAgingPolicy.cs`
- `Api.Tests/DailySalesStatsServiceTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/DailySalesReceiptReconciliationTests.cs`
- `Api.Tests/AdminRepairAuthorizationTests.cs`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Api.Tests/InventoryValuationAndAgingPolicyTests.cs`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/pages/__tests__/insightStudioTrustPresentation.spec.ts`
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-05-review-recertify-evidence.md`

## Validation run

- Focused Api.Tests filter (Daily Sales service/integration/receipt, freshness, startup/admin readiness) -> pass, 96/96.
- Follow-up filter (admin repair, cache invalidate, critical routes, inventory insights/balance, valuation policy) -> pass, 23/23.
- Frontend focused vitest (trust presentation, derived metrics, supplier open-action keys, Daily Sales premium) -> pass, 49/49.
- `npm run typecheck` and `npm run check:analytics-guardrails` -> pass after restoring the missing `ReorderPlan` import and retargeting drifted baseline lines.
- Frontend production build -> pass (chunk-size warning only).
- After the RQ48 summary sync: `git diff --check`, `node scripts/check-prompt-queues.mjs` (709 tasks), `node scripts/check-agent-instructions.mjs` (14 files) and `node scripts/check-planning-architecture.mjs` (80 tasks) -> pass.
- Release re-run of Daily Sales, freshness, startup/admin readiness, admin repair, critical route mappings and valuation policy -> pass, 113/113.
- Full `Api.Tests` on this environment -> 1817 passed, 40 skipped, 23 failed before the binding/valuation repairs. After those repairs the named product failures in that set were re-run green. Remaining full-suite failures that were not product regressions of this pass:
  - RQ561 six-screen and nivelacija event normalization: PostgreSQL Testcontainers fixture is not available in this environment. The failure text is the missing container, not a Daily Sales period assertion.
  - `AdminDataSourceEndpointsTests`: `The ConnectionString property has not been initialized` (SqlClient). Environment.
  - `SimulateScenarios_WithReliableEqualCost_KeepsGenuineZeroMargin`: markdown expected margin is about -832 because the existing discount goes below an equal purchase price. Scorer commits predate this review and were not changed. Genuine zero highlight margin stays 0; the markdown assertion was not rewritten to hide the negative result.

## Validation not run

- Live Render/provider probes. Not a start gate for this repository-local recertification.
- Full `Api.Tests` was not repeated after the binding/valuation repairs and the clean rebase onto `4ed7012d`. The previously red product filters were re-run green (23/23). RQ588's new discovery test was not modified.

## Documentation impact

- RQ48 summary and detail status are both DONE.
- RQ49 summary and detail status are both DONE.
- Main queue primary remains RQ588 IN_PROGRESS.
- Supplemental UI READY pointer remains P-UI-39, with collision-safe lanes P-UI-40, P-UI-41, P-UI-47, P-UI-49 and P-UI-52.

## What was missed

- Date-only and timestamp Daily Sales ends are the same exclusive midnight. UI calendar `Do` stays inclusive; the client sends the next exclusive instant.
- Vercel deployment rate limit is an external provider throttle, not a repository test result.

## Risks

- Any unpublished caller that passed an inclusive last day without adding one day will now drop that day. In-repo callers were updated.
- Hosts that already migrated a richer `StoresDim` are unchanged: `CREATE TABLE IF NOT EXISTS` is a no-op and the column add still fills `DataOrigin` when the migration chain omitted it.

## CI close-out after `ee33c490`

Exact-main run `37353827984` on `ee33c490b9865279d04b25146f6e38d00a891763`:

- RQ561 six-screen proof passed 1/1.
- RQ453 failed only while publishing the manifest. Vitest passed 39/39 and wrote `Klijent/TestResults/rq453-trust-state/vitest.json` because the step cwd is `Klijent/clientapp` and the output path was `../TestResults`. The publisher reads repository-root `TestResults/rq453-trust-state/vitest.json`. The workflow now writes `../../TestResults/...`. A local publisher replay fails on the old path and passes on the repository-root path (39/39, verdict PASS).
- Complete backend suite: 1842 passed, 4 failed, 40 skipped, total 1886.
  - `NivelacijaEndpoints_ExposeTheSameFlatRateEffect...` and `NivelacijaEndpoints_KeepTheSameNonZeroEffect...` failed with `relation "StoresDim" does not exist` at the shared seed. The seed now creates the table before altering it. Local supplier parity after that change: those two tests passed.
  - `SupplierEndpoint_PostgresParity_CacheHitAndIntegrityChangePreserveValuesAndRefreshGate` then returned to `operationsIntegrityEvidenceId` null. The fixture snapshot had no context fingerprint, so `ApplyFamilyEvidence` withheld the id. The fixture now binds `SupplierShoeType` to `[2026-07-01, 2026-07-07)` / `all` / no store. Local result: 3/3 passed. Analytics totals and the drift-evidence assertion were not relaxed.
  - `SimulateScenarios_WithReliableEqualCost_KeepsGenuineZeroMargin` expected markdown margin 0 and got -832. Commit `ffd2f52d` (2026-10-03) stopped clamping that margin at zero; the 8% markdown-scenario floor on an equal price is a real negative margin. Highlight margin stays 0. The test now expects markdown margin `-832` and was renamed. The scorer was not changed.

Earlier proof that the drift-evidence and zero-margin failures predate this close-out: run `37348409907` on `4ed7012d` and run `37350895055` on `4bdb4400`.

## Post-close routing recovery

- Recovery base before the CI close-out: `90a47a5427bb37de7e7e58af99489e2497c8b70f` (presentation pass-2, already on main).
- Active queue headers scanned: main RQ queue, Operations, UI/table, Advanced, Legacy, Action Outcome, Executive/DQ, Nivelacija, Inventory signals, Test hardening, Supplier audit, Cross-surface, SQL, UI premium, Stabilization, Backend CI, Platform evolution, Multitenancy, and `MASTER_ROADMAP.md`.
- Live section statuses that say READY are historical promotion notes inside DONE tasks (`RQ134`/`RQ135` notes, `RQ542`/`RQ544` notes). They are not current summary rows.
- No newly dependency-complete RQ prompt was promoted. Canonical pointer remains RQ588 IN_PROGRESS. Supplemental UI READY remains P-UI-39, with P-UI-40, P-UI-41, P-UI-47, P-UI-49 and P-UI-52.
- RQ48, RQ49, RQ88, RQ584, RQ590 and RQ591 stay DONE. RQ49 acceptance remains the RQ591 field split plus the null-revenue presentation proof.

## Next

- Leave RQ588 to its existing claim.
- Inspect Actions on the close-out SHA after it is on `origin/main`. Cancelled runs caused by a newer push are not failures. Vercel rate limit is not a code defect.

## Frontend quality-gate follow-up

Exact-main Analytics Quality Gates on `cd29c12da04db816b0a6dbf9d611d281818f27b4` failed the frontend analytics job. Planning Governance and the backend analytics suite on that SHA were already green. `origin/main` then advanced to `7265745a12068641405e2a80cfc08a4b70e14fee` (trust details start collapsed).

Fixes, test-side except for the existing guardrail baseline:

- Data Quality trend chart is queried by `Grafikon trenda kvaliteta podataka`.
- Configuration ping expects `U redu (120 ms)`.
- Inventory queue status is keyed by `article=501` because a later effect was clearing the queued marker.
- Known-only supplier PoP expects `fmtPct(null)` (`Nije dostupno`) and rejects a displayed `0`.
- Trust proofs open `analytics-trust-details-toggle` before readiness, dataset, provenance and evidence assertions. The visible supplier title is `Dobavljači: Pregled`. Expanded freshness `Sveže` is allowed in both the context strip and the details panel.
- Guardrail baseline lines moved with the UI commits already on `7265745a`: Global Trends 148, Supplier Footwear 363/543/552, Supplier Sales 1985. Same reviewed rules; no new exception.

Local proof after those edits, base `7265745a`:

- `npm run test:analytics`: 144 files, 1067 passed.
- `npm run check:analytics-guardrails` and typecheck: pass, 39 known violations, 0 removed.
- `npm run build`: pass.

Recovery scan of active queue headers on this base: canonical pointer remains RQ588 IN_PROGRESS. Supplemental UI READY remains P-UI-39 plus P-UI-40, P-UI-41, P-UI-47, P-UI-49 and P-UI-52. RQ48, RQ49, RQ88, RQ584, RQ590 and RQ591 stay DONE. The delivery tip is the commit that contains this section; verify `HEAD == origin/main` after push.

## Frontend quality-gate follow-up on `ef266796`

Exact-main Analytics Quality Gates run `37358374846` failed the frontend job: 3 tests, 1064 passed. Planning Governance on that SHA succeeded. The backend suite was not part of that workflow.

The dashboard tests clicked `Prikaži detaljnu analizu` while `loading` still disabled the button. `load()` then sets `showDetailedAnalysis` back to false, so the gainers section and the receipt-grain note never stayed open. Those tests now wait until the button is enabled. The inventory empty-state reset now waits for the store options to settle before clicking `Poništi filtere`, matching the sibling test that already passed on that run.

Local `npm run test:analytics` after the change: 144 files, 1067 passed.

Exact-main run `37359753968` on `f5d2269b` then failed one more inventory spec: `Osveži` was queried while the first load still showed `Učitavanje bilansa zaliha...`. The refresh click now waits until that button is mounted. Vercel on the same SHA is `Deployment rate limited — retry in 24 hours`, not a build failure.

Exact-main run `37360327352` on `8cc5b7ac` failed `clears queued marker when inventory source keys disappear`: the empty state did not replace the loaded table within the default 1s wait. Search is deferred, so that wait is now 8s. The queued-marker oracle is unchanged.

Exact-main run `37360828074` on `9daf92f615b0e15e1d08f09587eb35a4dfa3e56c` failed the frontend job 3/1067. Vercel on that SHA is still `Deployment rate limited — retry in 24 hours`.

- Data Quality context scope is asserted only after `getDataQualityTopOffenders("missingSupplier", 10, "imported")` has happened. Earlier calls in the file used `null` and were cleared for this test. The imported scope oracle is unchanged.
- Pre/Post special-character detail waits until the Novi Beograd store option is present and `Primeni` is enabled, then opens `Otvori puni detalj` from the live table. The `row:1` / `A/B & Co` snapshot oracle is unchanged.
- Pre-nivelacija provenance waits until the mocked header text contains `period je usidren na poslednju prodaju 2026-08-05`. `evidenceBasis` now recomputes when the response object changes, including `totalCandidates`, so a reused evidence window cannot keep a null basis.

Local `npm run test:analytics` after those edits: 144 files, 1067 passed.

Fresh header scan of the active RQ, SQL, UI, stabilization, backend-CI and multitenancy queues: canonical pointer remains RQ588 IN_PROGRESS. Supplemental UI READY remains P-UI-39 plus P-UI-40, P-UI-41, P-UI-47, P-UI-49 and P-UI-52. RQ48, RQ49, RQ88, RQ584, RQ590 and RQ591 stay DONE.
