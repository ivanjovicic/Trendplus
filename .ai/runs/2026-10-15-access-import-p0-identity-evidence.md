Task ID: P0-access-import-identity
Queue: direct-user-request
Date: 2026-10-15
Agent/tool: Cursor
Delivery target: main
Working branch / PR: codex/access-sale-line-identity / PR #163 (draft, PARTIAL, do not merge)
Main commit SHA: not delivered
Main verification: refreshed `origin/main` is `e8730865` (includes classifier `20eb9fb5` / PR #178). Deployed Render `trendplus-api` reported `commitSha=f9faf02542aed3ee191c556fcd66c8eed9da4fb0` at `buildTimeUtc=2026-10-15T14:07:39Z`. This branch is rebased onto `e8730865` by merge and is not on `main`.
Evidence state: PARTIAL
Incident classification: ROOT_CAUSE_UNPROVEN
Ownership transfer: none
Claim reservation: direct-user-request/not applicable

## Reconciliation against origin/main e8730865
- Production and GitHub do not contain a server log line with BatchId, SQLSTATE, TableName, ConstraintName, import step and source identity for the reported `duplicate_sale_header_identity` failure. The public incident text is only the pre-classifier message. No Render API credential was available. Deployed SHA `f9faf025` still emits the generic message; its structured log would have included Constraint, but that log was not retrieved.
- `tools/access-sale-header-identity-triage.sql` was executed read-only against local database `trendplus` on `127.0.0.1:5434`. `pg_indexes` returned no sale-header unique indexes and the next statement failed with `relation "prodaja_zaglavlje" does not exist`. That database is empty. It is not an authorized copy of the failed import. No production rows were read or changed. No sequence was reset. No constraint was disabled.
- PR #163 conflict with the merged classifier was resolved without returning to Access numeric IDs as PostgreSQL sale-header or sale-line IDs. Header and line 23505 failures keep distinct sanitized classes: `target_primary_key_collision`, `access_source_lineage_collision`, `unclassified_unique_constraint`. ConstraintName stays in the server log. The operator message does not include receipt numbers or PostgreSQL Detail. Cursor commit remains on the success path only.
- Disposable PostgreSQL proof, `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`, local server port 5434, not Docker: `AccessImportForeignKeyGuardTests` 18 passed / 0 failed / 0 skipped, including `MixedPopulationSqlOracle_DoesNotRebindHistoricalReceipts`. That test executes the triage script and checks populations `existing` (POS id 7), legacy access header 40 with NULL `source_row_id`, and a generated Access header whose source row 7 numerically overlaps POS id 7. POS and legacy lines stay on their original headers. Repeated import does not add a second header. `SalesFacts` totals are 50 / 10 / 396 and `SalesLineFacts` for source line 11 use the generated sale id. `AccessImportRetryAtomicityTests` 1 passed / 0 skipped.
- Issue #174 entities (`Artikli`, `TipObuce`, `Dobavljaci`, `Sezona`, `DnevnikPromena`, returns, synthetic movements) are not remapped here.

## Still required before merge
1. The actual failed-import server log line: BatchId, SQLSTATE, TableName, ConstraintName, import step, and HeaderSourceSamples (source table key and source row id). Redact receipt numbers and customer fields.
2. An authorized SELECT-only copy of the database that owns that batch, so the triage script can return real index definitions, lineage completeness, duplicate source tuples and numeric source/target overlaps.
3. An operator-owned immutable Access source-database identifier. Filename, storage key and `(source_table_key, source_row_id)` cannot separate two Access databases that reuse the same IDs.
4. A reviewed reconciliation for batch #23's 67,092 `prodaja_stavke` rows with NULL `source_row_id`. No receipt, date or price matching.
5. The same historical oracle on that copy for `SalesFacts` and `SalesLineFacts` populations `all` / `imported` / `existing`, plus issue #174 dependent foreign keys.
6. Exact final SHA CI after those inputs exist. This local 18/18 result is not that gate.

## CI repair addendum — 2026-10-15
- Resumed the existing direct-user P0 draft after `Analytics Tests & Data Integrity` failed on exact PR head `ff58cfaaa9aa55d7360dea7a67ccf78d84d8aa8e`. The sole failure was `MixedPopulationSqlOracle_DoesNotRebindHistoricalReceipts`: expected `0`, actual `2`.
- Cause: the independent `SalesLineFacts` oracle joined only Access lineage (`source_table_key`, `source_row_id`). Valid native POS and legacy fact rows deliberately use `trendplus.prodaja_stavke` plus the target `prodaja_stavke.id`, so the oracle falsely reported them as missing source rows.
- Repair: the test-only oracle now resolves either supported identity form. The negative control still changes an Access-derived fact and requires exactly one detected reparenting. No import, mapping, formula, schema, production data, or cursor behavior changed.
- Local required PostgreSQL proof used Testcontainers with `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`: focused oracle test `1 passed / 0 failed / 0 skipped`; whole `AccessImportForeignKeyGuardTests` group `20 passed / 0 failed / 0 skipped` (TRX result). `dotnet build Api.Tests/Api.Tests.csproj --no-restore --disable-build-servers -m:1` also passed with `0` warnings and `0` errors.
- Delivery state at this addendum: the correction is awaiting a normal non-force push to PR #163 and a fresh exact-head backend CI result. PR #163 remains draft/PARTIAL and must not merge: the stable source-database namespace, authorized batch #23 reconciliation, full all/imported/existing SQL oracle, and remaining failure/foreign-key matrix are still unmet.

## Earlier record

## What was done
- Confirmed the supplied incident text does not include the PostgreSQL exception chain. Local incident logs available to this run also had no matching SQLSTATE 23505, so the exact historical constraint, batch, source namespace and IDs remain unclassified.
- Confirmed the live async header paths used Access IDs as PostgreSQL target IDs and rejected a new Access header when an unrelated target row owned that numeric ID.
- Changed direct header, line-table-derived and Dnevnik synthesis paths to resolve headers through source lineage and use PostgreSQL-generated target IDs. Propagated those target IDs to dependent lines and analytics sale-ID deltas.
- Preserved repeated no-row-ID lines with exact multiset re-import checks; changed ambiguous snapshots fail closed.
- Added SQLSTATE/table/constraint-aware classifications for sale-table unique failures and sanitized structured incident context.
- Enlisted analytics into the outer operational transaction only when the configured PostgreSQL connection identities match; an analytics-inclusive import fails closed when they differ.
- Changed sequence repair to advance a sequence only when a table contains an explicit ID above its current value. It no longer resets a sequence to a lower `MAX(Id)`.
- Added a short read-only incident triage section to the existing batch #23 audit.

## Files changed
- Api/Services/AccessImportService.cs
- Api.Tests/AccessImportForeignKeyGuardTests.cs
- Api.Tests/AccessImportRetryAtomicityTests.cs
- Infrastructure/DbContexts/TrendplusDbContext.cs
- docs/qa/ACCESS_IMPORT_23_FACT_AUDIT_2026-10-10.md
- .ai/runs/2026-10-15-access-import-p0-identity-evidence.md

## Validation run
- `git fetch origin` -> pass; `origin/main` is `6ff22b7ccd5c7dd96c53efe02b724d66be7cb5b3`.
- `git merge --no-edit origin/main` -> pass; latest-main merge commit is `4ffbc63e`.
- `dotnet build Api/Api.csproj --no-restore -m:1 -clp:ErrorsOnly` -> pass on `4ffbc63e`, 0 errors, 174 warnings.
- `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true dotnet test Api.Tests/Api.Tests.csproj --filter 'FullyQualifiedName~AccessImportForeignKeyGuardTests|FullyQualifiedName~AccessImportRetryAtomicityTests' --no-restore -m:1 --logger 'console;verbosity=normal' -consoleloggerparameters:ErrorsOnly` -> pass, 14 passed, 0 failed, 0 skipped; Docker-backed PostgreSQL ran.
- Repeated the same focused PostgreSQL filter after incorporating latest main at `4ffbc63e` -> pass, 14 passed, 0 failed, 0 skipped.
- Fast-forward pushed to existing draft PR #163, transport branch `codex/access-sale-line-identity`, from `52fad788` to head `4c6f0c9067dc2c5ece52904165f6e8e704bd5756`; no force push. PR was verified OPEN and draft, base `main`.
- Exact-head CI snapshot for PR head `4c6f0c9067dc2c5ece52904165f6e8e704bd5756`: `Analytics Tests & Data Integrity` and `Analytics Quality Gates` were `in_progress` (backend build/migrations succeeded; backend tests had not started); Vercel context failed with a build rate-limit URL. These are not green exact-head results. See [PR #163](https://github.com/ivanjovicic/Trendplus/pull/163).
- The PostgreSQL class includes direct-header collision/re-import, duplicate same-product lines, line-table-derived header, Dnevnik synthesis, primary-key vs lineage-unique diagnostics, sequence monotonicity, and cursor rollback.
- `git diff --check` -> pass after merging latest main; a later evidence-only run-log update is recorded in the delivery commit.
- Full backend suite, run with `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`, ended with 2,094 passed, 43 skipped, and 5 failed (2 RQ610/RQ601 fixtures had no configured test connection; 3 supplier/SQL029 PostgreSQL fixture setup timeouts under parallel container load). All five named tests then passed in isolated runs against a disposable PostgreSQL container: RQ610 2/2, supplier parity 1/1, SQL029 lifecycle 1/1, RQ601 1/1. A second serialized full-suite attempt with RQ610/RQ601 connections ran idle for more than 13 minutes without a result and was interrupted; full suite status is incomplete, not green. Temporary runner config and disposable container were removed.

## Validation not run
- Full backend suite -> incomplete as detailed above; the isolated reruns do not convert the full-suite result into a pass.
- Independent before/after SQL oracle for `all` / `imported` / `existing`, including `SalesFacts` and `SalesLineFacts` -> not run against this implementation. Batch #23's historical oracle is documented in the existing audit, but it is not a post-fix oracle.
- Full failure injection during header flush, line flush, analytics sync, final flush/commit and retry -> not run. The cursor repository rollback test proves its update participates in a Trendplus transaction, not every batch failure stage.
- Exact-head CI -> still in progress/unresolved at the inspected head, with Vercel build-rate-limit failure as detailed above; no green exact-head backend result was observed.
- Production incident exception/batch log -> unavailable in this run; no production data was read or changed.

## Documentation impact
- Added incident capture fields, read-only SQL triage and safe ambiguity handling to `docs/qa/ACCESS_IMPORT_23_FACT_AUDIT_2026-10-10.md`.
- Existing audit confirms batch #23 had 5,530 Access headers with lineage and equal old target/source IDs; 67,092 lines have NULL `SourceRowId` but do have `SourceHash`.

## What was missed
- No durable source-database namespace is present on sale lineage or analytics line facts. The upload filename/storage key is not a safe substitute. A source-to-target namespace migration and safe legacy reconciliation need a stable Access database identity or an explicit operator-owned source identifier.
- Legacy rows without a unique reconciliation proof were not backfilled. No receipt-number/article/price/date heuristic was used.
- The requested code-path audit of the other entities was performed and recorded in the existing batch #23 audit. Confirmed overwrite/mapping risks remain in `TipObuce`, `Dobavljac`, `Sezona`, `Artikli`, `DnevnikPromena`, `PovracajZaglavlje`, and `PovracajStavke`; the synthetic `Nivelacije`, `UnosRobe`, `Povratnice`, and `PrenosRobe` paths retain a namespace/provenance risk. These paths were not refactored or claimed fixed.
- Strategy changes between direct table, line-derived and Dnevnik synthesis do not have a proven cross-strategy identity contract.
- Multi-source namespace isolation, the complete production-compatible PostgreSQL matrix and operator runbook are not complete.

## Risks
- Do not merge PR #163. Current source identity still lacks a stable database namespace, so two Access databases with the same source table/row IDs cannot be safely distinguished. The required external identity/provenance input is not present in the repository or supplied incident logs. The confirmed non-sale entity identity risks also remain.
- Separate operational and analytics databases cannot be committed atomically; the new gate refuses `includeAnalytics` imports in that configuration. Verify deployment connection topology before using this path.
- Local PostgreSQL proof covers focused regressions only and is not full acceptance evidence.

## Post-close routing recovery
- not applicable for direct-user-request; the change has not closed on main.

## Next
- Obtain the stable Access database namespace and original incident exception/batch log from the system/operator owner; then design the namespace migration and dry-run legacy reconciliation before claiming P0 completion.
- Complete the remaining per-entity source-to-target maps/FK remapping, full legacy proof, failure-injection matrix and independent post-fix SQL oracle; inspect exact-head CI before any merge decision.
