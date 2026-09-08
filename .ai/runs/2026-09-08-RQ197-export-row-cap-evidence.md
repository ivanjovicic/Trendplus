Task ID: RQ197
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq197-export-row-cap-20260908 / local merge
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

- RQ197 was explicitly promoted and claimed after the queue reported no current READY prompt.
- Added configurable `Documents:MaxExportRows` with a default of 50,000 and a defensive upper bound of 1,000,000.
- Scheduled inventory exports now sort in the database, read only `MaxExportRows + 1` rows to detect truncation, and render no more than the configured cap.
- Added a truncation footer to the shared document payload and renderers so CSV, XLSX, PDF and HTML outputs clearly identify incomplete exports.
- Added focused tests for the row-limit contract and CSV/XLSX/PDF footer output. Existing untruncated output remains unchanged.

## Files changed

- Infrastructure/Configuration/DocumentExportOptions.cs
- Api/appsettings.json
- Api/appsettings.Development.json
- Application/Documents/Models/DocumentContracts.cs
- Infrastructure/Services/Inventory/InventoryReportDeliveryService.cs
- Infrastructure/Services/Documents/DocumentRenderer.cs
- Infrastructure/Services/Documents/DocumentTemplateService.cs
- Infrastructure/Services/Documents/Internal/DocumentPdfWriter.cs
- Api.Tests/DocumentRendererTests.cs
- MASTER_ROADMAP.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~DocumentRendererTests" --verbosity minimal` -> pass, 16 tests.
- `dotnet build Api/Api.csproj --configuration Release --no-restore --nologo --verbosity minimal` -> pass; existing unrelated nullable warnings remain.
- `git diff --check` -> pass before delivery.

## Validation not run

- Full frontend/backend test suites -> not run; this is a bounded backend/document-renderer change and focused owner proof plus Release build passed.
- Live large-catalog, database/provider, browser/deployed runtime and remote CI proof -> not run; unavailable or outside local validation.

## Documentation impact

- Updated `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` and `MASTER_ROADMAP.md` with promotion, completion, scope and delivery evidence.
- Added this durable run log using `.ai/RUN_LOG_TEMPLATE.md`.

## What was missed

- No live large-catalog or deployed export verification was performed.
- No production configuration change or migration was required.

## Risks

- Large exports up to the configured bound can still require substantial document storage and renderer time.
- The CSV truncation footer is a comment-style line and consumers parsing CSV should ignore comment-prefixed rows.

## Next

- Queue returns to no READY prompt. No follow-up is required for RQ197.
