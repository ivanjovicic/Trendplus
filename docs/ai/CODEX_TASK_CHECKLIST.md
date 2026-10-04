# Codex Task Checklist for Trendplus

## Stop rules

Stop and report status if:

- source of truth is unclear
- the same command fails twice
- required proof is blocked and no narrower safe alternative from `VALIDATION_SELECTOR.md` can run
- the task spills into unrelated modules
- frontend and backend semantics are drifting apart
- route or lazy-import behavior is at risk
- you cannot tell whether the state should be empty, insufficient, or error

Do not continue blind.

## Architecture pre-flight

- [ ] I know the owning module, screen, route, endpoint, or worker.
- [ ] I know the source-of-truth endpoint/service/DTO.
- [ ] I found the existing shared component/helper/formatter first.
- [ ] I found the current tests or smoke coverage.
- [ ] I know which files should not be touched casually.

## Encoding pre-flight

- [ ] If I am touching Serbian UI/docs, I searched for mojibake first.
- [ ] I will not mix encoding cleanup with business logic unless the task explicitly requires both.
- [ ] I know whether the task needs a dedicated text-only commit.

## Pre task

- [ ] I know which screen or endpoint I am changing.
- [ ] I know whether the surface is recommendation, signal, or report.
- [ ] I know the backend owner of the state I am about to change.
- [ ] I checked existing helpers/components before creating new ones.
- [ ] I checked whether tests already exist.
- [ ] I planned a small patch.

## Analytics frontend checklist

- [ ] `AnalyticsTrustHeader` exists or is intentionally not needed.
- [ ] Error uses `AnalyticsErrorState`.
- [ ] Empty uses `AnalyticsEmptyState`.
- [ ] API failure does not show KPI zeros.
- [ ] `insufficient_data` does not look like a valid recommendation.
- [ ] Shared formatters are reused.
- [ ] No local recommendation/confidence scoring is added.
- [ ] Data quality and refresh/freshness are visible when relevant.
- [ ] No mojibake is introduced.
- [ ] Theme tokens are used instead of hardcoded colors.

## Analytics backend checklist

- [ ] Empty dataset uses empty meta.
- [ ] SQL, missing MV, missing table, or timeout path does not become fake zero.
- [ ] Fallback, partial, or stale data uses warning meta where appropriate.
- [ ] Error includes safe correlation context when available.
- [ ] Error logging is safe for long messages.
- [ ] `lastRefreshAtUtc` is not invented.
- [ ] `dataQualityStatus` is not fake `good`.

## Commit split checklist

- [ ] This change is docs-only, backend-only, frontend-only, test-only, or migration-only where possible.
- [ ] I am not mixing three or more unrelated concerns.
- [ ] If scope grew, I either split the work or documented the reason.

## Queue task checklist

- [ ] If this is a queue-driven task, I updated the queue status appropriately.
- [ ] I treated `Current READY` as the primary/default pointer, not as proof that every other READY task is blocked.
- [ ] If `Current READY` is `none`, I ran Idle recovery instead of stopping immediately.
- [ ] For every candidate blocker I classified: stale/satisfied, circular same-prompt artifact, repo-local proof, external/provider/deployed evidence, product/security/tenant authority, or active owner/path collision.
- [ ] I checked whether a prerequisite is circular (for example requiring a baseline before the prompt whose first job is to create that baseline).
- [ ] I did not use provider/live/CI proof needed only for final acceptance as a blanket start blocker for independently safe repo-local work.
- [ ] If a prompt mixed safe repo-local work with external final proof, I repaired/narrowed it before claim without changing business semantics or weakening acceptance.
- [ ] If the first candidate remained genuinely blocked, I checked another collision-safe lane in the same program and then the next eligible program before reporting no work.
- [ ] After any terminal queue transition, I refreshed the **post-delivery origin/main SHA**; I did not reuse the pre-claim routing result.
- [ ] I ran the **Post-close dependency cascade**: searched every active owner queue/addendum for the completed task ID and changed dependencies, re-evaluated all dependents, then scanned all non-terminal prompts for stale blockers.
- [ ] If any dependent/independent prompt became dependency-complete and collision-safe, I promoted it in the same recovery run instead of leaving it WAITING behind stale prose.
- [ ] If multiple READY tasks exist, I selected one whose dependency, feature-family, path, owner and gate collision checks are clear; this agent/workspace still owns only one claimed prompt at a time.
- [ ] I did not copy an older queue header, run-log `Next: none`, blocked P0, pending CI or missing deployed proof into a new zero-READY conclusion.
- [ ] A final "no safe task" report has a **Zero-READY proof**: recovery-base SHA, active queue/addendum files scanned, every plausible candidate checked, blocker class, start-gate-vs-final-proof classification, why no repo-local slice is safe, and the exact unblock event.
- [ ] If I could not inspect the entire active owner queue/addendum set, I reported recovery incomplete and did **not** claim there was no READY work.
- [ ] I added evidence: date, files changed, checks, risks, next step.
- [ ] I did not execute an additional queue task unless explicitly instructed.

## Commands

Select the smallest applicable checks through `docs/ai/VALIDATION_SELECTOR.md`. The commands below are common defaults, not an instruction to run every suite for every patch.

Frontend:

```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run test -- --run src/components/__tests__/WorkersPanel.spec.tsx
npm run build
```

Use `--run` for targeted Vitest execution so the process does not stay in watch mode.

Backend:

```powershell
dotnet build
dotnet test
```

Migrations:

```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

## Final report template

```text
Changed:
- ...

Checks:
- dotnet build: pass/fail/not run
- dotnet test: pass/fail/not run
- npm run check:analytics-guardrails: pass/fail/not run
- npm run build: pass/fail/not run

Not done:
- ...

Risks:
- ...

Next:
- ...
```
