Task ID: QUEUE-GUIDANCE
Queue: MASTER_ROADMAP.md / docs/ai/PROMPT_QUEUE_PROTOCOL.md / docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
Date: 2026-08-19
Agent/tool: Codex / shell
Model: GPT-5
Delivery target: main
Main commit SHA: 72e6072d3597045489c77b40c51abee16f692347
Main verification: git rev-parse HEAD -> 72e6072d3597045489c77b40c51abee16f692347

## What was done
- Added canonical queue guidance that tells agents not to claim a later `WAITING` prompt when the owner queue header says `Current READY prompt: none`.
- Added the same no-ready warning to the data-source connector queue header so the instruction is visible at the owner queue itself.
- Restored the durable QDB05 evidence file to main so the prior mapping-preview work remains recorded in repository history.
- Verified that the live master roadmap already reflects `QDB06`/`QDB07` as non-runnable, so no roadmap override was needed in the final commit.

## Files changed
- docs/ai/PROMPT_QUEUE_PROTOCOL.md
- docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md
- .ai/runs/2026-08-19-QDB05-evidence.md

## Validation run
- git diff --check -> pass
- node scripts/check-agent-instructions.mjs --self-test -> pass
- node scripts/check-agent-instructions.mjs -> pass
- node scripts/check-prompt-queues.mjs --self-test -> pass
- node scripts/check-prompt-queues.mjs -> pass
- node scripts/check-planning-architecture.mjs --self-test -> pass
- node scripts/check-planning-architecture.mjs -> pass

## Validation not run
- dotnet build - not applicable; docs-only routing update.
- dotnet test - not applicable; docs-only routing update.
- npm run check:analytics-guardrails - not applicable; docs-only routing update.
- npm run build - not applicable; docs-only routing update.

## What was missed
- Historical evidence files elsewhere in the repo still contain older QDB03/QDB05 references; they were left intact because they are historical rather than live routing.

## Risks
- The queue now routes correctly, but stale historical notes can still confuse a casual reader if they ignore the live header and master roadmap.
- QDB06 remains waiting on owner approval, so there is still no current READY QDB task.

## Next
- QDB06 owner approval, then QDB07 promotion when the release/auth gates permit it
