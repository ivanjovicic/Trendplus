# Documentation audit - 2026-10-17

Direct owner request (Ivan, 2026-10-17): analyze the documentation, fix what is stale, document the rest. Base `origin/main` `e7781fa7`. Queue: direct-user-request (no RQ claim, no status/ownership change). Every correction below was checked against code on that base.

## Fixed in this change (docs only)

| Area | File(s) | Correction |
|---|---|---|
| Navigation and Home | `README.md`, `docs/product/TRENDPLUS_HIDDEN_VALUE_UX_AUDIT_2026-10-15.md` | Primary menu after PR #181: Puls odluka is primary, Pre/Posle is under "Dodatne analize" (`navConfig.ts`). Home shows the *observed* Access sales date range, not a "confirmed period" (PR #182). The audit gets a post-delivery note. |
| Access import operations | `README.md` | Limit 100 MiB (`AccessImport:MaxUploadBytes`, 413, RQ677), advisory-lock concurrent start (RQ673/RQ672), 503 + Retry-After history (RQ674), admin key in sessionStorage (RQ675), `Storage:Provider=local` caveat (RQ679), production import not approved (STAB17). |
| Startup migrations | `README.md` | Deferred `DatabaseInitializer` only with `Database:AutoMigrate=true` or Development; no synchronous `Database.Migrate()`. The README also ended with a leftover chat-assistant offer; it was removed. |
| CI gates | `docs/ci/ANALYTICS_CI_GATES.md` | Missing gates added: `test:analytics`, `check:ui-program-gates`, Planning governance (4 validators, path-filtered), Vercel as non-required. Test-harness conventions added: EF warning opt-out (RQ665/RQ685), `[PostgresRequiredFact]`, PostgreSQL merge policy. |
| Validation choice | `docs/ai/VALIDATION_SELECTOR.md` | Added the 4th governance validator (`check-analytics-execution-plan.mjs`), `check:ui-program-gates`, the PostgreSQL-required rule and the EF opt-out rule. |
| Repeated failures | `docs/ai/COMMON_FAILURES_AND_FIXES.md` | New #25 (EF `ManyServiceProvidersCreatedWarning` flake, incl. the secondary `WorkerRuntimeSettings` 42P01 symptom) and #26 (Testing Library render race, PR #183). |
| Agent rules | `AGENTS.md` 0A.1 | Removed the stale "complete RQ666/RQ667 first" instruction; both have been DONE since 2026-10-15. It now points to the current P1 in `MASTER_ROADMAP.md`. |
| Broken links | 13 files in `docs/ai`, `docs/qa`, `docs/security`, `docs/demo` | `docs/analytics/` became `docs/Analytics/` (case breaks on Linux/GitHub), moved files were re-pointed, and absolute `C:/Users/Ivan/...` links became relative. Only the link targets changed; no status text changed. |

## Marked historical, then archived (second pass, same day)

- The 21 root notes and 11 root IDE plan prompts that got banners in the first pass were moved with `git mv` to `docs/archive/legacy-root/` (history kept). Index: [docs/archive/README.md](../archive/README.md). The repository root now keeps only `README.md`, `AGENTS.md` and `MASTER_ROADMAP.md` as Markdown.
- `docs/Frontend/ANALYTICS_NAVIGATION_AUDIT.md` (June 2026): banner; the current menu is `navConfig.ts`.
- June/May `docs/Analytics` audits (`DAILY_SALES_UX_AUDIT`, `INVENTORY_UX_AUDIT`, `SECONDARY_ANALYTICS_SCREENS_AUDIT`, `ACTION_OUTCOME_DATA_SHAPE_AUDIT`, `STOCK_COVER_SELL_THROUGH_AUDIT`, `KPI_METHODOLOGY_AUDIT`): HISTORICAL SNAPSHOT banner with pointers to `navConfig.ts`, `analyticsRouteDefinitions.ts` and the 2026-10-15 audits. All file paths they name still exist; their findings predate later RQ work and PR #181/#182, so they were not re-verified finding by finding.

## Decisions (second pass, 2026-10-17; the owner delegated them)

| # | Item | Decision and reason | Outcome |
|---|---|---|---|
| 1 | Legacy root notes | Archive, do not delete: they are history, but at the root they look like current runbooks. The only path dependencies were 5 doc references (no script, validator, CI, Docker or README dependency). | Moved to `docs/archive/legacy-root/` + index. Inbound references updated: `Database/Migrations/QUICKSTART.md`, `TEST_DATA_README.md`, `TROUBLESHOOTING_500_ERROR.md`, the DONE RQ411 "Read first" line (annotation only) and the 2026-10-07 product audit row. `.ai/runs/**` evidence logs were left unchanged (history). |
| 2 | Encoding of the 10 non-UTF-8 notes | Re-encode, because it is provably lossless: they contain only bytes 0x8A/0x8E/0x9A/0x9E (`Š Ž š ž`), identical in CP1250 and CP1252, so there is no guess. Each file was checked by a byte round-trip, and its CRLF count is unchanged. | 10 files are UTF-8 now. Letters such as `č`/`ć`/`đ` and emoji had already become `?` before this audit; that loss is noted in the index and not guessed back. |
| 3 | `MASTER_ROADMAP.md` | STAB17 is IN_PROGRESS (Codex `/root`) and owns the two top sections, so no restructuring. | Added one "Current state / next (read first, verified 2026-10-17)" block above the STAB17 sections, pointing to the 2026-10-16 routing; STAB17 text untouched. Condensing the older dated sections into a historical appendix is deferred until STAB17 is closed or released. |
| 4 | `WorkerRuntimePolicyService` fail-open | Code/behavior change, so no code edit. RQ685 is DONE and must not be reopened. | New READY **RQ686** (P2, parallel-safe with RQ680): fail-closed on unknown errors, keep the explicit missing-table bootstrap path, deduplicated Error log, no fake manual-run token, plus tests. Pointers updated in the addendum header and the roadmap RQ line. |
| 5 | RQ679 | Stays WAITING: Render config cannot be read from the repository. | RQ679 got an "Owner check (exact)" section: which keys to check on `trendplus-api` and `trendplus-worker`, how to decide the outcome, and the safe default (treat as `local` and do not rely on the queue for production imports). |
| 6 | Doc link checker | It fits the existing validator pattern (Node, `--self-test`, no dependencies) and is cheap. | `scripts/check-doc-links.mjs`: relative Markdown links in tracked `.md` outside `.ai/runs/**` and `docs/archive/**`, case-sensitive against `git ls-files`, ignores `*.lock(.md)` / `.ai/task-locks/`, optional frozen baseline `scripts/doc-links-baseline.txt` (`--write-baseline`). The current tree has 0 broken links, so no baseline is needed. Added to Planning Governance (self-test + check) through a PR. |
| 7 | Missing files in historical queue entries (`ADVANCED_V2_ANALYTICS_META_CONTRACT.md`, `RETAIL_ANALYTICS_COPILOT_PRD.md`, `SUPPLIER_SCORECARD_FORMULA.md`, `018_*`/`029_*.sql`) | Left as they are, per protocol: closed entries are a historical ledger. They are backtick references, not links, so they do not fail the checker. | No change. |
| 8 | `docs/ai/ARCHITECTURE_BOUNDARIES.md` | Checked against the code: all 32 named files exist, and the routes match `analyticsRouteDefinitions.ts`/`navConfig.ts`. The only inaccuracy was missing modules. | Added a verification note and rows for Home `/` and Decision Pulse `/analytics/decision-pulse`. |
| 9 | Mojibake found by a repo-wide `check-encoding.mjs` run (the canonical `npm run check:encoding` from `Klijent/clientapp` was already green) | These are clear double-encoded punctuation marks in 4 `docs/qa/*_2026-08-17.md` evidence files. Fixing them changes characters only, not meaning. | Fixed (`—`, `≠`, `“`, `”`). The two hits in `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md` intentionally quote the bug string `NeodreÄ‘eno` (RQ581), so they were left as they are. |

## Remaining (needs owner or later work)

| # | Finding | Owner |
|---|---|---|
| 1 | Condense `MASTER_ROADMAP.md` (about 1,050 lines of dated "current" sections) into the live block plus a historical appendix, keeping validator anchors (`primary/default READY`, `additional READY`, `Parallel-safe`, routing rows, queue links). | roadmap owner, after STAB17 is closed or released |
| 2 | RQ686 implementation (code + tests). | next RQ executor |
| 3 | RQ679 owner check on Render. | Ivan |
| 4 | `Database/Migrations/*.md` (for example `QUICKSTART.md`, `TEST_DATA_README.md`, `TROUBLESHOOTING_500_ERROR.md`) are still CP1250. They were edited in their own encoding, not converted. Convert them in a separate, verified pass. | docs owner |
| 5 | The link checker covers Markdown links only. Backtick path references (most of the stale refs) are not checked, by design, to avoid noise from historical ledgers. | optional follow-up |

Verified current, no change needed: `docs/Analytics/ANALYTICS_CACHE_POLICY.md` and `ANALYTICS_CACHE_INVALIDATION_AND_STALE_POLICY.md` (updated with PR #170/#171; TTLs match `AnalyticsCachePolicy.cs`), the RQ677/RQ673 queue entries, and the canonical stub `docs/Analytics/KPI_METHODLOGY_AUDIT.md`.
