Task ID: P0-UNOS-ROBE
Queue: direct-user-request
Date: 2026-10-16
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/p0-unos-robe-20261016; draft PR #184 https://github.com/ivanjovicic/Trendplus/pull/184
Main commit SHA: pending
Main verification: pending; refreshed origin/main is 0e40de54e710993c58d645b81817123e6e9c000c and does not yet contain PR #184
Evidence state: pending final review, current-main integration validation and delivery
Ownership transfer: Ivan Jovicic (PR author ivanjovicic) -> Codex (current task); authority is the user's 2026-10-16 instruction to finish and merge PR #184; recorded before source-path edits in this run log
Claim reservation: direct-user-request/not applicable

## What was done
- Ownership transfer recorded before any source-path edits: the user explicitly reassigned PR #184 completion in this task. The existing PR branch/worktree `codex/p0-unos-robe-20261016` is preserved; no branch was deleted or rewritten. Review/revalidation will use current `origin/main` `0e40de54e710993c58d645b81817123e6e9c000c` and the PR's exact head.
- Implementation commits on the branch: `3e510f080017d244db13c7ac6b7fbe671b9b64d1` (receipt workflow) and `315ac6f37e727cdb44ac2abcb14c650ddf334d16` (stale search/accessibility regression fix), based on refreshed `origin/main` `ef85f93e2bbea1bdc214cf7b7b6eaba09a56d16c`.
- Replaced console-only receipt handling with an admin-protected, server-side receipt transaction that stores a receipt header, immutable line snapshots, and one linked `Ulaz robe` movement per line.
- Existing article stock is incremented atomically while its row is locked. New articles get PostgreSQL-generated IDs; receipt prices remain line history and do not overwrite article master prices.
- A unique request UUID plus canonical payload hash returns the committed receipt on an identical retry and rejects key reuse with a different payload. Similar supplier/document/store receipts require explicit confirmation so partial deliveries remain possible.
- Added an idempotent worker projection into analytics, post-commit best-effort cache invalidation, a receipt history view, and a runbook.
- Made Access article imports preserve app-origin stock movements, fail closed on a source ID colliding with a non-Access target article, serialize article snapshot import with receipt writes, and advance identity sequences monotonically.
- Added a regression test for stale article search results. It failed before the UI fix and passed after clearing old results on query change. Search choices now use normal keyboard-operable buttons.
- Added an idempotent compatibility migration for `Artikli.ImagePath`, discovered missing from the clean migrated PostgreSQL schema although the EF model/importer requires it.
- The repository contains a caller for PostgreSQL `unos_artikla`, but no function definition or migration; the receipt implementation therefore uses the verified article table contract directly.
- No production database, inventory, price, or Access data was modified.

## Files changed
- `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/GoodsReceiptEndpoints.cs`, `Api/Program.cs`
- `Api/Services/AccessArticleStockOverlay.cs`, `Api/Services/AccessImportService.cs`, `Api/Services/GoodsReceiptService.cs`
- `Infrastructure/Migrations/20261016100000_AddGoodsReceiptTransactions.cs`, `Infrastructure/Migrations/20261016103000_EnsureArticleImagePathColumn.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs`, `Api.Tests/GoodsReceiptEndpointAuthorizationTests.cs`, `Api.Tests/GoodsReceiptPostgresIntegrationTests.cs`
- `Klijent/clientapp/src/components/GoodsReceiptHistoryPanel.tsx`, `Klijent/clientapp/src/components/UnosArtikalaForm.tsx`, `Klijent/clientapp/src/components/UnosArtikalaForm.spec.tsx`, `Klijent/clientapp/src/components/UnosRobeForm.tsx`, `Klijent/clientapp/src/components/UnosRobeForm.spec.tsx`, `Klijent/clientapp/src/pages/ArtikliPage.tsx`, `Klijent/clientapp/src/pages/UnosRobePage.tsx`
- `Workers/GoodsReceiptMovementProjector.cs`, `Workers/SyncWorker.cs`
- `docs/operations/GOODS_RECEIPT_RUNBOOK.md`

## Validation run
- `npm run test -- --run src/components/UnosArtikalaForm.spec.tsx` -> pass, 9 tests.
- `npm run test -- --run src/components/UnosArtikalaForm.spec.tsx -t 'hides old article results'` -> red before UI fix (1 failed, 8 filtered); pass after fix (1 passed, 8 filtered).
- `npm run test -- --run` -> pass, 247 files / 2069 tests, 0 skipped.
- `npm run check:analytics-guardrails` -> pass; encoding and baseline self-test pass, 36 existing baseline findings and 0 removed, typecheck `tsc -b` pass.
- `npm run build` -> pass; Vite transformed 2735 modules.
- `dotnet build Api/Api.csproj --no-restore -clp:ErrorsOnly` -> pass, 0 errors (176 existing warnings on the refreshed main base).
- `dotnet build Workers/Workers.csproj --no-restore -clp:ErrorsOnly` -> pass, 0 warnings/errors.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoodsReceiptPostgresIntegrationTests'` -> pass on real Testcontainers PostgreSQL, 10 passed / 0 failed / 0 skipped. Covers mixed receipt, movement projection retry, identical concurrent key, partial shipment warning, concurrent stock updates, full rollback on line-two failure, invalid references, Access-ID collision, Access snapshot serialization, and monotonic sequence repair.
- Focused backend suites -> pass: `GoodsReceiptEndpointAuthorizationTests` 2/2; `AccessImportServiceTests` 42/42; `TransferServiceTests` + `DnevnikPromenaReadServiceTests` + `InventorySnapshotContractTests` 48/48; cache failure isolation 1/1.
- `git diff --check` -> pass.

## Validation limits and remote results
- Browser end-to-end -> `BROWSER NOT RUN`; no isolated browser deployment/environment was available. The production URL was not used for a write-flow rehearsal.
- Local full `Api.Tests` -> the mandatory all-suite Testcontainers attempt hit Docker API/Testcontainers startup timeouts; a broad non-integration attempt was interrupted while silent. This local limitation was superseded by the remote full suite below.
- GitHub PR CI on implementation head `84cec623e7b01b6cc1758f0fcd5a405538a7a1a8` -> all checks passed: backend run `38859376447` (2,173 passed, 0 failed, 43 known classified skips; 13 classified as isolated PostgreSQL certifiable and 30 legacy/superseded, 0 uncovered/new skips), frontend run `38859376334` (tests, guardrails, build, fixture browser/CSV parity and bundle gates), RQ453 certification, SDK pin, POS UI build/dependency audit, Vercel deployment and preview comments. Backend full suite ran with `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`; run duration 8m04s. This run-log synchronization changes only evidence text; recheck the resulting PR head before any merge decision.

## Documentation impact
- Added `docs/operations/GOODS_RECEIPT_RUNBOOK.md`, including retry, partial-delivery, projection failure, Access collision, and correction escalation guidance.
- Documented that shared admin-key writes are attributed as `admin-api-key`; they do not identify an individual operator.

## What was missed
- No correction/storno operation is provided. Incorrect confirmed receipts require controlled administrator correction until a linked, auditable reversal flow is implemented.
- The shared admin-key path does not provide person-level authorship. An authenticated named administrator is recorded when available; shared-key-only use cannot satisfy verified individual audit attribution.
- Browser interaction across desktop/mobile widths and SPA back/forward was not verified end to end.

## Risks
- Do not use for actual supplier receipts until person-level actor identity and a traceable correction/reversal workflow are available and the complete browser flow is verified in an isolated environment. Backend transaction and idempotency behavior passed PostgreSQL tests, but that is not sufficient production-use proof.
- Access article snapshot import takes ordered row locks on existing articles for its transaction; a long Access batch can delay receipt writes.
- Operational PostgreSQL and analytics PostgreSQL are separate. Receipt commit is atomic in the operational database; analytics movement projection is durable, idempotent, and retryable, not cross-database ACID.

## Post-close routing recovery
- not applicable for direct-user-request; no queue task was claimed or closed.

## Next
- PR #184 head `84cec623e7b01b6cc1758f0fcd5a405538a7a1a8` passed the listed remote CI checks. Keep it open: individual actor identity, linked correction/reversal workflow, and isolated end-to-end browser proof remain required before merge or real-use approval.
