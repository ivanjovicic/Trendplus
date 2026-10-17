# Documentation archive

> [!WARNING]
> **HISTORICAL ONLY.** Nothing in `docs/archive/` is current routing, current READY state, a runbook or release proof. Do not claim, execute or cite these files as current truth.

Current sources: `README.md` (developer/ops), `AGENTS.md` and `docs/ai/AGENT_START_HERE.md` (agents), `MASTER_ROADMAP.md` (routing and priorities), owner-queue headers (task status), `docs/ai/ENCODING_AND_TEXT_SAFETY.md` (Serbian text), `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md` and `Application/Analytics/AnalyticsMarginPolicy.cs` (business rules).

## `planning/` - planning snapshots

See [planning/README.md](planning/README.md). Those snapshots stay at their original paths; the index explains which ones are historical.

## `legacy-root/` - former repository-root notes (moved 2026-10-17)

Moved with `git mv` (history kept: `git log --follow docs/archive/legacy-root/<file>`). Each file still starts with its 2026-10-17 HISTORICAL banner. Links inside these files are relative to the old root location and are not maintained; the doc link check (`scripts/check-doc-links.mjs`) skips `docs/archive/**`.

Encoding: ten files were saved in a Windows code page and were re-encoded to UTF-8 on the move (marked *re-encoded*). They used only the bytes for `Š Ž š ž`, which are the same in CP1250 and CP1252, so the conversion is unambiguous; it was verified by a byte round-trip, and CRLF line endings were kept. Letters such as `č`, `ć`, `đ` and emoji had already been replaced by `?` in the original files; that earlier loss cannot be recovered and was not "fixed" by guessing. In `AUTOMATIC_SETUP_FINAL.md` and `NEON_SETUP.md` trailing spaces were removed so `git diff --check` stays clean (two-space Markdown hard breaks became the equivalent trailing `\`).

| File | Kind | Note |
|---|---|---|
| [START_HERE.md](legacy-root/START_HERE.md) | setup/fix note | re-encoded; current start: `README.md`, `docs/ai/AGENT_START_HERE.md` |
| [STARTUP_GUIDE.md](legacy-root/STARTUP_GUIDE.md) | setup note | current: `README.md` quickstart |
| [HITNA_POMOC.md](legacy-root/HITNA_POMOC.md) | troubleshooting note | re-encoded; current: `README.md` troubleshooting |
| [QUICK_FIX_README.md](legacy-root/QUICK_FIX_README.md) | fix note | re-encoded |
| [ONE_LINE_FIX.md](legacy-root/ONE_LINE_FIX.md) | fix note | re-encoded |
| [NEON_SETUP.md](legacy-root/NEON_SETUP.md) | setup note | re-encoded; never run production commands from it |
| [RABBITMQ_CONNECTION_FIX.md](legacy-root/RABBITMQ_CONNECTION_FIX.md) | fix note | |
| [SERBIAN_LETTERS_FIX.md](legacy-root/SERBIAN_LETTERS_FIX.md) | encoding fix note | re-encoded; current: `docs/ai/ENCODING_AND_TEXT_SAFETY.md` |
| [MANUAL_FIX_SERBIAN_LETTERS.md](legacy-root/MANUAL_FIX_SERBIAN_LETTERS.md) | encoding fix note | current: `docs/ai/ENCODING_AND_TEXT_SAFETY.md` |
| [AUTOMATIC_DATABASE_SETUP.md](legacy-root/AUTOMATIC_DATABASE_SETUP.md) | startup-migration note | re-encoded; current behaviour: `README.md` (deferred `DatabaseInitializer`) |
| [AUTOMATIC_SETUP_FINAL.md](legacy-root/AUTOMATIC_SETUP_FINAL.md) | startup-migration note | re-encoded |
| [ANALYTICS_VELICINA_BOJA_FEATURE.md](legacy-root/ANALYTICS_VELICINA_BOJA_FEATURE.md) | feature note | re-encoded |
| [IMAGE_ATTRIBUTION_COMPLETED.md](legacy-root/IMAGE_ATTRIBUTION_COMPLETED.md) | feature note | |
| [IMAGE_ATTRIBUTION_GUIDE.md](legacy-root/IMAGE_ATTRIBUTION_GUIDE.md) | feature note | |
| [LOGS_FEATURE.md](legacy-root/LOGS_FEATURE.md) | feature note | |
| [PERFORMANCE_TRACKING.md](legacy-root/PERFORMANCE_TRACKING.md) | feature note | re-encoded |
| [SUPPLIER_DECISION_REPORT.md](legacy-root/SUPPLIER_DECISION_REPORT.md) | feature report | |
| [PR3_ENGINEERING_REVIEW.md](legacy-root/PR3_ENGINEERING_REVIEW.md) | code review | |
| [Quest_DevEngineer_Experience_Final_v12.md](legacy-root/Quest_DevEngineer_Experience_Final_v12.md) | frozen runtime-scoring scope | |
| [MARGIN_FORENSIC_ANALYSIS.md](legacy-root/MARGIN_FORENSIC_ANALYSIS.md) | forensic report | current rules: `AnalyticsMarginPolicy.cs`, glossary |
| [MARGIN_FORENSIC_AUDIT_FINAL.md](legacy-root/MARGIN_FORENSIC_AUDIT_FINAL.md) | forensic report | referenced by DONE RQ411 (annotated) |
| [plan-addQueryStringToPerformanceLogs.prompt.md](legacy-root/plan-addQueryStringToPerformanceLogs.prompt.md) | IDE plan prompt | not a queue prompt |
| [plan-bilansStanja.prompt.md](legacy-root/plan-bilansStanja.prompt.md) | IDE plan prompt | not a queue prompt |
| [plan-dailyShiftSupplierSales.prompt.md](legacy-root/plan-dailyShiftSupplierSales.prompt.md) | IDE plan prompt | not a queue prompt |
| [plan-incrementalAccessImport.prompt.md](legacy-root/plan-incrementalAccessImport.prompt.md) | IDE plan prompt | superseded by STAB17 |
| [untitled-plan-accessImport.prompt.md](legacy-root/untitled-plan-accessImport.prompt.md) | IDE plan prompt | superseded by STAB17 |
| [untitled-plan-clientSideFallback.prompt.md](legacy-root/untitled-plan-clientSideFallback.prompt.md) | IDE plan prompt | not a queue prompt |
| [untitled-plan-codeChangesToEnforceCorrectAnalyticsDbTarget.prompt.md](legacy-root/untitled-plan-codeChangesToEnforceCorrectAnalyticsDbTarget.prompt.md) | IDE plan prompt | not a queue prompt |
| [untitled-plan-investigatePu02HyperError.prompt.md](legacy-root/untitled-plan-investigatePu02HyperError.prompt.md) | IDE plan prompt | not a queue prompt |
| [untitled-plan-pu02TimeoutMitigation.prompt.md](legacy-root/untitled-plan-pu02TimeoutMitigation.prompt.md) | IDE plan prompt | not a queue prompt |
| [untitled-plan-resolveAnalyticsRootCause.prompt.md](legacy-root/untitled-plan-resolveAnalyticsRootCause.prompt.md) | IDE plan prompt | not a queue prompt |
| [untitled-plan-softGrayTheme.prompt.md](legacy-root/untitled-plan-softGrayTheme.prompt.md) | IDE plan prompt | not a queue prompt |

## Marked historical in place (not moved)

These stay at their paths because current docs, queues or validators link to them. Each has a HISTORICAL banner pointing to the current source:

- `docs/Analytics/DAILY_SALES_UX_AUDIT.md`, `INVENTORY_UX_AUDIT.md`, `SECONDARY_ANALYTICS_SCREENS_AUDIT.md`, `ACTION_OUTCOME_DATA_SHAPE_AUDIT.md` (June 2026), `STOCK_COVER_SELL_THROUGH_AUDIT.md`, `KPI_METHODOLOGY_AUDIT.md` (May 2026);
- `docs/Frontend/ANALYTICS_NAVIGATION_AUDIT.md`;
- the planning snapshots listed in [planning/README.md](planning/README.md).
