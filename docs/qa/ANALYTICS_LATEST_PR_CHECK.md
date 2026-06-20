# Analytics Latest PR Check

Date: 2026-06-18
Repo: `ivanjovicic/Trendplus`

## Latest Head

- Local branch: `main`
- Local HEAD: `1467e9e45675022b31ebbb6a0fbd9a6a253798e0`
- Remote `origin/main`: `1467e9e45675022b31ebbb6a0fbd9a6a253798e0`
- Remote `origin/HEAD`: `1467e9e45675022b31ebbb6a0fbd9a6a253798e0`

## PR Status

- PR #1 head ref: `refs/pull/1/head`
- PR #1 head SHA: `6278af4f1ab4ca255faf4cbb85ae2525e1edb1e2`
- Relative to latest `main`, PR #1 is `57` commits behind and `3` commits ahead
- PR #1 is stale and does not contain the latest stabilization line on `main`

### Latest Stabilization Commits

These commits are present on latest `main`, but not in PR #1:

- `e4612178` `stabilization and frontend bug fixing`
- `acbd75c` `docs(security): plan analytics p0 access control`
- `9b34bc0` `docs(demo): add analytics demo reset runbook`
- `ca50e0b` `docs(qa): audit analytics cache invalidation`
- `d2a52d1` `docs(qa): add analytics pilot smoke test`
- `756aba7` `test(analytics): cover product action status fallback`
- `86e259f` `test(analytics): cover critical route mappings`
- `9be02ce` `docs(qa): triage render backend version`
- `7f86f92` `fix(analytics): make cache prewarm startup-safe`

## Vercel Status

- Most likely failure reason: Vercel project root/output config mismatch for the nested frontend app
- Evidence:
  - real frontend lives in `Klijent/clientapp`
  - repo root originally had no Vercel build override
  - repo root `package.json` did not expose the nested app build path
  - prior triage notes showed the same failure pattern on non-analytics commits
- Repo-side mitigation added in this pass:
  - root `vercel.json` now points Vercel at `Klijent/clientapp`

## Checks Run

- `git fetch origin`
- `git log --oneline -20`
- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Unit"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Category=Integration"`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`

## Results

- `git diff --check`: pass with line-ending normalization warning on `docs/qa/ANALYTICS_LATEST_PR_CHECK.md`
- `dotnet build`: pass
- `dotnet test` unit: pass
- `dotnet test` integration: pass
- `npm run check:analytics-guardrails`: pass
- `npm run build`: pass

## Failures / Warnings

- Current evidence does not support an analytics code regression
- The blocker is deployment configuration, not the stabilization feature set
- PR #1 should not be used as the merge vehicle for the latest analytics line
- Root-level `vercel.json` was added so the nested frontend app can build even if Vercel is still rooted at the repository top

## Merge Recommendation

- Merge candidate: latest `main` at `1467e9e45675022b31ebbb6a0fbd9a6a253798e0`
- Do not merge PR #1 as-is
- If a PR is needed, open it from latest `main` rather than `refs/pull/1/head`

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
