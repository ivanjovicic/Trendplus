# Analytics Audit Round 7

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Queue state before sync: `RQ167` remained the existing `READY` prompt. No active claim or existing prompt status was changed.

## Scope

This pass revisited the in-scope analytics decision surfaces and shared report/export adapters, with a narrow second review of Pilot Intake readiness and report preview lineage. The audit included `/analytics`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/actions`, `/analytics/decision-board`, `/analytics/data-quality`, `/analytics/reports`, sales and pre/post nivelacija surfaces, their nearest API/DTO/backend/query/storage/cache owners and focused tests. Forecast-only, Trend Models, Shopify, scrapers and unrelated test functionality were excluded.

## Confirmed New Findings

| Prompt | Surface | Confirmed defect | User risk |
|---|---|---|---|
| RQ246 | `/analytics/reports/pilot-intake` legacy browser preview | Missing period and generation metadata are replaced with the current browser time in the compatibility adapter. | A temporary or incomplete preview can look like a measured current-period report with false provenance. |
| RQ247 | Pilot Intake readiness card/report/export | Import status, import scope and readiness status are emitted as raw backend tokens in visible card/CSV/export projections. | Users see technical values such as `completed` or `global`, and future enum changes can leak opaque contract codes. |
| RQ248 | Pilot Intake readiness impact signal | A nullable/non-finite supplier-share value is treated as a number in readiness warning logic; null can suppress a limitation as zero, while `NaN`/`Infinity` can reach formatting. | Missing denominator/evidence can be mistaken for measured `0%`, or produce visibly invalid percentages and parity drift. |

## Evidence Map

- `Klijent/clientapp/src/pages/PilotIntakeReportPage.tsx:83-84` resolves the preview period to `null` when query and metadata do not contain dates.
- `PilotIntakeReportPage.tsx:102` then assigns `new Date().toISOString()` to missing `generatedAtUtc`, and `:105-108` assigns the current time to both missing period boundaries.
- `Klijent/clientapp/src/services/analyticsApi.ts:1088-1163` owns the durable `/api/analytics/reports/pilot-intake` client path; the affected branch instead reads `getBrowserPreviewPayload` from `analyticsTableState.ts`, so this is a local compatibility defect, not a backend endpoint-period duplicate.
- `Api/Endpoints/DataQualityEndpoints.cs:498-540` and `:644-685` show the backend source/query and Pilot Intake DTO builder: `Artikli`, `ProdajaStavke`, `ProdajaZaglavlja`, `Dobavljaci` and `StoresDim`, with refresh metadata from `AnalyticsRefreshStatusService`.
- `Infrastructure/DbContexts/TrendplusDbContext.cs` maps `Artikli`, sales headers/lines, suppliers and `DataImportBatches`; `Infrastructure/DbContexts/AnalyticsDbContext.cs` maps `StoresDim` and analytics facts. No new table or migration is implicated by RQ246-RQ248.
- `Klijent/clientapp/src/components/analytics/PilotImportReadinessCard.tsx:54-55` renders `lastImportStatus` and `lastImportScope` verbatim.
- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx:61-63`, `:112-114` and `:157-158` put raw readiness/import metadata into CSV/export rows and metadata.
- `Klijent/clientapp/src/utils/pilotImportReadiness.ts:80-96` normalizes import status for branching but only null-checks revenue coverage; supplier coverage is multiplied without null/finite validation at `:96`.
- `pilotImportReadiness.ts:113-118` uses the same value for warning decisions, while `PilotDataQualityIntakeReport.tsx:80-81` and `:131-132` format the impact independently, creating a card/readiness/export parity boundary.
- `Klijent/clientapp/src/types/analytics.ts:953-958` declares supplier coverage non-null even though the API response is consumed at a runtime boundary and partial payloads are possible.
- `Api/Endpoints/DataQualityEndpoints.cs:634` emits `0d` for an empty article denominator; `RQ169` already owns empty-intake readiness semantics and is not duplicated here.

## Existing Coverage and History Checked

- `PilotIntakeReportPage.spec.tsx` covers durable reload, missing explicit browser preview and stale preview keys, but not a legacy payload with missing period/generation metadata.
- `PilotImportReadinessCard.spec.tsx` covers normal `completed` display and failed readiness, but its assertion explicitly accepts raw status text and does not cover safe labels or export parity.
- `pilotImportReadiness.spec.ts` covers null revenue share and import status behavior, but not null/missing supplier share, true denominator-backed zero, `NaN` or `Infinity`.
- Git history was checked for `PilotIntakeReportPage.tsx`, `PilotImportReadinessCard.tsx`, `PilotDataQualityIntakeReport.tsx` and `pilotImportReadiness.ts`. Commit `8006a4a6f` introduced the current-time preview fallbacks, `ad1d86bfd` introduced direct import metadata rendering, and `db02cec7f` introduced the readiness card utility; later reliability commits did not close these exact boundaries.
- `RQ129` was checked and already closes the Decision Board sample-count-as-confidence defect; no duplicate prompt was created.
- `RQ169` owns empty-intake score/readiness semantics, `RQ170` owns backend invalid report periods, `RQ239` owns Executive fallback provenance, and `RQ245` owns Analytics Actions metadata labels. The new prompts are bounded to Pilot Intake preview/readiness projections.

## Screen Matrix for New Findings

| Stavka | Pilot Intake preview/report | Pilot Intake readiness/report/export |
|---|---|---|
| React page/component | `PilotIntakeReportPage.tsx`; `PilotDataQualityIntakeReport.tsx`; `PilotImportReadinessCard.tsx` | same components plus `pilotImportReadiness.ts` |
| API client | `analyticsApi.ts` for durable path; `analyticsTableState.ts` for legacy preview | `analyticsApi.ts` intake report; `exportApi.ts` for server document |
| Endpoint | Durable `/api/analytics/reports/pilot-intake`; legacy preview has no endpoint | `/api/analytics/reports/pilot-intake`; refresh status companion endpoint |
| DTO/response | `PilotDataQualityIntakeReport`; `ResolvedAnalyticsTablePayload`; durable report envelope | `PilotDataQualityIntakeReport`, `AnalyticsRefreshStatus`, export table/report payload |
| Backend service/query | `DataQualityEndpoints.BuildPilotDataQualityIntakeReportAsync` | same builder plus `AnalyticsRefreshStatusService` and readiness utility |
| SQL/EF/storage | `Artikli`, `ProdajaStavke`, `ProdajaZaglavlja`, `Dobavljaci`, `StoresDim`, `DataImportBatches` | same; no new storage owner identified |
| Cache/refresh | browser preview `analyticsTableState`; durable response and refresh status | `AnalyticsRefreshStatusService`; browser preview snapshot where legacy path is used |
| Existing tests | `PilotIntakeReportPage.spec.tsx` | `PilotImportReadinessCard.spec.tsx`, `pilotImportReadiness.spec.ts`, report component tests |
| Traženi/efektivni period | query values or missing in legacy payload | report DTO period; must not be replaced by current time |
| Posmatrani period | backend sales dates when present; absent in legacy preview | `loadedData.firstSaleDate/lastSaleDate`, distinct from requested period |
| Data scope | query/dataScope metadata; legacy preview may omit it | report `dataScope` and import scope, separate authorities |
| Vreme generisanja | explicit metadata only; currently synthetic in legacy branch | report generated time; missing must be unavailable |
| Poslednji uspešan refresh | explicit metadata/refresh status only | `lastRefreshAtUtc`; never query time or browser now |
| Freshness/data quality | metadata values may be absent in preview | backend `meta`/refresh status; frontend must only map |
| Empty/partial/error | valid empty preview, expired snapshot and backend error are separate states | empty/partial/fallback/error must remain distinct |
| Recommendation allowed | do not unlock from legacy preview fallback | consume backend state; no frontend readiness inference |
| Razlog ograničenja | missing period/provenance | raw status/scope or invalid numeric evidence must become clear limitation copy |

## Queue Result

- Added `RQ246-RQ248` as `WAITING` prompts with bounded ownership, failing-first tests, parity requirements and explicit dependencies.
- Preserved the single existing `READY` item `RQ167`; no claim, lock or unrelated queue status was modified.
- No production code or tests were changed; this round records new audit findings and implementation prompts only.

## Residual Risk

`RQ246-RQ248` are implementation prompts, not fixes. The affected behavior remains unproven until queue owners add failing-first regression tests and complete the frontend/report/export evidence described in each prompt.
