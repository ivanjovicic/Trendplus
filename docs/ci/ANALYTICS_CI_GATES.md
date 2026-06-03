# Analytics CI Gates

_Last audited: 2026-06-02_

---

## Workflow inventory

| File | Trigger | Purpose |
|---|---|---|
| `.github/workflows/ci.yml` | PR + push → `main`/`master` | **Primary quality gate** (see below) |
| `.github/workflows/analytics-tests.yml` | PR + push → `main`/`master` (backend paths only) | Backend unit + integration tests with live Postgres |
| `.github/workflows/db-migrate.yml` | push → `main` (migration paths) + manual | EF Core `database update` |
| `.github/workflows/fly-deploy.yml` | push → `main` | Deploy to Fly.io — no build/test gates |
| `.github/workflows/nivelacija-repair-dry-run.yml` | Manual | Nivelacija repair dry-run |
| `.github/workflows/deploy-render-manual.yml` | Manual | Render deploy fallback |

---

## Primary quality gate — `ci.yml`

Added 2026-06-02. Runs on every PR and every push to `main`/`master`.

### `backend` job

| Check | Command | Status |
|---|---|---|
| dotnet build | `dotnet build Trendplus2.sln --configuration Release` | ✅ enabled |
| dotnet test (unit) | `dotnet test --filter "Category=Unit"` | ✅ enabled |
| dotnet test (integration) | skipped — requires live Postgres | ⚠️ not in lightweight gate (covered by `analytics-tests.yml`) |

### `frontend` job

| Check | Command | Status |
|---|---|---|
| Analytics guardrails | `npm run check:analytics-guardrails` | ✅ enabled |
| Typecheck | included via `check:analytics-guardrails` → `tsc -b` | ✅ enabled |
| Unit + regression tests | `npm run test -- --run` | ✅ enabled |
| Mojibake scan | covered by `AnalyticsSalesReadinessRegression.spec.tsx` vitest test | ✅ enabled |
| Build | `npm run build` | ✅ enabled |

---

## Pre-existing `analytics-tests.yml`

Covers backend integration tests against a real Postgres service container.

**Gap:** path filter restricts the trigger to `Api/**`, `Api.Tests/**`, `Domain/**`, `Infrastructure/**`.  
Frontend-only PRs do **not** trigger this workflow.  
The new `ci.yml` fills this gap for both stacks.

---

## Guardrails script

`Klijent/clientapp/scripts/check-analytics-guardrails.mjs`

Scans `src/pages`, `src/components`, `src/services`, `src/utils` for forbidden patterns:

- Local `fmtRsd` / `fmtPct` / `formatCurrency` / `formatPercent` definitions
- `decisionScore =`, `confidencePct =`, `reliabilityPct =`, `recommendationStatus =` assignments
- Business-logic thresholds: `BOOST_SCORE_THRESHOLD`, `KEEP_SCORE_THRESHOLD`
- Score multiplication constants (`score * 0.`, `marginCoveragePct * 0.`)
- Intermediate normalisation variables: `trendNorm`, `shareNorm`

Exits with code `2` on violation. Allowed paths (formatters, constants, test files) are excluded.

---

## Mojibake scan

Covered by the vitest test `AnalyticsSalesReadinessRegression.spec.tsx`:

```
it("mojibake guardrail for analytics TSX does not introduce new corrupt tokens")
```

Regex: `/[ÃÅÄâ\uFFFD]/` across all source `.ts`/`.tsx` files.  
Runs as part of `npm run test -- --run` in the `frontend` CI job.

---

## Known blockers / limitations

| Item | Notes |
|---|---|
| Integration tests need Postgres | Only `analytics-tests.yml` spins up a service container. Not included in `ci.yml` to keep gate fast. |
| `fly-deploy.yml` deploys without waiting for CI | Deploy and CI are separate jobs. Consider adding a `needs: [ci / backend, ci / frontend]` dependency if strict gate-before-deploy is required. |
| No e2e / browser smoke in CI | Out of scope per task instructions. |
