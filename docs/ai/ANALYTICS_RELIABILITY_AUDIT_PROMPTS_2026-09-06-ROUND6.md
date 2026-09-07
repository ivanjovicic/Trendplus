# Analytics Audit Round 6

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Queue state before sync: `RQ167` remained the existing `READY` prompt. No active claim was changed.

## Scope

This pass revisited the in-scope analytics routes and their shared projections, with emphasis on surfaces not isolated by the previous five rounds: `/analytics/actions`, `/analytics/data-quality`, supplier footwear pre/post, product decisions, decision board, reports/exports and the shared table/print adapters. Forecast-only, Trend Models, Shopify, scrapers and unrelated test functionality were excluded.

## Confirmed New Findings

| Prompt | Surface | Confirmed defect | User risk |
|---|---|---|---|
| RQ243 | Supplier footwear pre/post and its export metadata | Schema/database fallback creates a non-null default data-quality DTO. The page coalesces its counters/share to zero and computes a trust summary from those values. | A fallback or incomplete quality snapshot can look like measured numeric evidence and can pollute table/export metadata with false zero counts. |
| RQ244 | `/analytics/actions` outcome detail | Read-side evidence helper treats an outcome measurement timestamp as proof when no evidence source, ledger evidence or measured value exists. | A legacy/partial qualitative outcome can be presented as confirmed measured evidence, overstating action-result reliability. |
| RQ245 | `/analytics/actions` metadata details and projections | Unknown freshness, confidence, action-code, source-module and recommendation-type values fall back to backend strings with underscores replaced. | New or malformed backend tokens can be exposed directly to users instead of a clear unknown state. |

## Evidence Map

- `Api/Models/VendorSalesNivelacijaModels.cs:112-124` defines all supplier footwear data-quality fields as non-null numerics.
- `Api/Endpoints/AllEndpoints.cs:3237-3249`, `:4098-4112` and `:6740-6765` show schema/database fallback construction; `DataQuality` is a default zero object.
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx:795-830` converts that object to a numeric trust classification.
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx:1037-1060` sends the same values to shared toolbar/export metadata.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx:416-464` contains timestamp-as-proof logic and the resulting user-facing outcome message.
- `Api/Endpoints/AnalyticsActionsEndpoints.cs:326-333` protects new authoritative writes with `evidenceSource`, but read compatibility still accepts legacy/partial payloads.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx:359-387` contains raw fallback formatting for unknown action metadata.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx:1388-1428` renders the affected freshness/confidence/source values in visible evidence details.

## Existing Coverage Checked

- `SupplierFootwearAnalyticsPage.spec.tsx` covers complete and insufficient pre/post fixtures and some numeric parity, but not fallback/partial data-quality evidence or trust/export parity for default DTO values.
- `AnalyticsActionsPage.spec.tsx` covers missing outcome without timestamp and unknown warning-code text, but not timestamp-only evidence or unknown freshness/confidence/action metadata values.
- Backend action tests and the action-outcome addendum (`RQ82-RQ87`, `RQ93`) were checked. They already own denominator, qualitative outcome, write-side evidence and not-measured snapshot semantics; the new RQ244 is defensive read-side legacy/partial presentation only.
- PDC backend explicitly documents count KPI scope as returned/top rows and money totals as analyzed rows (`CachedAnalyticsEndpoints.cs:5940-5958`), so no duplicate PDC-count prompt was created.
- Supplier decision top-8 concentration is explicitly described as a top-priority-set view, so it was not reported as a new all-supplier denominator defect.
- Data Quality health status, Dashboard nullable inventory ratios, Daily Sales concentration and invalid Dashboard dates remain owned by existing RQ75/RQ154/RQ204/RQ240-RQ242 families.
- Recent Git history was checked for `AnalyticsActionsPage.tsx`, `DataQualityPage.tsx`, `ProductDecisionCenterPage.tsx`, `ProdajaPrePostNivelacijePage.tsx` and their backend/API owners. Prior fixes were used to avoid reopening completed owners.

## Non-Duplicates

- `RQ156`/`RQ182` own pre/post coverage/unknown aggregate semantics; `RQ243` owns the separate data-quality summary/fallback DTO state.
- `RQ179` owns supplier footwear freshness provenance; `RQ180` owns frontend denominator reconstruction; neither owns quality-summary zero coercion.
- `RQ82-RQ86` own action denominator and write-side evidence rules; `RQ244` only hardens read-side timestamp-only legacy/partial payloads.
- `RQ178` owns Inventory raw alert/reason code presentation; `RQ245` owns Analytics Actions metadata labels.

## Queue Result

- Added `RQ243-RQ245` as `WAITING` prompts with bounded ownership, failing-first tests, acceptance criteria and dependencies.
- Preserved `RQ167 READY` and did not change task locks or active claims.
- No production code or tests were changed; this round only records audit findings and implementation prompts.

## Validation and Delivery Truth

- `node scripts/check-prompt-queues.mjs` and `git diff --check` are required after the edits.
- Runtime tests, analytics guardrails, backend/frontend builds, live schema/404/refresh checks and browser console/theme/chart smoke are not run by this documentation-only round.
- No commit or push is performed unless explicitly requested; the new prompt documents are local changes until delivery is requested.

## Residual Risk

`RQ243-RQ245` are implementation prompts, not fixes. The affected behavior remains unproven until queue owners add failing-first regression tests and complete the backend/frontend parity and live evidence required by each prompt.
