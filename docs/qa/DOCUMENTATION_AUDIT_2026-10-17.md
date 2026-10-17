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

## Marked historical (banner, content unchanged)

- 21 root notes from Dec 2025-May 2026, now pointing to README / AGENT_START_HERE / ENCODING_AND_TEXT_SAFETY / glossary / AnalyticsMarginPolicy:
  - setup and fix notes: `START_HERE.md`, `STARTUP_GUIDE.md`, `HITNA_POMOC.md`, `QUICK_FIX_README.md`, `ONE_LINE_FIX.md`, `NEON_SETUP.md`, `RABBITMQ_CONNECTION_FIX.md`, `SERBIAN_LETTERS_FIX.md`, `MANUAL_FIX_SERBIAN_LETTERS.md`;
  - startup-migration notes (banner also states the current behavior): `AUTOMATIC_DATABASE_SETUP.md`, `AUTOMATIC_SETUP_FINAL.md`;
  - feature notes and reviews: `ANALYTICS_VELICINA_BOJA_FEATURE.md`, `IMAGE_ATTRIBUTION_*.md` (2), `LOGS_FEATURE.md`, `PERFORMANCE_TRACKING.md`, `SUPPLIER_DECISION_REPORT.md`, `PR3_ENGINEERING_REVIEW.md`;
  - frozen runtime-scoring scope: `Quest_DevEngineer_Experience_Final_v12.md`;
  - forensic reports: `MARGIN_FORENSIC_*.md` (2).
- 11 root IDE plan prompts (`plan-*.prompt.md`, `untitled-plan-*.prompt.md`): not queue prompts, do not claim.
- `docs/Frontend/ANALYTICS_NAVIGATION_AUDIT.md` (June 2026): the current menu is `navConfig.ts`.

## Remaining - needs an owner or a decision

| # | Finding | Owner / decision |
|---|---|---|
| 1 | `MASTER_ROADMAP.md` (1,034 lines) keeps many dated "current" sections (for example 2026-10-14 "Current product priority", "Next runnable analytics work") beside the live 2026-10-16 routing and the STAB17 P0 section. It needs consolidation into one live routing block plus a historical appendix. I did not edit it because STAB17 is IN_PROGRESS by another agent. | `agent-system` / roadmap owner, after the STAB17 handoff settles |
| 2 | `WorkerRuntimePolicyService` still returns "allow" for *any* exception (`Infrastructure/Services/WorkerRuntimePolicyService.cs:72`). An unknown state becomes "worker may run", which conflicts with the no-unknown-to-positive rule. This is code. | Ivan: decide fail-closed vs allow; then a code task |
| 3 | RQ679 (durable copy of queued Access upload) is WAITING on the production `Storage__Provider` value of the Render web and worker services. | Ivan (Render config) |
| 4 | 10 root legacy notes are not valid UTF-8 (legacy code page; for example `START_HERE.md`, `HITNA_POMOC.md`). They carry banners now. Either archive them under `docs/archive/` or convert them. Moving files breaks external links. | Ivan: archive vs keep |
| 5 | Unresolved references to files that never landed or were removed: `docs/qa/ADVANCED_V2_ANALYTICS_META_CONTRACT.md`, `docs/product/RETAIL_ANALYTICS_COPILOT_PRD.md`, `docs/analytics/SUPPLIER_SCORECARD_FORMULA.md`, `Database/Migrations/018_*.sql`, `029_*.sql`. Each is in a historical queue or audit, so the text was not rewritten. | queue owners on next touch |
| 6 | About 520 references to `.ai/task-locks/*.lock.md` in closed queue entries point to files that are deleted by design. A link checker must exclude them. There is no repository link checker; this audit used an ad-hoc scan. | `agent-system`: optional `check-doc-links` in governance |
| 7 | June 2026 audits in `docs/Analytics/` (`DAILY_SALES_UX_AUDIT`, `INVENTORY_UX_AUDIT`, `SECONDARY_ANALYTICS_SCREENS_AUDIT`, ...) and `docs/ai/ARCHITECTURE_BOUNDARIES.md` (2026-06-20) were not re-verified line by line. Treat them as dated snapshots until reviewed. | analytics docs owner |

Verified current, no change needed: `docs/Analytics/ANALYTICS_CACHE_POLICY.md` and `ANALYTICS_CACHE_INVALIDATION_AND_STALE_POLICY.md` (updated with PR #170/#171; TTLs match `AnalyticsCachePolicy.cs`), the RQ677/RQ673 queue entries, and the canonical stub `docs/Analytics/KPI_METHODLOGY_AUDIT.md`.
