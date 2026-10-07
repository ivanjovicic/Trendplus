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
- `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`
- `docs/ai/BACKEND_STANDARDS.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- `docs/ai/COMMIT_STANDARDS.md`
- `docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_TEMPLATES.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`

## Authority order when docs conflict

Do not use one global precedence list for both **facts** and **authority**. They are different:

1. **Implementation facts** — current code, focused tests, executable validators and synchronized run evidence prove what is actually implemented or passing.
2. **Queue/routing data** — `MASTER_ROADMAP.md` owns cross-program priority/current program pointer; the current owner queue owns task status, named owner, dependencies and the program's READY set.
3. **Mechanics/policy** — the canonical owner document for the rule controls how those facts/statuses may be interpreted or changed. For queue selection, claim, takeover, locks, no-READY recovery and close-out, that owner is **only** `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
4. **Entrypoints/summaries** — `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/AGENT_START_HERE.md` and this README summarize/route; they do not create competing mechanics.
5. **Historical evidence** — old ledgers, addenda, dated audits, stale "next READY" prose and interrupted command output never override current routing.

**Evidence does not grant authority.** A runtime commit on `main`, a green test, a missing local lock or an absent branch/PR may prove repository state, but none of them releases another owner's `IN_PROGRESS` claim or authorizes takeover. Only the queue protocol plus current routing/owner evidence can do that.

If a summary/helper doc disagrees with its canonical owner, update or deprecate the helper; do not merge both versions into a hybrid rule.

## Canonical owners by topic

- Repo-wide agent behavior and question threshold: `AGENTS.md`
- Cross-program priority/current program routing data: `MASTER_ROADMAP.md`
- Per-program task status/READY set/dependencies/named owner: the current owner queue named by `MASTER_ROADMAP.md`
- **All queue mechanics** (selection, claim, active-owner exclusivity, takeover, locks, Idle recovery, no-READY action ladder, Post-close cascade and Zero-READY proof): **`docs/ai/PROMPT_QUEUE_PROTOCOL.md`**
- Agent entry/orientation and read order: `docs/ai/AGENT_START_HERE.md`
- Architecture ownership and safe path boundaries: `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- Analytics/runtime semantics: `docs/ai/ANALYTICS_STANDARDS.md`, `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md` and `docs/ai/BACKEND_STANDARDS.md`
- Frontend presentation guardrails: `docs/ai/FRONTEND_UX_STANDARDS.md`
- Delivery evidence and honest completion: `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md`
- Validation selection by changed layer and risk: `docs/ai/VALIDATION_SELECTOR.md`
- Commit naming and commit-body expectations: `docs/ai/COMMIT_STANDARDS.md`
- UTF-8 and text-only repair workflow: `docs/ai/ENCODING_AND_TEXT_SAFETY.md`

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

## Queue recovery principle

When a queue has no current READY prompt, the expected behavior is proactive recovery, not early refusal. Follow the canonical protocol's **Mandatory no-READY action ladder**; this README intentionally does not restate the algorithm. A final no-work result must come from the protocol's current Zero-READY proof. Do not invent owner decisions, steal active claims or bypass production/security/tenant gates.

## Production and queue references

- Queue workflow: `MASTER_ROADMAP.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, owner queue named by the roadmap
- Historical queue ledger: `docs/ai/NEXT_PROMPT_QUEUE.md`. `NEXT_PROMPT_QUEUE.md` is a **historical ledger**, never the live router.
- GenAI gated queue: `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`
- Analytics roadmap: `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
- GenAI roadmap: `docs/ai/GENAI_COPILOT_ROADMAP.md`
- Production readiness: `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
