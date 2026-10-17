Task ID: STAB17
Queue: docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
Date: 2026-10-17
Agent/tool: Codex
Delivery target: main through existing draft PR #163
Working branch / PR: agent/stab17-pr163-engineering -> codex/access-sale-line-identity / #163
Main commit SHA: pending
Main verification: pending - PostgreSQL-required exact-head PR CI must pass before merge
Evidence state: pending - local disposable PostgreSQL is unavailable because Docker did not respond
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
- `dotnet build Api/Api.csproj -c Release --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass, 0 warnings and 0 errors.
- `node scripts/check-agent-instructions.mjs --self-test`, `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs --self-test`, `node scripts/check-prompt-queues.mjs`, `node scripts/check-planning-architecture.mjs --self-test`, `node scripts/check-planning-architecture.mjs` -> pass; queue 811, planning 80.

## Validation not run

- Disposable PostgreSQL/Testcontainers suite and migration rehearsal -> not run locally. `docker version --format '{{.Server.Version}}'` produced no daemon response in ten seconds, and the focused PostgreSQL test host remained in startup waiting for Docker. This is an environment limitation, not a passing or skipped product test. Exact-head PR CI is required before merge.
- A real Windows Access ODBC file and a real Linux MDBTools file -> not available locally. The source-session contract, ODBC connection-string, predicate-pushdown and MDBTools CSV behavior tests pass; live provider execution remains CI/operator evidence.
- Production import, production SQL, production migration/backfill, `setval`, deploy -> intentionally not run; not authorized.

## Exact-head CI correction

- The first exact-head run for `d62a3985da3f5fe472df1d33769da37091389301` (`Analytics Tests & Data Integrity` run `39035758843`) applied both disposable PostgreSQL migrations and passed its separate operations certification, but the complete backend suite failed (2,186 passed, 24 failed, 43 skipped). This was not accepted as a pass.
- The failures exposed concrete namespace-transition defects: minimal PostgreSQL fixtures omitted `DataImportBatches.SourceNamespace`, the private batch-creation reflection fixture used the old parameter list, collision classification did not recognize the new namespace indexes, and the read-only oracle used an unquoted analytics fact key. The same run also exposed stale expectations that source and target IDs share a value, a forbidden sequence-reset test, and an Access negative-ID sign check.
- Corrected those fixtures/contracts and retained the production fail-closed namespace rule. The replacement test now proves that an Access source ID equal to a web/POS target ID creates a separate mapped target record, does not overwrite the web/POS record, and does not repair PostgreSQL sequences.
- Post-correction local proof: `dotnet build Api.Tests/Api.Tests.csproj --no-restore -p:RunAnalyzers=false -v:minimal -nodeReuse:false` -> pass (21 existing warnings, 0 errors); `dotnet test ... --filter "FullyQualifiedName~NegativeEntityIdGuardTests"` -> 8/8 passed, 0 skipped; `git diff --check` -> pass. PostgreSQL coverage remains pending a new exact-head CI because Docker is unavailable locally.

## Documentation impact

- Added the bigint rollout/rehearsal and backup-restore backout plan.
- Added the read-only SQL oracle and embedded its expected zero-violation/idempotency result in the disposable PostgreSQL fixture.

## What was missed

- No production namespace was registered and no historical row/cursor was backfilled. Those actions require operator authority and source evidence.
- The physical `integer` to `bigint` migration remains a reviewed rehearsal plan, not an executed migration.

## Risks

- PR #163 remains draft and cannot be merged until the exact-head PostgreSQL-required CI proves the disposable database scenarios.
- Existing historical rows without a namespace/source row ID intentionally remain unmatched; routine imports fail closed instead of guessing.
- The current target ID headroom guard intentionally blocks sales import at fewer than 10,000 IDs remaining; observed production headroom requires the separately approved bigint rollout.

## Post-close routing recovery

- pending: STAB17 remains `IN_PROGRESS`; no terminal queue transition or successor promotion is authorized before exact-head CI and external production gates.

## Next

- Push the correction to existing draft PR #163, inspect its new exact-head CI, then keep STAB17 WAITING/PARTIAL until the authorized source-namespace registration, disposable PostgreSQL migration rehearsal, and production evidence gates are complete.
