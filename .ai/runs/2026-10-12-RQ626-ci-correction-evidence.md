# RQ626 CI Fixture Correction Evidence

Task ID: RQ626-CI-CORRECTION
Queue: direct-user-request
Date: 2026-10-12
Agent/tool: Codex / local PowerShell and GitHub CLI
Delivery target: main
Working branch / PR: `codex/rq626-ci-fixture-correction` / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none

## What was done

- Inspected the red current-main backend runs `38265801416` and `38265812357` on RQ626 implementation SHA `a3e621f74f9b3af1391714280fdd20e8b2b6f41a`.
- Classified the failing assertion as a fixture date error: SQL 022 and the existing UTC movement oracle report end-of-day historical balances, while this fixture placed the activity on the date whose balance it expected to precede that activity.
- Moved the inferred-negative receipt and restock matrix to the as-of date, and the earlier sale/transfer-out to the following historical day. No production SQL or API behavior changed.

## Files changed

- `Api.Tests/AnalyticsIntelligenceNegativeStockProxyTests.cs`
- `.ai/runs/2026-10-12-RQ626-ci-correction-evidence.md`

## Validation run

- `git diff --check` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AnalyticsIntelligenceNegativeStockProxyTests` -> build pass; test setup failed before executing the test body because Testcontainers could not connect to Docker (`Docker is either not running or misconfigured`).
- GitHub backend run `38265801416` -> red; failing oracle expected `-10`, got `0` for a movement seeded on the date being read.
- GitHub backend run `38265812357` -> red with the same RQ626 oracle failure; confirmed by the failed job log and PR #127 check rollup.

## Validation not run

- PostgreSQL-backed execution of the corrected oracle -> unavailable locally because Docker/Testcontainers cannot start; rerun in CI after publishing this correction.
- Corrected CI run -> pending.

## Documentation impact

- No product or contract documentation changed. This evidence records a CI fixture correction and its validation limit.

## What was missed

- The PostgreSQL oracle did not run locally, and the corrected fixture has not yet received a CI result.

## Risks

- The intended signed arithmetic now matches SQL 022's established end-of-day date attribution, but PostgreSQL CI must prove the corrected oracle before this correction is considered complete.

## Post-close routing recovery

- Not applicable for this direct-user-request correction. After delivery, refresh `origin/main` and re-check whether RQ630 remains the primary READY successor before starting it.

## Next

- Publish the correction for backend CI, merge it to `main` once the repository delivery path allows, verify the delivered SHA, and update this evidence with the result.
- Continue with RQ630 only after the current-main RQ626 backend failure is resolved and reclassified.
