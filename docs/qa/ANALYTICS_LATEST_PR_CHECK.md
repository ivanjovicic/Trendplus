# Analytics Latest PR Check

Date: 2026-06-18  
Repo: `ivanjovicic/Trendplus`

## Latest Head

- Local branch: `main`
- Local HEAD: `e4612178cb6b5346639daae7d9362d8210966356`
- Remote `origin/main`: `e4612178cb6b5346639daae7d9362d8210966356`
- Remote `origin/HEAD`: `e4612178cb6b5346639daae7d9362d8210966356`

## PR Status

- PR #1 head ref: `refs/pull/1/head`
- PR #1 head SHA: `6278af4f1ab4ca255faf4cbb85ae2525e1edb1e2`
- Relative to latest `main`, PR #1 is `52` commits behind and `3` commits ahead
- PR #1 does not contain the latest stabilization commits listed in the task

### Latest Stabilization Commits

These commits are present on latest `main`, but not in PR #1:

- `acbd75c` `docs(security): plan analytics p0 access control`
- `9b34bc0` `docs(demo): add analytics demo reset runbook`
- `ca50e0b` `docs(qa): audit analytics cache invalidation`
- `d2a52d1` `docs(qa): add analytics pilot smoke test`
- `756aba7` `test(analytics): cover product action status fallback`
- `86e259f` `test(analytics): cover critical route mappings`
- `9be02ce` `docs(qa): triage render backend version`
- `7f86f92` `fix(analytics): make cache prewarm startup-safe`

## Checks Run

- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration"`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`

## Results

- `dotnet build`: pass
- `dotnet test` unit: pass
- `dotnet test` integration: pass
- `npm run check:analytics-guardrails`: pass
- `npm run build`: pass

## Failures / Warnings

- No command failures
- `dotnet build` completed with existing repository warnings only
- `npm run build` completed with Vite chunk-size warning for `recharts`
- PR #1 is stale and should not be used as the merge vehicle for latest analytics stabilization

## Merge Recommendation

- Merge candidate: latest `main` at `e4612178cb6b5346639daae7d9362d8210966356`
- Do not merge PR #1 as-is
- If a PR is needed, open it from latest `main` or the current latest stabilization branch state rather than `refs/pull/1/head`

## Suggested PR Description

Title:

`Analytics pilot stabilization and production route hardening`

Body:

```text
## Summary

This PR captures the latest analytics stabilization state on top of the current mainline:

- route alignment and production route hardening
- startup-safe cache prewarm behavior
- product decision fallback protection
- pilot smoke documentation and cache invalidation audit notes
- demo reset runbook and access-control planning notes
- route and fallback regression coverage

## Verification

- dotnet build Trendplus2.sln --no-restore --configuration Release
- dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"
- dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration"
- cd Klijent/clientapp && npm run check:analytics-guardrails
- cd Klijent/clientapp && npm run build

## Merge note

PR #1 is stale and does not contain the latest stabilization commits. Use latest mainline HEAD for merge readiness.
```
