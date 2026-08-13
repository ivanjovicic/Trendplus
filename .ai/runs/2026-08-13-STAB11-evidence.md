Task ID: STAB11
Queue: docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Cursor Grok 4.6
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: skipped; user did not request commit or push

## What was done
- Gated `GET /errors`, `GET /api/logs`, and `GET /api/logs/{id}` with shared `AdminAccessControl.RejectIfUnauthorized`.
- Extracted `MapLogsAndErrorsReadEndpoints` so focused tests can host only those routes.
- Added `LogsOperationalReadsAuthorizationTests` for missing key (401), wrong key (403), and valid key (200/404) without calling the store on reject.
- Logs page/API now send `X-Admin-Key` and map 401/403 to explicit unauthorized messages. Clear-log auth is unchanged.
- Promoted `STAB12` to READY.

## Files changed
- Api/Endpoints/AllEndpoints.cs
- Api.Tests/LogsOperationalReadsAuthorizationTests.cs
- Klijent/clientapp/src/services/logsApi.ts
- Klijent/clientapp/src/services/__tests__/logsApi.spec.ts
- Klijent/clientapp/src/pages/LogsPage.tsx
- docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md
- docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md

## Validation run
- git diff --check: pass
- dotnet test .\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~LogsOperationalReadsAuthorizationTests --configuration Release: pass (11)
- npm run test -- --run src/services/__tests__/logsApi.spec.ts: pass (4)
- node scripts/check-prompt-queues.mjs --self-test: pass
- node scripts/check-prompt-queues.mjs: pass
- node scripts/check-planning-architecture.mjs: pass

## Validation not run
- Full `dotnet test` suite: STAB11 asked for focused logs auth tests
- `npm run build`: only a bounded logs auth-state branch was added
- Duplicate `Trendplus2/Program.cs` logs routes: out of STAB11 scope (`AllEndpoints.cs`)

## What was missed
- Legacy `Trendplus2/Program.cs` still maps public `/errors` and `/api/logs` if that host is used.

## Risks
- Operators must enter an admin key to view logs.
- If the unused Trendplus2 host is still deployed anywhere, those duplicate routes remain public.

## Next
- STAB12 document/export header trust boundary
