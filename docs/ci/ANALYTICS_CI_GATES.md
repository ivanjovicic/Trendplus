# Analytics CI Gates

Ovaj dokument opisuje minimalne CI gate-ove za analytics i frontend guardrails.

## Trenutno stanje

- Backend build/test ostaje u `.github/workflows/analytics-tests.yml`.
- Frontend guardrails/build su u `.github/workflows/analytics-quality-gates.yml`.
- Nema eksplicitnog blokera; minimalni set je implementiran.

## Gate tabela

| Check | Command | Runs on PR | Runs on main | Blocks merge | Notes |
| --- | --- | --- | --- | --- | --- |
| Backend build | `dotnet build Trendplus2.sln --no-restore --configuration Release` | Yes | Yes | Yes | Pokriva compile regresije u API/Domain/Infrastructure slojevima. |
| Backend tests | `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"` i `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration"` | Yes | Yes | Yes | Targeted gate; full solution `dotnet test` nije uveden zbog Postgres/integration setup-a i sporijeg feedback loop-a. |
| Frontend guardrails | `cd Klijent/clientapp && npm run check:analytics-guardrails` | Yes | Yes | Yes | Uključuje guardrail scan i `tsc -b` kroz postojeći npm script. |
| Frontend build | `cd Klijent/clientapp && npm run build` | Yes | Yes | Yes | Hvata TypeScript/Vite regresije i bundle probleme. |

## Workflow reference

- `.github/workflows/analytics-tests.yml`
- `.github/workflows/analytics-quality-gates.yml`

## Napomena

Ako backend testovi postanu preteški za GitHub Actions, sledeći korak je da se dokumentuje još uži targeted set, ali `dotnet build` ostaje obavezan.
