# Analytics screen bug review — 2026-10-13

Owner: analytics frontend trust surfaces (`Klijent/clientapp/src/pages`, `Klijent/clientapp/src/utils`).
Baseline: `origin/main` `a5604874` (PR #144 merge). Read-only static review; no runtime, provider or data change.

## Method

All 18 routes in `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts` were mapped to their page components in `App.tsx`. Instead of a line-by-line read of ~28k lines, the review searched every analytics page for the bug classes named in `AGENTS.md`:

- fake zero (`?? 0` / `|| 0` in front of a formatter that already has an unavailable fallback);
- error rendered as empty or as success (swallowed `catch`, `.catch(() => {})`);
- stale or out-of-order responses (request sequence/abort guards per loader);
- UTC/local calendar-day shifts (`toISOString().slice(0, 10)` on local `Date` values);
- frontend recreation of backend decisions.

Each candidate was confirmed by reading the surrounding code and its formatter/schema before it was registered. Candidates that could not be proven were dropped and are listed below.

## Confirmed findings (registered as queue prompts)

| Prompt | Screen | Finding | Severity |
|---|---|---|---|
| RQ651 | `/analytics/data-quality` and margin coverage text on supplier/shoe-type screens | Unknown master-data counts and unknown margin-coverage shares render as `0` / `0,0%` | P2 |
| RQ652 | `/analytics/actions` | Status KPI counts have no request-sequence guard, keep previous-filter values after a failed reload, and the outcome summary is not refreshed after an outcome is saved | P2 |
| RQ653 | `/analytics/pilot-readiness` | When the action-counts request fails but the outcome summary succeeds, the Actions card states "0 akcija" as fact | P2 |
| RQ654 | All screens using period presets | Presets use the UTC calendar day, so between 00:00 and 02:00 Europe/Belgrade time "last N days" ends on the previous local day | P3 |

### RQ651 detail — fake zero in data-quality and margin trust text

- `DataQualityPage.tsx` header ("Odvojeni signali"): `fmtNumber(health.missingSupplierArticleCount ?? 0, 0, "-")` and the same for `missingCostArticleCount` / `missingCategoryArticleCount`. The fields are optional in `analyticsResponseSchemas.ts` (`nonNegativeInteger.optional()`), and `fmtNumber` already returns the fallback for `null`/`undefined`; the `?? 0` makes the `"-"` fallback unreachable, so an absent count reads as "Bez dobavljača: 0". The KPI cards on the same page (`fmtNumber(health.missingSupplierArticleCount ?? health.orphanArticleCount)`) already handle absence correctly, so the page contradicts itself.
- `utils/marginQuality.ts`: `buildCoverageTooltip`, `buildRecommendationCaveat` and `buildMarginDetailNote` pass `historicalPct ?? 0`, `estimatedPct ?? 0`, `noCostPct ?? 0` to `fmtPct`, which also already handles `null`. Unknown coverage therefore reads as e.g. "Bez troška: 0,0%" — a false statement that all revenue has cost evidence. Callers: `SupplierSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`.

### RQ652 detail — stale counts and outcome summary on the actions page

- `AnalyticsActionsPage.tsx` `loadItems` and `loadOutcomeSummary` use `itemsRequestSeqRef` / `outcomeSummaryRequestSeqRef`; `loadCounts` has neither, so an older counts response can overwrite newer filters.
- `loadCounts` swallows every error (`// non-critical`) and leaves the previous `counts` state rendered in the KPI cards (`counts.new`, `counts.accepted`, …, `counts.p1Open`), which then describe the previous filter set without any indication.
- `submitOutcomeModal` updates the row and detail but never calls `loadOutcomeSummary`, so the outcome summary (realization, measured sample) stays stale after the user records an outcome on the same page.

### RQ653 detail — pilot readiness partial failure

`PilotReadinessPage.tsx` loads `getAnalyticsActionCounts()` and `getAnalyticsActionOutcomeSummary()` independently via `Promise.allSettled`. `buildActionsCard` only returns `unknown` when **both** are null. With counts null and summary present, `newCount … doneCount` default to `0`, and if `sampleSize > 0` the card reports "Ukupno 0 akcija, otvorenih 0, završenih 0" as an established fact.

### RQ654 detail — preset period day boundary

`utils/analyticsPeriodPresets.ts` `getAnalyticsPeriodPresetRange` uses `setUTCDate`/`setUTCHours` and `toISOString().slice(0, 10)`. For `now = 2026-10-14T00:30+02:00` (= `2026-10-13T22:30Z`), "Poslednjih 30 dana" ends on `2026-10-13`, i.e. local "today" is excluded for two hours after local midnight (one hour in winter). The only spec (`utils/__tests__/analyticsPeriodPresets.spec.ts`) uses noon UTC and cannot catch this. Used by `AnalyticsDashboard.tsx`, `InventoryPage.tsx`, `AnalyticsDetails.tsx` and `analyticsFormatters.ts`. Severity is P3 because the window is short, and the fix must first confirm the backend business-day convention (sales timestamps may be stored as local wall-clock time).

## Observed, not registered

- `computePriorityScore` in `ExecutiveDecisionBoardPage.tsx` ranks cards on the frontend. Already owned by `docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md`; not duplicated.
- `InsightStudioPage.tsx` swallows nine loader errors with `.catch(() => {})` and keeps the previous period's data. The route is behind `ExperimentalInsightStudioRoute`; recorded here for whoever promotes the experiment, not queued.
- `AnalyticsActionsPage` measured-impact input: a Serbian thousands-separator misparse (`"12.500"` → 12.5) was suspected from `Number(raw.replace(",", "."))`, but the field is `<input type="number">`, so the browser supplies a canonical number string. Not proven; dropped.
- `/api/analytics/data-quality/health` exclusive `toDate`: the client adds one day and the backend `ResolveHealthWindow` treats `toDate` as exclusive. Consistent; no bug.
- Color/ShoeType/DailySales loaders: guarded by request sequence and existing period-truth specs; DailySales clears data on failure. No finding.

## Limits

Static review only; no browser run against live data. Findings are code-path proofs. Each prompt requires a failing-first regression test before the fix.
