# QDB12 evidence

Task ID: QDB12
Queue: `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
Date: 2026-10-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: `agent/qdb12-sef-xml-reconciliation` / none
Main commit SHA: `3b0edc2e69cb261be7a92c02543ff265c20226b4`
Main verification: `origin/main` resolved to the implementation SHA immediately after direct-main push; `git merge-base --is-ancestor HEAD origin/main` passed.
Evidence state: synchronized
Ownership transfer: none
Claim reservation: inspected `origin/main` `32072083cefe2e39e0805eec0695cd70c8f7b07b`; pushed claim `ef85f93e2bbea1bdc214cf7b7b6eaba09a56d16c`; Codex; `C:\Users\Ivan\source\repos\Trendplus2-qdb11`; `agent/qdb12-sef-xml-reconciliation`; no matching remote SEF/XML/reconciliation branch or PR, with PR #163 classified as unrelated.

## What was done

- Added an admin-only, rate-limited `POST /api/pilot/sef-xml/reconcile` endpoint behind the existing `SefXmlPilot:Enabled` server-side flag.
- Added a bounded, no-tracking catalog reader that projects only `Artikli.Id`, `PLU`, and `Naziv`; it neither loads nor changes cost, stock, formulas, journals, Access, or source documents.
- Added deterministic reconciliation. Only a unique ordinal-exact source `SupplierItemId` to `PLU` is `ExactIdMatch`; a manual choice is explicitly manual, names are review candidates, duplicates are ambiguous, and a truncated candidate set fails closed.
- Preserved source line order/IDs and source text. Physical quantity is always null in the response. Price comparison is explicitly unknown, rather than an invented supplier/cost discrepancy. Historical Access verification is explicitly unavailable without proven lineage.

## Files changed

- `Api/Services/SefXml/SefXmlReconciliationService.cs`
- `Api/Services/SefXml/EfSefXmlCatalogReader.cs`
- `Api/Endpoints/SefXmlReconciliationEndpoints.cs`
- `Api/Endpoints/SefXmlPreviewEndpoints.cs`
- `Api/Program.cs`
- `Api.Tests/SefXmlReconciliationServiceTests.cs`
- `Api.Tests/SefXmlReconciliationEndpointTests.cs`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- `.ai/runs/2026-10-16-QDB12-evidence.md`

## Validation run

- `git diff --check` — pass.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --filter 'FullyQualifiedName~SefXmlReconciliationServiceTests|FullyQualifiedName~SefXmlReconciliationEndpointTests|FullyQualifiedName~SefXmlInvoiceParserTests|FullyQualifiedName~SefXmlPreviewEndpointTests' --verbosity minimal` — pass, 33 passed / 0 failed / 0 skipped.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --verbosity minimal` — pass, 0 warnings / 0 errors.
- Focused static scope inspection of QDB12 feature files — pass: no write methods/query paths, Access import, inventory, journal, master-price, formula, migration, or raw source logging dependency.
- GitHub Actions `38869089375` for `3b0edc2e69cb261be7a92c02543ff265c20226b4` — queued at inspection; not treated as proof.

## Validation not run

- Full `Api.Tests` suite: not run; the mapped parser/reconciliation/API/security regression set and Release build ran.
- Real SEF XML, original Access receipt/cost lineage, and real operator validation: not supplied or authorized; synthetic evidence only.
- Browser/UI: not run; belongs to QDB13 after this close-out.

## Documentation impact

- Updated the canonical data-source connector queue with QDB12 DONE evidence and promoted QDB13 after the dependency cascade.

## What was missed

- Real source/financial truth and commercial value remain NOT VERIFIED.

## Risks

- Current exact-main Actions run `38869089375` was queued at inspection.
- `PLU` is treated only as a catalog external identifier for this bounded preview; no universal supplier/master identity claim is made.
- Access receipt validation remains unavailable until a business owner provides a provable original document/lineage/cost relationship.

## Post-close routing recovery

- Recovery base: `3b0edc2e69cb261be7a92c02543ff265c20226b4` on `origin/main` after delivery.
- Searched the active owner queue: QDB11 and QDB12 are DONE. QDB13’s parser/reconciliation/security dependencies are now satisfied, so it is promoted to READY. QDB14 remains WAITING on QDB13 and explicit owner approval plus an isolated test database.

## Next

- Claim QDB13 only after the QDB12 close-out commit is visible on refreshed `origin/main` and a fresh collision check confirms no active work owns `UnosRobe` or `Artikli` paths.
