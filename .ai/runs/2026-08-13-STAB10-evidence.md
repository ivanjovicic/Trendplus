Task ID: STAB10
Queue: docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Cursor
Model: Cursor Grok 4.6
Delivery target: main
Main commit SHA: pending
Main verification: not run

## What was done
- Gated access-import operational reads and cleanup inspection with the existing admin-key contract: missing key 401, wrong key 403, valid key keeps current behavior.
- Added `AdminAccessControl.RejectIfUnauthorized` and reused it on runtime-status, batches/jobs list and detail, logs, cleanup preview, archive list and archive export.
- Extended `AccessImportAdminAuthorizationTests` with route-matrix 401/403 coverage plus authorized runtime-status and empty batch-list proofs.
- Same-owner UI repair so Access Import no longer treats unauthorized batch history as an empty success; reads send `X-Admin-Key` after the existing prompt.

## Files changed
- Api/Endpoints/AdminAccessControl.cs
- Api/Endpoints/AccessImportEndpoints.cs
- Api.Tests/AccessImportAdminAuthorizationTests.cs
- Klijent/clientapp/src/services/accessImportApi.ts
- Klijent/clientapp/src/pages/AccessImportPage.tsx
- docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md
- docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-08-13-STAB10-evidence.md

## Validation run
- git diff --check -> pass (LF/CRLF warnings only)
- dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release -> pass
- dotnet test --filter FullyQualifiedName~AccessImportAdminAuthorizationTests -> pass (34/34)
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs -> pass
- node scripts/check-agent-instructions.mjs -> pass

## Validation not run
- npm run check:analytics-guardrails -> not run - Access Import is not an analytics KPI surface
- npm run build -> not run - header plumbing only; no shared analytics contract change
- full dotnet test -> not run - focused auth family was the named proof

## What was missed
- File-preview `/preview` and `/scope-options` remain public; they were outside STAB10 evidence.
- Batch history now loads after an admin-key prompt rather than on anonymous page mount.

## Risks
- Operators who previously viewed import history anonymously must enter the admin key via Osvezi or a write action.
- STAB11 still needs to gate logs/errors reads.

## Next
- STAB11 - Protect logs and errors operational read surfaces
