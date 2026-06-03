# Analytics CI Gates

Trendplus currently uses two GitHub Actions workflows to keep analytics regressions from slipping into main:

- `.github/workflows/ci.yml`
- `.github/workflows/analytics-tests.yml`

The split is intentional:

- `ci.yml` runs the solution build, frontend analytics guardrails, frontend build, and frontend regression tests.
- `analytics-tests.yml` runs the API test project with a local Postgres service so the backend can exercise unit and integration coverage without a full browser or deployment stack.

## Checks

| Check | Command | Runs on PR | Runs on main | Blocks merge | Notes |
|---|---|---:|---:|---:|---|
| Backend solution build | `dotnet restore Trendplus2.sln` then `dotnet build Trendplus2.sln --no-restore --configuration Release` | Yes | Yes | Yes | Defined in `ci.yml`. This is the compile gate for the repo. |
| Backend unit tests | `dotnet test Trendplus2.sln --no-build --configuration Release --filter "Category=Unit" --verbosity minimal` | Yes | Yes | Yes | Defined in `ci.yml`. This is the lightweight backend safety net. |
| Backend integration tests | `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration" --verbosity normal` | Yes | Yes, for backend-path pushes | Yes | Defined in `analytics-tests.yml`. Uses a local Postgres service. This is the targeted backend test coverage instead of a single monolithic `dotnet test` across the entire repo. |
| Analytics guardrails + typecheck | `cd Klijent/clientapp && npm run check:analytics-guardrails` | Yes | Yes | Yes | Defined in `ci.yml`. The script already includes the frontend typecheck. No separate mojibake scan exists in the current scripts. |
| Frontend unit and regression tests | `cd Klijent/clientapp && npm run test -- --run` | Yes | Yes | Yes | Defined in `ci.yml`. `--run` keeps Vitest out of watch mode in CI. |
| Frontend build | `cd Klijent/clientapp && npm run build` | Yes | Yes | Yes | Defined in `ci.yml`. This catches TypeScript and Vite bundle regressions. |

## Why the backend tests are split

A single repo-wide `dotnet test` is not the current gate. The backend test strategy is split into:

- a solution build plus unit tests in `ci.yml`
- API integration tests with Postgres in `analytics-tests.yml`

That keeps PR checks lighter while still giving analytics-related backend coverage a real database-backed path.

## What is not claimed

- There is no fake email, Slack, Teams, or webhook alerting in CI.
- There is no dedicated mojibake scan in the current scripts.
- There is no browser or E2E suite in the CI gate.
