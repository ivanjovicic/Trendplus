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
| JavaScript SDK pins | `node scripts/check-javascript-sdk-pins.mjs` | Yes | Yes (path-filtered) | Yes | Fails if any `.esproj` pins an unavailable `Microsoft.VisualStudio.JavaScript.Sdk` version. |
| Frontend guardrails | `cd Klijent/clientapp && npm run check:analytics-guardrails` | Yes | Yes | Yes | Uključuje guardrail scan i `tsc -b` kroz postojeći npm script. |
| Frontend build | `cd Klijent/clientapp && npm run build` | Yes | Yes | Yes | Hvata TypeScript/Vite regresije i bundle probleme. |
| Client dependency audit | `cd Klijent/clientapp && npm ci && npm audit --audit-level=high` | Yes | Yes | Yes | SEC08; koristi committed `package-lock.json` i fail-closed high threshold. |
| POS UI dependency audit | `cd Trendplus.POS.Ui && npm ci && npm audit --audit-level=high` | Yes | Yes | Yes | SEC08; pokriva odvojeni POS lockfile i ne zavisi od lokalnog cache-a. |

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

## Napomena

Ako backend testovi postanu preteški za GitHub Actions, sledeći korak je da se dokumentuje još uži targeted set, ali `dotnet build` ostaje obavezan.
