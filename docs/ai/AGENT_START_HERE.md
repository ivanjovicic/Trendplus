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

## Why analytics bugs keep appearing

Most recurring bugs come from one of these patterns:

1. Fallback values are treated as real evidence.
   - `null -> 0`, missing status -> `good`, missing severity -> `info`, or unknown boolean -> `false`.
2. The same number has different meanings on different surfaces.
   - ratio vs percent, revenue vs expected impact, returned count vs total count.
3. Local UI helpers re-create backend business semantics.
   - frontend scoring, fallback recommendations, action impact inference.
4. List/detail/export/action metadata drift apart.
   - table uses one filter or unit, detail/export/action uses another.
5. Date and denominator contracts are implicit.
   - inclusive end vs whole day, visible rows vs all filtered rows, closed actions vs measured actions.
6. Agents optimize one screen without checking downstream readers.
   - chart fixed, but CSV/detail/report/action queue still wrong.

The fix is not “more code”. The fix is stricter contracts, smaller prompts, and tests that cover true-zero vs unknown.

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
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- `docs/ai/GENAI_COPILOT_ROADMAP.md`
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`

For analytics reliability tasks, treat `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` as the routing/index document. Do not read every queue addendum unless the index says the prompt has a read-together dependency.

For GenAI, RAG, LLM, agent, MCP or analytics-copilot tasks, read the four GenAI documents above in order. Use `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` only after the canonical analytics queue has no earlier TODO, PARTIAL or BLOCKED item. Do not skip GAI01 or another P0 gate to start provider integration, tool calling or UI work.

## The ten non-negotiables

1. No fake zero
   Backend failure or missing evidence must never look like valid `0 RSD`, `0 kom`, or `0%`.
2. No fake green
   Missing, stale, fallback, partial or insufficient data must never look like `good`, `healthy`, `fresh`, `normal`, `maintain`, or `measured`.
3. Backend source of truth
   Backend computes recommendations, confidence, reliability, reason codes, expected impact, and data quality semantics.
4. No frontend-invented confidence or recommendations
   Pages must not create local scoring thresholds or fake decision labels.
5. Impact vocabulary must stay strict
   `expectedImpactRsd` is actionable impact. Use `potentialExposureRsd`, `contextRevenueRsd`, or `estimatedValueRsd` when the value is not actionable expected impact.
6. Units must be explicit
   Every percent/share/rate must say whether it is ratio `0.35` or percent unit `35`.
7. Counts must be explicit
   Do not label returned rows as total matching rows. Use `returnedCount`, `totalMatchingCount`, or visible truncation labels.
8. Date ranges must be explicit
   Date-only UI filters should use half-open whole-day semantics unless a task states otherwise.
9. Surface parity is required
   API, table, chart, detail, CSV/XLSX/PDF/report, and action payload must agree or clearly document why not.
10. UTF-8, no mojibake
    Serbian Latin text must preserve `č ć š đ ž`. If text is corrupted, fix text safely and keep logic out of that commit.

## Analytics safety gate before coding

Before changing analytics code, write the answers in local notes or the prompt result:

```md
Analytics safety gate:
- Source of truth:
- Contract changed? yes/no
- Unit/denominator:
- True zero case:
- Missing/unknown case:
- No-baseline case:
- Freshness/fallback case:
- Surfaces affected:
- Tests that prove table/detail/export/action parity:
- Stop condition hit? no / details
```

If any line cannot be answered, do not implement the runtime fix. Add docs/tests or mark the prompt `BLOCKED`/`PARTIAL`.

## Task workflow

1. Identify the owning screen, route, endpoint, or worker.
2. Identify the source-of-truth service, DTO, or endpoint.
3. Find the shared helper, component, formatter, or response-meta utility before creating anything new.
4. Find existing tests and route smoke coverage.
5. Run the analytics safety gate.
6. Make the smallest safe patch.
7. Run the exact checks required by the task.
8. Update queue/docs when the task is queue-based or changes canonical behavior.

## Stop rules

Stop and report status if:

- source of truth is unclear
- the task spills into unrelated modules
- migration context is unclear
- the same command fails twice
- route or lazy-import behavior is at risk
- mojibake is found and the change is turning into mixed text-plus-logic cleanup
- a missing value would become zero/good/fresh/normal/measured
- a frontend helper would have to invent backend business semantics
- a change fixes table display but leaves detail/export/action payload inconsistent

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
- Do not let action/outcome summaries call something measured unless measurement evidence exists.
- Do not let report/export values use a different unit than the on-screen table.
