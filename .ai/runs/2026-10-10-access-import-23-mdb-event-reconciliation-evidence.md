Task ID: ACCESS-23-MDB-EVENT-RECON
Queue: direct-user-request; RQ606 registered as a distinct follow-up
Date: 2026-10-10
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending
Main verification: pending
Evidence state: synchronized after direct-main documentation delivery
Ownership transfer: none

## What was done
- Refreshed `main` and verified no competing active Access/MDB owner, lock, branch or PR for this new event-identity scope.
- Performed read-only PostgreSQL reconstruction for batch #23, including skipped coverage, receipt groups, journal expansion and transfer pair counts.
- Inspected current Access import mapping and identified the under-keyed transfer re-import deduplication path.
- Inspected local MDB candidates read-only and rejected them as non-batch-23 evidence.
- Registered RQ606 as a separate, WAITING import event-identity/reimport follow-up without duplicating RQ604 or RQ605.

## Files changed
- `docs/qa/ACCESS_IMPORT_23_MDB_EVENT_RECONCILIATION_2026-10-10.md`
- `tools/access-import-23-mdb-event-reconciliation.sql`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-10-access-import-23-mdb-event-reconciliation-evidence.md`

## Validation run
- Local PostgreSQL read-only SQL checks against `trendplus-postgres` / `trendplus`: pass.
- Candidate MDB ODBC reads: pass; candidates rejected as non-batch evidence.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName~AccessImportServiceTests|FullyQualifiedName~AccessImportReceiptDiagnosticsTests|FullyQualifiedName~AccessImportRetryAtomicityTests|FullyQualifiedName~AccessImportDnevnikTriggerTests|FullyQualifiedName~AccessImportCancellationTests"`: pass, 51/51.
- First equivalent test command with build: environment failure because an already-running .NET host held output DLL locks; retried once with `--no-build`, then passed.
- `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs`, and `git diff --check`: pass.

## Validation not run
- Exact-MDB row-by-row reconciliation: not run — batch-23 MDB was deleted after completion and no exact replacement is available.
- New real-MDB/PostgreSQL regression test: not run — no exact source fixture and no product code change authorized in this audit phase.
- Import/re-import execution: not run by explicit safety requirement.
- Production database or production MDB: not accessed or mutated.

## Documentation impact
- Added the durable read-only report and SQL evidence.
- Added RQ606 only for the independent transfer event-identity/reimport defect; RQ604 SalesLineFacts identity and RQ605 legacy fact/cache consumers remain separate owners.

## What was missed
- Per-row outcome for 244 skipped `tblProdaja` lines, 20 receipts and 168 skipped journal rows remains blocked on the exact source MDB.

## Risks
- Current transfer idempotency can suppress a legitimate equal-valued source event when source identity is present but the composite values collide.
- Operational `prodaja_stavke.source_row_id` is null for the legacy line table; analytical `SalesLineFacts.SourceLineId` is populated separately. A future fix must not conflate these contracts.

## Post-close routing recovery
- Not applicable to direct-user-request audit; RQ606 is registered WAITING and was not claimed.

## Next
- Restore/provide the exact batch-23 MDB, then execute RQ606's real MDB/PostgreSQL regression proof and complete the 244/20 row classification.
