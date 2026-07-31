# Trendplus AI Standards README

## Start here

After `AGENTS.md` and `.github/copilot-instructions.md`, read:

1. `docs/ai/AGENT_START_HERE.md`
2. `docs/ai/CODEX_TASK_CHECKLIST.md`
3. task-specific standards and module docs

## Canonical doc map

- `AGENTS.md`
- `.github/copilot-instructions.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/CODEX_TASK_CHECKLIST.md`
- `docs/ai/COMMON_FAILURES_AND_FIXES.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/BACKEND_STANDARDS.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- `docs/ai/COMMIT_STANDARDS.md`
- `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md`
- `docs/ai/PROMPT_TEMPLATES.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`

## Architecture and boundaries

Use `docs/ai/ARCHITECTURE_BOUNDARIES.md` to identify:

- the owning backend layer
- the owning frontend layer
- shared UI/helpers
- module tests
- routes and endpoints that should not be changed casually

## Encoding and text safety

Use `docs/ai/ENCODING_AND_TEXT_SAFETY.md` before editing Serbian UI copy or docs.

That doc covers:

- UTF-8 expectations
- mojibake search patterns
- safe text-only fix protocol
- future encoding guardrail plan

## Common failure playbook

Use `docs/ai/COMMON_FAILURES_AND_FIXES.md` when you see recurring failures such as:

- fake zero / fake green
- empty vs error confusion
- route lazy-import test breakage
- stale Vercel bundle
- protected write fake success
- frontend recomputing backend business decisions

## GenAI / Retail Analytics Copilot

GenAI work is a separate, gated track. It must not bypass the existing analytics, security or source-of-truth standards.

Read in this order:

1. `docs/ai/GENAI_COPILOT_ROADMAP.md`
2. `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
3. `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
4. `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`

Important:

- Plans and provider-free evaluation work are safe to start now.
- Real business data, public AI routes, MCP, tool calling and customer pilots remain gated.
- The first implementation is read-only.
- Existing image embeddings and future text RAG must remain separate until an audit proves the runtime and schema boundaries.
- Core Trendplus analytics must work when every GenAI component is disabled.

## When to update docs

Update docs when:

- a failure repeats
- architecture changes
- queue status changes
- a new module becomes source of truth
- a deploy or ops mistake becomes a recurring pattern

## Do not duplicate standards

- Keep canonical rules in one detailed doc and link to it from lighter docs.
- `AGENTS.md` and `.github/copilot-instructions.md` should stay short and point to canonical docs.
- Prefer updating the central doc over creating a contradictory duplicate.

## Production and queue references

- Queue workflow: `docs/ai/NEXT_PROMPT_QUEUE.md`
- GenAI gated queue: `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`
- Analytics roadmap: `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
- GenAI roadmap: `docs/ai/GENAI_COPILOT_ROADMAP.md`
- Production readiness: `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
