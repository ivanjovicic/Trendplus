# 2026-10-17 documentation audit - evidence

- Request: direct owner request (Ivan, t48u); Queue: direct-user-request. No RQ claimed, no queue status or ownership changed, no code changed.
- Base: `origin/main` `e7781fa7`.
- Read/verified against code: `navConfig.ts`, `HomePage.tsx`, `AccessImportEndpoints.cs`, `AccessImportJobQueue.cs`, `AccessImportPage.tsx`, `DatabaseMigrationOwnershipTests.cs`, `Program.cs`, `AnalyticsCachePolicy.cs`, `TestDbContextOptionsBuilderExtensions.cs`, `TestHostDbContextWarningConventionTests.cs`, `.github/workflows/*.yml`, `package.json` scripts.
- Ad-hoc link scan (markdown links and backticked repo paths, `.ai/runs` excluded): 560 unresolved references in 46 files before, 523 in 36 after. The remainder is mostly deleted-by-design `.ai/task-locks` references, plus the items listed in the report.
- Findings, changes and open items: `docs/qa/DOCUMENTATION_AUDIT_2026-10-17.md`.
- Checks: see the commit; the governance validators and `git diff --check` were run before the push.
