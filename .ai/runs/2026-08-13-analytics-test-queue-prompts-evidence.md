Task ID: analytics-test-queue-prompts
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
Date: 2026-08-13
Agent/tool: Cursor Grok 4.6
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: not run (docs/queue only; user did not request commit or push)

## What was done
- Wrote canonical analytics test strategy: contract tests for named failure modes, not page coverage.
- Added owner-promoted WAITING prompts RQ100-RQ104 for Decision Board/PDC, inventory null evidence, sales period/empty/scope, action outcome/learning, and frontend backend-truth display.
- Added P-UI-20 WAITING for grouped ErrorState/EmptyState/TrustHeader proof on stats pages, without changing P-UI-19 READY.
- Registered the new RQ addendum in `scripts/check-prompt-queues.mjs` and aligned MASTER/RQ/P-UI indexes. Did not promote RQ100 to READY.

## Files changed
- docs/ai/ANALYTICS_TEST_STRATEGY.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md
- docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
- docs/ai/AGENT_START_HERE.md
- docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md
- MASTER_ROADMAP.md
- scripts/check-prompt-queues.mjs

## Validation run
- git diff --check: pass after removing trailing whitespace on the P-UI roadmap header
- node scripts/check-agent-instructions.mjs --self-test: pass
- node scripts/check-agent-instructions.mjs: pass
- node scripts/check-prompt-queues.mjs --self-test: pass
- node scripts/check-prompt-queues.mjs: pass (257 tasks)
- node scripts/check-planning-architecture.mjs --self-test: pass
- node scripts/check-planning-architecture.mjs: pass

## Validation not run
- dotnet build / test: docs/queue only
- npm run check:analytics-guardrails / npm run build: docs/queue only

## What was missed
- Runtime tests were not implemented; only precise queue prompts and the strategy doc.
- RQ100 remains WAITING so it does not displace STAB11 / QDB03 / P-UI-19.

## Risks
- An agent could still start RQ100 without owner promotion if it ignores MASTER_ROADMAP.md.
- Existing stats-page premium specs mock TrustHeader; P-UI-20 must add real mount proof rather than treat those mocks as coverage.

## Next
- Owner promotes RQ100 when STAB11 / QDB03 are not exclusive.
- After P-UI-19 DONE, promote P-UI-20.
