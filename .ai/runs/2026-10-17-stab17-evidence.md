Task ID: STAB17
Queue: docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
Date: 2026-10-17
Agent/tool: Codex
Delivery target: main through existing draft PR #163
Working branch / PR: agent/stab17-pr163-engineering -> codex/access-sale-line-identity / #163
Main commit SHA: not delivered (PR #163 remains DRAFT/PARTIAL by policy)
Main verification: origin/main is intentionally unchanged; exact-head PR CI is green on the draft PR head and does not authorize merge
Evidence state: repository-local hardening validated on exact-head CI; production/operator gates remain open
Current hardening head: `2185e680154fec5eb4f90e5f6a449c999eaa2c0f` (docs-only evidence follow-up after code `bdc4d508`); PR #163 remains the delivery transport and is not mergeable.
Ownership transfer: explicit owner release in merged PR #191; STAB17 is reserved by this run
Claim reservation: inspected origin/main e7781fa771d2b04aad793cefd650de40f6d410ff; pushed claim a51be37fc12c67a791c8c33082687d0ff7af238c; worktree C:\Users\Ivan\.codex\worktrees\stab17-pr163\Trendplus2; branch agent/stab17-pr163-engineering; existing remote PR #163 head fb193a076e47f79870acb84ec134ee1c1fe97a35

## What was done

- Completed the repository-local STAB17 correction on the existing PR #163 branch.
- Made routine Access uploads fail closed until an explicitly registered, immutable logical source namespace is supplied; filename, path, and file hash are never used as identity.
- Persisted namespace provenance across batch, source lineage, headers, lines, returns, articles, journal rows, analytics facts, and namespace-plus-physical-table import cursors.
- Kept PostgreSQL target identifiers generated independently of Access IDs. Sale/header/line/article mappings now use namespace, physical table, and source row identity; ambiguous or legacy rows without proof are blocked instead of heuristically rebound.
- Changed routine import defaults to safe incremental re-read/anti-join behavior. ID-only cursors do not use an unsafe `id > cursor` predicate, source sale dates are not cursor timestamps, and a cursor is committed only after the enclosing successful import step.
- Disabled routine full/overwrite sales reset at API, enqueue, and worker-reload boundaries; POS and legacy records remain untouched.
- Removed the Dnevnik-delta gate as a reason to skip direct sales/returns work.
- Added a read-only source-to-header-to-line-to-fact SQL oracle, namespace-aware analytics fact persistence, isolated bigint rollout/backout plan, and synthetic PostgreSQL integration coverage for lineage, facts, rollback, and target-ID headroom.
- Added UI/API namespace gating and tests. Routine import cannot be started from the UI until the namespace is supplied.

## 2026-10-17 final-hardening continuation

- Added a read-only pre-write baseline guard. Legacy Access headers, lines, returns,
  movements and articles without complete namespace/table/row provenance are reported
  as `exact=0`, `ambiguous=0`, `unmatched=N` and block the transaction. No heuristic
  reconciliation or automatic backfill is performed.
- Kept unsupported legacy-global master/store families fail-closed: non-Access target
  ID collisions are rejected, and a second logical Access namespace cannot reuse
  `TipObuce`, `Dobavljaci`, `Sezone` or store IDs until their lineage migration is
  certified.
- Added a true pre-write PostgreSQL sequence inspection (`pg_get_serial_sequence` /
  `pg_sequences`) for both sales tables. It rejects stale sequences, target high-water
  collisions and low remaining Int32 headroom without calling `setval` or changing
  schema. The existing post-flush high-water check remains as a defence in depth.
- Fixed article replay state: preloaded `Kolicina` and persisted `SourceHash`/lineage
  are restored on attached stubs, NULL Access stock preserves known stock, and
  generated target IDs are retained across a flush batch for duplicate source rows.
- UI now defaults to operational-only import when databases are separate, persists an
  explicit Analytics pending/stale warning, and labels the result accordingly. No
  cross-database atomicity is claimed; a durable projection/outbox remains external.
- Expanded the read-only namespace oracle with master/product/return FK controls,
  SalesFacts amount/quantity/line drift, provenance populations, and negative-parent
  checks. The unrelated `InfoTip.tsx` change remains untouched after its independent
  5/5 component test passed; it is not part of this importer hardening.

## Files changed

- Access importer, cursor repository, endpoint, and options: `Api/Services/AccessImportService.cs`, `Api/Services/Access/AccessImportCursorRepository.cs`, `Api/Services/Access/IAccessImportCursorRepository.cs`, `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Config/AccessImportOptions.cs`.
- Domain/EF lineage and migration artifacts: `Domain/Model/**`, `Infrastructure/DbContexts/**`, `Infrastructure/Migrations/20261017082507_AddAccessSourceNamespaces*`, `Infrastructure/Migrations/AnalyticsDb/20261017124952_AddAccessSourceNamespaceFacts*` and snapshots.
- Focused backend tests: `Api.Tests/AccessImport*Tests.cs`, `Api.Tests/DataSourceConnectorContractTests.cs` and related endpoint/worker test doubles.
- UI/API client and tests: `Klijent/clientapp/src/pages/AccessImportPage.tsx`, `Klijent/clientapp/src/services/accessImportApi.ts`, and their focused specs.
- Operational evidence: `tools/access-import-namespace-oracle.sql`, `docs/operations/ACCESS_IMPORT_BIGINT_ROLLOUT.md`.

## Validation run

- `git diff --check` -> pass before delivery commit.
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass, 21 pre-existing repository warnings and 0 errors.
- Focused Access non-PostgreSQL tests for cursor, connector, service, Dnevnik, enqueue and endpoint behavior -> pass, 73/73 and 0 skipped.
- `dotnet ef migrations has-pending-model-changes --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj --context TrendplusDbContext --no-build` -> pass.
- Same EF command for `AnalyticsDbContext` -> pass.
- `npm run test:run -- src/pages/__tests__/AccessImportPage.spec.tsx src/services/__tests__/accessImportApi.spec.ts src/services/__tests__/accessImportActiveBatch.spec.ts` -> pass, 17/17.
- `npm run build` in `Klijent/clientapp` -> pass.
- Final-hardening local compile `dotnet build Api/Api.csproj --no-restore` -> pass, 0 errors (repository analyzer warnings only).
- Final-hardening test compile `dotnet build Api.Tests/Api.Tests.csproj --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass, 21 pre-existing warnings, 0 errors.
- Focused non-PostgreSQL importer/queue/endpoint tests after hardening -> pass, 62/62, 0 skipped.
- `npm run test:run -- src/pages/__tests__/AccessImportPage.spec.tsx src/services/__tests__/accessImportApi.spec.ts src/services/__tests__/accessImportActiveBatch.spec.ts` -> pass, 17/17.
- Independent unrelated `InfoTip.spec.tsx` -> pass, 5/5.
- `npm run build` after the Analytics pending/stale UI change -> pass (TypeScript + Vite release bundle).
- `git diff --check` -> pass after the hardening patch.
- `dotnet build Api/Api.csproj -c Release --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass, 0 warnings and 0 errors.
- `node scripts/check-agent-instructions.mjs --self-test`, `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs --self-test`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs --self-test`, `node scripts/check-planning-architecture.mjs` -> pass; queue 811, planning 80.
- Exact-head CI on `2185e680154fec5eb4f90e5f6a449c999eaa2c0f`: Planning Governance run `39069219629` -> success; Analytics Quality Gates run `39069219574` -> success; Analytics Tests & Data Integrity run `39069219620` -> success.
- Exact-head backend CI: 2,253 total, 2,210 passed, 0 failed, 43 individually classified known skips (13 isolated PostgreSQL-certifiable, 30 legacy superseded, 0 uncovered, 0 new/reason-changed); RQ453 disposable PostgreSQL certification passed 5/5, parity 2/2, inventory 1/1, runtime raw-fact 1/1, six-screen 1/1, OP2 4/4, and integrity contracts with zero skips.
- Exact-head frontend CI: analytics 167 files/1,305 tests passed; UI regression 3 files/11 tests passed; goods-receipt/storno 3 files/13 tests passed; release build and bundle gates passed.

## Validation not run

- Disposable PostgreSQL/Testcontainers suite and migration rehearsal -> not run locally. `docker version --format '{{.Server.Version}}'` produced no daemon response in ten seconds, and the focused PostgreSQL test host remained in startup waiting for Docker. This is an environment limitation, not a passing or skipped product test. Exact-head PR CI supplied the disposable PostgreSQL proof; local migration rehearsal remains outstanding.
- A real Windows Access ODBC file and a real Linux MDBTools file -> not available locally. The source-session contract, ODBC connection-string, predicate-pushdown and MDBTools CSV behavior tests pass; live provider execution remains CI/operator evidence.
- Production import, production SQL, production migration/backfill, `setval`, deploy -> intentionally not run; not authorized.

## Exact-head CI correction

- The first exact-head run for `d62a3985da3f5fe472df1d33769da37091389301` (`Analytics Tests & Data Integrity` run `39035758843`) applied both disposable PostgreSQL migrations and passed its separate operations certification, but the complete backend suite failed (2,186 passed, 24 failed, 43 skipped). This was not accepted as a pass.
- The failures exposed concrete namespace-transition defects: minimal PostgreSQL fixtures omitted `DataImportBatches.SourceNamespace`, the private batch-creation reflection fixture used the old parameter list, collision classification did not recognize the new namespace indexes, and the read-only oracle used an unquoted analytics fact key. The same run also exposed stale expectations that source and target IDs share a value, a forbidden sequence-reset test, and an Access negative-ID sign check.
- Corrected those fixtures/contracts and retained the production fail-closed namespace rule. The replacement test now proves that an Access source ID equal to a web/POS target ID creates a separate mapped target record, does not overwrite the web/POS record, and does not repair PostgreSQL sequences.
- Post-correction local proof: `dotnet build Api.Tests/Api.Tests.csproj --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass (21 existing warnings, 0 errors); `dotnet test ... --filter "FullyQualifiedName~NegativeEntityIdGuardTests"` -> 8/8 passed, 0 skipped; `git diff --check` -> pass. PostgreSQL coverage remains pending a new exact-head CI because Docker is unavailable locally.
- Exact-head run `39037183108` on `09b8a66ee30b8d8f67ca9254622f427d065165fe` was inspected and rejected: 2,206 passed, 4 failed, 43 skipped. The three RQ606 failures exposed that one Access transfer row can create two movement roles, while the mixed-population oracle still expected the synthetic table key before normalization.
- Corrected the transfer identity contract on `b98cc50c`: Dnevnik uniqueness is now namespace + physical table + source row + movement role + deterministic repeated-line ordinal; the importer sets that ordinal for repeated source documents and scopes existing transfer matching to the active namespace. The oracle test now asserts the normalized `saleslines` physical key. The migration SQL drops/recreates the named index so an older four-column index cannot survive silently.
- Exact-head run `39055724784` on `02e6600d8b6a2b2be37e96ccee7daa50bd47ab97` was also inspected and rejected: 2,207 passed, 3 failed, 43 skipped. Its failures were exactly the above transfer-ordinal and normalized-key defects; no other Access identity failures remained. A fresh exact-head CI for `b98cc50c` is required.
- Exact-head run `39056717016` on `49722964382d52fff55f50f4136ecaa915c87218` is green: backend 2,253 total, 2,210 passed, 0 failed, 43 skipped; the skip inventory classified all 43 with 0 new/reason-changed skips; RQ453 Operations analytics certification also passed. The Access identity regressions (all RQ606 cases and the mixed-population oracle) passed on this head.
- Exact-head run `39057531551` on final documentation head `7ce7b5a2879f9d49a8f44d0fb178ef8450f5e1a0` is also green: backend 2,253 total, 2,210 passed, 0 failed, 43 skipped; 43 known skips and 0 new/reason-changed skips; RQ453 passed.
- Exact-head run `39058248581` on final head `0255a296f4577062dcfd331f2722d1b31321e175` is green: backend 2,253 total, 2,210 passed, 0 failed, 43 known skips and 0 new/reason-changed skips; RQ453 passed.

## Documentation impact

- Added the bigint rollout/rehearsal and backup-restore backout plan.
- Added the read-only SQL oracle and embedded its expected zero-violation/idempotency result in the disposable PostgreSQL fixture.

## What was missed

- No production namespace was registered and no historical row/cursor was backfilled. Those actions require operator authority and source evidence.
- The physical `integer` to `bigint` migration remains a reviewed rehearsal plan, not an executed migration.
- Source-scoped target mapping for legacy-global `TipObuce`, `Dobavljac`, `Sezona` and
  store dimensions is intentionally not claimed; the importer now blocks unsafe
  combinations until that migration/certification is approved.
- Durable cross-database analytics outbox/projection is not implemented; production
  uses the explicit operational-only/pending path until that owner delivers it.

## Risks

- PR #163 remains draft and cannot be merged despite green exact-head CI because source certification, legacy lineage, bigint rollout, analytics projection and operator/production gates remain open.
- Existing historical rows without a namespace/source row ID intentionally remain unmatched; routine imports fail closed instead of guessing.
- The current target ID headroom guard intentionally blocks sales import at fewer than 10,000 IDs remaining; observed production headroom requires the separately approved bigint rollout.

## Post-close routing recovery

- pending: STAB17 remains `IN_PROGRESS`/PARTIAL; exact-head CI is green, but no terminal queue transition or successor promotion is authorized before the external production gates.

## Next

- Push the final-hardening correction to the existing draft PR #163 and inspect its
  exact-head PostgreSQL, backend, frontend and governance results. Keep STAB17
  IN_PROGRESS/PARTIAL until source-namespace certification, disposable bigint
  rehearsal, real provider evidence and production operator approval exist.
