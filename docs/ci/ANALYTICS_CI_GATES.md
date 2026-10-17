# Analytics CI Gates

Ovaj dokument opisuje minimalne CI gate-ove za analytics i frontend guardrails.

## Trenutno stanje

- Backend build/test ostaje u `.github/workflows/analytics-tests.yml`.
- Frontend guardrails/build su u `.github/workflows/analytics-quality-gates.yml`.
- Nema eksplicitnog blokera; minimalni set je implementiran.

## Gate tabela

| Check | Command | Runs on PR | Runs on main | Blocks merge | Notes |
| --- | --- | --- | --- | --- | --- |
| Backend build | `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` | Yes | Yes | Yes | Pokriva compile regresije u API/Domain/Infrastructure/Workers graph-u bez učitavanja frontend `.esproj` wrappera. |
| Backend tests | `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release` (+ coverage collector) | Yes | Yes | Yes | Full `Api.Tests` suite with Postgres service; no test filters. |
| Backend skip inventory | `python3 scripts/ci/validate_backend_skip_inventory.py --trx TestResults/analytics-tests.trx --inventory scripts/ci/backend-skip-inventory.json` | Yes | Yes | Yes | Requires the exact classified legacy skip set and reasons; new, missing, or changed skips fail the job. |
| Operations PostgreSQL certification | Required RQ453 suites in `.github/workflows/analytics-tests.yml` | Yes | Yes | Yes | Strict TRX counts require Supplier/Shoe oracle 5/5, four-screen parity 2/2, inventory and raw-fact proofs 1/1 each, six-screen routes 1/1, and OP2 seams 4/4 with zero skips. |
| JavaScript SDK pins | `node scripts/check-javascript-sdk-pins.mjs` | Yes | Yes (path-filtered) | Yes | Fails if any `.esproj` pins an unavailable `Microsoft.VisualStudio.JavaScript.Sdk` version. |
| Frontend guardrails | `cd Klijent/clientapp && npm run check:analytics-guardrails` | Yes | Yes | Yes | Uključuje guardrail scan i `tsc -b` kroz postojeći npm script. |
| Frontend analytics tests | `cd Klijent/clientapp && npm run test:analytics` | Yes | Yes | Yes | Vitest za analytics/inventory komponente, servise i `src/pages`. |
| UI program gates | `cd Klijent/clientapp && npm run check:ui-program-gates` | Yes | Yes | Yes | UI ratchets i chart accessibility (sa self-testovima), responsive baseline self-test i theme-token specovi. |
| Frontend build | `cd Klijent/clientapp && npm run build` | Yes | Yes | Yes | Hvata TypeScript/Vite regresije i bundle probleme. |
| Client dependency audit | `cd Klijent/clientapp && npm ci && npm audit --audit-level=high` | Yes | Yes | Yes | SEC08; koristi committed `package-lock.json` i fail-closed high threshold. |
| POS UI dependency audit | `cd Trendplus.POS.Ui && npm ci && npm audit --audit-level=high` | Yes | Yes | Yes | SEC08; pokriva odvojeni POS lockfile i ne zavisi od lokalnog cache-a. |
| Planning governance | `node scripts/check-agent-instructions.mjs`, `check-prompt-queues.mjs`, `check-planning-architecture.mjs`, `check-analytics-execution-plan.mjs` (svaki i sa `--self-test`) | Yes (path-filtered) | Yes (path-filtered) | Yes | `.github/workflows/planning-governance.yml`; okida se samo za AGENTS/roadmap/`docs/ai`/`docs/planning`/`docs/product`/`docs/roadmaps`/`docs/architecture` i navedene status dokumente. |
| Vercel preview | Vercel integracija | Yes | - | No | Informativno; nije required check. |

## Workflow reference

- `.github/workflows/analytics-tests.yml`
- `.github/workflows/analytics-quality-gates.yml`
- Canonical build commands: `docs/ci/SOLUTION_AND_FRONTEND_BUILD_CONTRACT.md`
- Backend filter: `Trendplus2.Backend.slnf`
- Supply-chain policy: `docs/architecture/SUPPLY_CHAIN_ASSURANCE_POLICY.md`

## Backend workflow diagnostics (BCI02)

- Restore/build/test steps have stable IDs (`restore`, `build`, `test`).
- Missing coverage fails the job only when the test step succeeded but no Cobertura report was produced.
- After restore/build/test failures, the coverage summary explains the gap and exits successfully so it does not create a second root cause.
- `TestResults` upload uses `if-no-files-found: warn`, so upstream bootstrap failures do not add an artifact error.
- PostgreSQL service health probe uses `pg_isready -U postgres -d trendplus_test`.
- `scripts/ci/backend-skip-inventory.json` classifies all 42 live Operations skips observed in backend run `38178775379`; `scripts/ci/validate_backend_skip_inventory.py` rejects unclassified skip drift in the full-suite TRX.
- RQ453 enables `TRENDPLUS_RUN_INTEGRATION_TESTS=true` only for its disposable PostgreSQL certification job and enforces non-vacuous test totals for the critical seeded-fixture proofs.

## Test-harness konvencije (backend)

- `TestHostDbContextWarningConventionTests` zahteva opt-out `ManyServiceProvidersCreatedWarning` za svaku DbContext registraciju u `WebApplicationFactory` hostovima i u obicnim `ServiceCollection` testovima (RQ665, RQ685; helper `Api.Tests/TestDbContextOptionsBuilderExtensions.cs`). Bez toga EF baca gresku kad broj internih providera u procesu predje 20 i pad zavisi od redosleda testova.
- `PostgresRequiredAttributeConventionTests`: testovi koji traze PostgreSQL koriste `[PostgresRequiredFact]`; lokalni rani `return` bez Docker-a je `not run`.
- Merge politika (owner 2026-10-13, `AGENTS.md` 7.1): promena ciji PostgreSQL dokaz nije izvrsen lokalno ide kroz PR sa zelenim exact-head backend CI pre merge-a.

## Napomena

Ako backend testovi postanu preteški za GitHub Actions, sledeći korak je da se dokumentuje još uži targeted set, ali `dotnet build` ostaje obavezan.
