Task ID: 2026-09-20-reliability-contract-layer-prompts
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-20
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no PR
Main commit SHA: d9f43c6a
Main verification: pending final origin/main verification after push
Evidence state: synchronized after initial delivery commit; final remote verification pending

## What was done
- Added systemic Reliability Contract Layer prompts RQ359-RQ367 as `WAITING` entries.
- Covered shared async query lifecycle, reusable invariant tests, guardrail expansion, authoritative/derived provenance, Zod response validation, dataset projection separation, PostgreSQL migration/bootstrap smoke, non-growing guardrail baseline and machine-generated validation evidence.
- Corrected the live queue summary so completed RQ333-RQ336 are marked `DONE` and the round-3 intake distinguishes DONE RQ331-RQ344 from WAITING RQ345-RQ358.
- Updated `MASTER_ROADMAP.md` with the planning intake and the RQ345-RQ367 waiting range.
- Did not promote or claim any prompt; the analytics queue remains `Current READY prompt: none`.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-20-reliability-contract-layer-prompts-evidence.md

## Validation run
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass, 8 canonical files checked.
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 506 tasks checked.
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 planning tasks checked.

## Validation not run
- Runtime/frontend/backend tests -> not run; this task changes queue and roadmap planning only.
- Remote CI -> not inspected; repository policy does not require CI for docs-only queue intake before main delivery.

## Documentation impact
- Added the nine systemic prompts to the canonical analytics reliability queue and updated the canonical roadmap routing truth.
- Telemetry/Sentry/OpenTelemetry was explicitly routed to the Platform Evolution queue boundary rather than added as an unowned analytics runtime prompt.

## What was missed
- No runtime implementation was requested or performed; all new prompts remain WAITING.

## Risks
- The queue now contains both existing page-level follow-ups and a systemic reliability track; promotion must preserve the canonical one-READY rule and dependencies.

## Next
- Explicitly promote one safe prompt only when the owner chooses the next execution slice; recommended first systemic candidate is RQ359 after the current READY pointer is established.
