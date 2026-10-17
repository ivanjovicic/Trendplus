# 2026-10-17 documentation audit - evidence

- Request: direct owner request (Ivan, t48u); Queue: direct-user-request. No RQ claimed, no queue status or ownership changed, no code changed.
- Base: `origin/main` `e7781fa7`.
- Read/verified against code: `navConfig.ts`, `HomePage.tsx`, `AccessImportEndpoints.cs`, `AccessImportJobQueue.cs`, `AccessImportPage.tsx`, `DatabaseMigrationOwnershipTests.cs`, `Program.cs`, `AnalyticsCachePolicy.cs`, `TestDbContextOptionsBuilderExtensions.cs`, `TestHostDbContextWarningConventionTests.cs`, `.github/workflows/*.yml`, `package.json` scripts.
- Ad-hoc link scan (markdown links and backticked repo paths, `.ai/runs` excluded): 560 unresolved references in 46 files before, 523 in 36 after. The remainder is mostly deleted-by-design `.ai/task-locks` references, plus the items listed in the report.
- Findings, changes and open items: `docs/qa/DOCUMENTATION_AUDIT_2026-10-17.md`.
- Checks: see the commit; the governance validators and `git diff --check` were run before the push.

## Second pass (owner-delegated decisions, 2026-10-17)

- Base: `origin/main` `b6493046`; branch `docs/archive-roadmap-20261017` (PR because Planning Governance CI gets a new step).
- STAB17 status read from `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`: IN_PROGRESS (Codex `/root`) -> roadmap not restructured; one "Current state / next" block added above the STAB17 sections.
- Path-dependency scan for the 32 root notes (`git grep` over the tree excluding `.ai/runs`): no script/validator/workflow/Docker/README dependency; 5 doc references updated.
- Encoding probe: the 10 non-UTF-8 files contained only bytes 0x8A/0x8E/0x9A/0x9E; CP1250 and CP1252 decode identically; UTF-8 -> CP1250 round-trip equals the original bytes; CRLF counts unchanged (e.g. `START_HERE.md` 231 CRLF, 10 diacritics).
- `WorkerRuntimePolicyService.cs` read: `GetPolicyAsync` generic catch -> `CreateAllowFallback` (`CanRunNow: true`); `RequestManualRunAsync` generic catch -> fabricated token; `TryConsumeManualRunRequestAsync` -> false. 12 workers call `GetPolicyAsync`. Registered as READY RQ686 (RQ685 is DONE).
- `render.yaml`: `trendplus-api` WorkerEnabled=false/RegisterWorkerInWebProcess=false, `trendplus-worker` true/false, no `Storage__Provider`; storage keys from `StorageOptions`/`S3FileStorage` (`Provider`, `Bucket`, `Endpoint`, `Region`, `AccessKey`, `SecretKey`).
- `ARCHITECTURE_BOUNDARIES.md`: 32/32 named files exist; all module-map routes present in `analyticsRouteDefinitions.ts`/`navConfig.ts`; Home and Decision Pulse rows were missing.
- Link checker probe: injecting `[probe](NOPE_PROBE.md)` into a tracked doc made `node scripts/check-doc-links.mjs` fail with that entry; the real tree has 0 broken relative Markdown links.
- Checks: all five governance validators with and without `--self-test` PASS (prompt queues 812 tasks, planning architecture 80 tasks, agent instructions 18 files), `git diff --cached --check` clean, `node Klijent/clientapp/scripts/check-encoding.mjs` OK.
