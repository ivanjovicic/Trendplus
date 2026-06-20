# Agent Start Here

Read this after `AGENTS.md` and `.github/copilot-instructions.md`.

## Repo mission in 10 lines

- Trendplus is a retail decision-support product, not just a dashboard collection.
- Analytics must help operators decide what to do this week.
- Every serious analytics surface should explain period, freshness, data quality, reason, and next action.
- Empty is not the same as error.
- Unknown is not the same as zero.
- Stale or partial data must stay visible.
- Backend business decisions stay in backend contracts.
- Frontend should present, format, filter, and guide; it should not invent confidence or recommendations.
- Pilot safety matters more than flashy UI.
- Small, evidence-based changes beat broad rewrites.

## Read order

1. `AGENTS.md`
2. `.github/copilot-instructions.md`
3. `docs/ai/AGENT_START_HERE.md`
4. `docs/ai/CODEX_TASK_CHECKLIST.md`
5. Task-specific standards and module docs

Useful next documents:

- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `docs/ai/COMMON_FAILURES_AND_FIXES.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/BACKEND_STANDARDS.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md`

## The five non-negotiables

1. No fake zero
   Backend failure must never look like valid `0 RSD`, `0 kom`, or `0%`.
2. Backend source of truth
   Backend computes recommendations, confidence, reliability, reason codes, and data quality semantics.
3. No frontend-invented confidence or recommendations
   Pages must not create local scoring thresholds or fake decision labels.
4. UTF-8, no mojibake
   Serbian Latin text must preserve `č ć š đ ž`. If text is corrupted, fix text safely and keep logic out of that commit.
5. Small scoped commits
   Prefer docs-only, backend-only, frontend-only, test-only, or migration-only commits. Avoid mixing unrelated concerns.

## Task workflow

1. Identify the owning screen, route, endpoint, or worker.
2. Identify the source-of-truth service, DTO, or endpoint.
3. Find the shared helper, component, formatter, or response-meta utility before creating anything new.
4. Find existing tests and route smoke coverage.
5. Make the smallest safe patch.
6. Run the exact checks required by the task.
7. Update queue/docs when the task is queue-based or changes canonical behavior.

## Stop rules

Stop and report status if:

- source of truth is unclear
- the task spills into unrelated modules
- migration context is unclear
- the same command fails twice
- route or lazy-import behavior is at risk
- mojibake is found and the change is turning into mixed text-plus-logic cleanup

## Final report format

Use this structure:

```text
Changed:
- ...

Checks:
- ...

Not done:
- ...

Risks:
- ...

Next:
- ...
```

## Quick reminders

- Do not replace lazy/Suspense routing just to satisfy tests.
- Do not bypass shared formatters or analytics meta helpers.
- Do not hide stale, partial, fallback, or insufficient-data states.
- Do not turn docs drift into architecture drift; update canonical docs when a rule repeats.
