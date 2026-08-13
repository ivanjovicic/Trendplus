Task ID: STAB12
Queue: docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Cursor Grok 4.6
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: skipped; user did not request commit or push

## What was done
- Stopped `DocumentUserContextAccessor` from treating unauthenticated `X-User-*` headers as identity or `AnalyticsExport`/`Admin` roles.
- Gated document generate/batch/print-preview/list/status and inventory export/print/run-now with `AdminAccessControl`.
- Kept signed download/print token validation; print URLs now include a token so they do not depend on spoofable headers.
- Added accessor/access-control unit tests and HTTP tests proving spoofed headers cannot generate without an admin key.
- Same-owner UI repair: export and inventory generate calls send `X-Admin-Key` after a prompt.

## Files changed
- Infrastructure/Services/Documents/DocumentSecurityServices.cs
- Api/Endpoints/AdminAccessControl.cs
- Api/Endpoints/DocumentEndpoints.cs
- Api/Endpoints/InventoryEndpoints.cs
- Api.Tests/DocumentSecurityTests.cs
- Api.Tests/DocumentExportAuthorizationTests.cs
- Klijent/clientapp/src/services/exportApi.ts
- Klijent/clientapp/src/services/analyticsApi.ts
- Klijent/clientapp/src/services/__tests__/exportApi.spec.ts
- docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md
- docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

## Validation run
- git diff --check: pass
- dotnet test --filter FullyQualifiedName~DocumentSecurityTests|DocumentExportAuthorizationTests --configuration Release: pass (16)
- npm run test -- --run src/services/__tests__/exportApi.spec.ts: pass (2)
- node scripts/check-prompt-queues.mjs --self-test: pass
- node scripts/check-prompt-queues.mjs: pass
- node scripts/check-planning-architecture.mjs: pass

## Validation not run
- Full `dotnet test` suite: STAB12 asked for DocumentSecurityTests plus focused new auth tests
- `npm run build`: bounded export auth-state branch only

## What was missed
- Inventory schedule create/update still stores anonymous user names; they do not grant generate privilege.

## Risks
- Operators must enter an admin key to export or print.
- A valid signed download URL remains usable without the admin key by design.

## Next
- QDB03 SQL Server read-only connector
