# Trendplus Validation Selector

Updated: 2026-08-11
Repo: `ivanjovicic/Trendplus`

Use this document to choose the smallest executable proof that matches the changed behavior. Validation is selected from the owning layer, changed paths and named failure modes; routine validation choice does not require a user question.

## Automatic selection rule

1. Start with the nearest reproducer, focused test or validator for the changed behavior.
2. Add a changed-project build or static check when compilation, typing, routing or shared contracts may be affected.
3. Add a wider suite only when a shared contract, several projects/surfaces or release readiness is explicitly in scope.
4. If a check fails, classify it as product, test/harness, dependency/environment, documentation or CI failure before editing again.
5. Allow one cause-changing retry. Do not repeat an unchanged failing command.
6. Never patch runtime code merely to hide an environment, harness or CI-infrastructure failure.
7. Record every executed and skipped check honestly. A file existing, a test compiling or CI being queued is not passing runtime proof.

For a known runtime bug, run the intended focused test before the production edit when practical. A failure on the expected behavioral assertion is useful reproducer evidence. Compilation, setup, timeout or unrelated baseline failure is unavailable proof, not a successful reproducer.

## Common proof ladder

```text
current contract or reproducer
-> nearest focused test and counterexample
-> changed-project build/static check
-> mapped documentation/governance checks
-> wider suite only for a named wider risk
```

## Documentation and agent instructions

For ordinary docs-only changes:

```text
git diff --check
```

Also verify referenced paths and current owner links. When agent/development instructions or their validator change, run:

```text
node scripts/check-agent-instructions.mjs --self-test
node scripts/check-agent-instructions.mjs
```

Do not run .NET or frontend builds for a docs-only change unless the task also changes executable examples, generated outputs or runtime configuration.

## Queue and planning changes

Run all governance checks when a live queue, roadmap, router, planning owner or governance validator changes:

```text
node scripts/check-agent-instructions.mjs --self-test
node scripts/check-agent-instructions.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-prompt-queues.mjs
node scripts/check-planning-architecture.mjs --self-test
node scripts/check-planning-architecture.mjs
```

A historical snapshot banner or typo-only change outside live routing may use the instruction check plus the specifically affected governance validator, with wider checks recorded as skipped and why.

## React and analytics UI

Start with the nearest non-watch Vitest spec:

```text
cd Klijent/clientapp
npm run test -- --run <path-to-spec>
```

Add the analytics guardrail check when analytics pages, shared analytics helpers, trust/error/empty states, formatters, routing or export/report behavior changes:

```text
npm run check:analytics-guardrails
```

Add `npm run build` when shared types, imports, routes, build configuration or multiple frontend modules changed. A route smoke test does not replace browser/live evidence when URL refresh, API loading or deployed behavior is part of acceptance.

## .NET API, application and infrastructure

Start with the nearest test project, test class or filtered test that owns the behavior. Then build the changed project or backend solution when public DTOs, dependency injection, endpoint registration, EF mappings or shared services changed.

Use the repository-level defaults only when a narrower owner is unknown or the change intentionally spans the backend:

```text
dotnet build Trendplus2.Backend.slnf
dotnet test Trendplus2.Backend.slnf --no-build
```

If `--no-build` is invalid because the matching build did not complete, use `dotnet test Trendplus2.Backend.slnf`. Do not treat restore/network failure as a product regression.

## Workers, refresh and scheduled jobs

Run the nearest worker/service test and the owning project build. Add API/UI checks only when status contracts, manual-trigger behavior or user-visible freshness semantics changed. Prove web-versus-worker ownership, retry/failure state and stale/unknown behavior where relevant.

Long-running import, backfill, materialized-view refresh or scheduler execution is not a default local validation step. Use a bounded fixture/integration test or record the required environment evidence as not run.

## Analytics contracts and reports

Use `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md` before runtime edits. Focused proof should distinguish:

- true zero from missing/unknown/error;
- empty from insufficient data;
- fresh from stale/partial/fallback;
- ratio from percent unit and returned count from total count;
- API/table/chart/detail/export/report/action parity for every changed value.

Backend tests prove business semantics; frontend tests prove safe presentation. One does not substitute for the other when both surfaces changed.

## Migrations and data-bound changes

In addition to focused tests/builds, inspect the exact migration and run the relevant context command:

```text
dotnet ef migrations list --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj --context AnalyticsDbContext
```

Do not apply production migrations, mutate customer data or infer live success from a local migration listing.

## Full-suite triggers

Run a wider backend/frontend suite only when at least one is true:

- the task explicitly owns release or CI readiness;
- a shared DTO/helper/routing/DI contract affects several modules;
- targeted checks reveal a wider regression;
- migrations or cross-surface analytics semantics changed broadly;
- several high-risk owner layers changed in one accepted scope.

If the full suite is skipped, state the reason. Focused proof is sufficient for a focused change when it covers the changed contract and selected failure mode.
