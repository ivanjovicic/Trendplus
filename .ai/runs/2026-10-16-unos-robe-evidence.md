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
- Ownership-transfer validation on current code tree `b958c96ed05406fef18fccb2c6dd8d9c0fdf5712` after merging refreshed `origin/main` `0e40de54e710993c58d645b81817123e6e9c000c`: `GoodsReceiptEndpointAuthorizationTests` 6/6 passed; focused receipt UI specs 3 files / 13 tests passed; `git diff --check` passed. `docker info` did not return and was interrupted; no local PostgreSQL run was claimed for this merged tree.
- Pre-refresh exact PR-head CI at `879ccc22a02c3722fb172a9dffd6885f5f80693b`: backend/PostgreSQL workflow `38875299959` and Analytics Quality Gates workflow `38875300226` succeeded. These are superseded for merge acceptance by the required rerun on the current-main-integrated PR head.

## Validation limits and remote results
- Real operator or production-browser session -> not run. A **synthetic, network-intercepted desktop/mobile browser fixture** ran successfully in workflow `38875300226`; it does not access a live database and is not a real receipt demonstration.
- Local full `Api.Tests` -> the mandatory all-suite Testcontainers attempt hit Docker API/Testcontainers startup timeouts; a broad non-integration attempt was interrupted while silent. This local limitation was superseded by the remote full suite below.
- Latest pre-refresh PR-head CI on `879ccc22a02c3722fb172a9dffd6885f5f80693b` -> backend run `38875299959`, frontend/analytics and synthetic goods-receipt browser workflow `38875300226`, RQ453 certification, SDK pin, POS UI audit/build, and Vercel checks succeeded. The current-main-integrated head still requires exact-head CI before merge.
- Current-main-integrated local head `b958c96ed05406fef18fccb2c6dd8d9c0fdf5712` has not yet been pushed; no current-head remote check has been inferred from earlier runs.

## Documentation impact
- Added `docs/operations/GOODS_RECEIPT_RUNBOOK.md`, including retry, partial-delivery, projection failure, Access collision, and correction escalation guidance.
- Documented that shared admin-key writes are attributed as `admin-api-key`; they do not identify an individual operator.

## What was missed
- No real invoice, production Access file, live operator credentials or stock mutation was used. No live operator/browser demonstration is claimed.
- Public-environment provisioning and explicit owner authorization to set `GoodsReceipts:WritesEnabled=true` remain outside this merge task and were not performed.

## Risks
- The code merge does not authorize actual supplier receipt use. Keep `GoodsReceipts:WritesEnabled` OFF until a separate owner-authorized environment review and operator provisioning; synthetic CI is not proof of live behavior or consent to post stock.
- Access article snapshot import takes ordered row locks on existing articles for its transaction; a long Access batch can delay receipt writes.
- Operational PostgreSQL and analytics PostgreSQL are separate. Receipt commit is atomic in the operational database; analytics movement projection is durable, idempotent, and retryable, not cross-database ACID.

## Post-close routing recovery
- not applicable for direct-user-request; no queue task was claimed or closed.

## Next
- Push the ownership-evidence commit and current-main integration merge, wait for exact-head PostgreSQL/backend plus frontend/synthetic-browser CI, then mark PR #184 ready and merge if all required checks pass. Do not enable writes or create a real receipt.
