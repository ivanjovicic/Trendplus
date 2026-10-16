Task ID: QDB11
Queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-10-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: agent/qdb11-sef-xml-preview / none
Main commit SHA: 600a0a4c82e8a38aef0c69cfcfe5bc25c2cebfd5
Main verification: passed - fresh origin/main resolved to 600a0a4c82e8a38aef0c69cfcfe5bc25c2cebfd5 and contains the implementation SHA.
Evidence state: synchronized
Ownership transfer: none
Claim reservation: inspected origin/main 69abc25db79e07444bb241f3cd7ad5fb0013f458; pushed claim 84a0fd96c206ff405b7f2d1edaa251450cdbcc80; owner Codex; worktree C:\Users\Ivan\source\repos\Trendplus2-qdb11; branch agent/qdb11-sef-xml-preview; remote branch/PR query found no matching SEF/XML/QDB work.

## What was done

- Added a bounded UBL 2.x Invoice parser and typed, request-scoped read-only preview DTO.
- Added an authenticated, strict-rate-limited pilot endpoint at `POST /api/pilot/sef-xml/preview`; its feature flag is off unless `SefXmlPilot:Enabled=true`.
- Rejected unsafe/incompatible inputs, including DTD/XXE, CreditNote, wrong namespaces/types, malformed XML, PDF/ZIP content, excessive input, element depth and invoice-line limits.
- Kept invoice quantities separate from physical receipt: `PhysicallyReceivedQuantity` is always unknown in this preview.

## Files changed

- Api/Services/SefXml/SefXmlInvoiceParser.cs
- Api/Endpoints/SefXmlPreviewEndpoints.cs
- Api/Program.cs
- Api.Tests/SefXmlInvoiceParserTests.cs
- Api.Tests/SefXmlPreviewEndpointTests.cs
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- .ai/runs/2026-10-16-QDB11-evidence.md

## Validation run

- `git diff --check` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --filter 'FullyQualifiedName~SefXmlInvoiceParserTests|FullyQualifiedName~SefXmlPreviewEndpointTests|FullyQualifiedName~EndpointNameUniquenessTests' --no-restore --verbosity minimal` -> pass; 22 passed, 0 failed, 0 skipped.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --verbosity minimal` -> pass; 0 errors, 262 pre-existing repository warnings.
- Static changed-file review -> pass; new code has no Access import, inventory, journal, persistence or logging dependency.
- Delivery verification -> pass; implementation was rebased without conflict over unrelated `3e775e9b`/`1e171959` documentation commits and pushed as `600a0a4c` to `main`.

## Validation not run

- Full `Api.Tests` suite -> not run; the focused parser/API/security suite and release build were the mapped proof.
- Real SEF XML compatibility and owner/operator validation -> not run; no authorized source document was supplied.
- Browser/UI test -> not run; QDB13 is a separate waiting task at this closure.

## Documentation impact

- Updated the owning QDB queue to close QDB11 and promote QDB12.
- Added this durable QDB11 evidence record.

## What was missed

- Authentic invoice compatibility and operator acceptance remain NOT VERIFIED.

## Risks

- GitHub Actions run `38836814732` for `600a0a4c` was `queued` when inspected.
- Unseen permitted UBL variants remain a compatibility risk until tested with an authorized sample; no real document was logged, stored or committed.

## Post-close routing recovery

- Recovery base origin/main SHA: `600a0a4c82e8a38aef0c69cfcfe5bc25c2cebfd5`.
- Active owner queue/addendum files scanned: `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`, `MASTER_ROADMAP.md`, `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`.
- Completed/changed dependency IDs searched: QDB11, QDB12, QDB13 and QDB14.
- Newly satisfied dependencies: QDB11 is delivered on main with the parser DTO/test contract required by QDB12.
- Promoted successor: QDB12. QDB13 remains WAITING on QDB12.

## Next

- QDB12 - deterministic read-only SEF XML reconciliation against an authorized catalogue scope.
