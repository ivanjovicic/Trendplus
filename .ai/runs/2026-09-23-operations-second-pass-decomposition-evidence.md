Task ID: RQ408
Queue: direct-user-request -> docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-23
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

- Re-read the Operacije menu routes and performed a second static pass over the frontend pages, analytics services, inventory components, cached inventory endpoints and existing focused tests.
- Added `RQ408` as a WAITING analysis/decomposition prompt with 49 numbered findings (`OP2-01` through `OP2-49`), grouped by cross-screen contract, Supplier Sales, Inventory, Supplier Footwear, Shoe Type/Color, Daily Sales and Pre/Post/Pre-Nivelacija.
- Explicitly linked overlapping findings to existing owners `RQ371`-`RQ407` so the downstream agent can classify duplicates instead of opening competing implementation lanes.
- Kept the current READY pointer as `none`; no runtime code, schema, production data or READY state was changed.

## Files changed

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-23-operations-second-pass-decomposition-evidence.md`

## Validation run

- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass (12 canonical files checked)
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (547 tasks)
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass (78 new planning tasks checked)
- `git diff --check` -> pass

## Validation not run

- Product/runtime tests -> not run; this task only records findings and creates a downstream analysis prompt, with no product-code change.
- Live browser/database/deployed verification -> not run; the new prompt records these as downstream evidence requirements where needed.
- Remote CI -> not inspected; no named remote check is part of this documentation-only acceptance.

## Documentation impact

- Updated the canonical analytics reliability queue with `RQ408` and its finding catalogue.
- Updated `MASTER_ROADMAP.md` with the new WAITING queue owner entry.
- No historical audit document was rewritten; the prior `RQ406`/`RQ407` record remains intact.

## What was missed

- The static catalogue intentionally does not prove that every candidate is a runtime defect. Classification, reproduction and individual prompt creation remain the downstream `RQ408` task.

## Risks

- Some findings overlap existing prompts and must be deduplicated by the next owner before any implementation begins.
- Live/deployed data, real browser behavior and cross-screen numeric reconciliation remain unproven until `RQ407`/`RQ408` follow-up work runs.
- The repository had an unrelated untracked `.codex-remote-attachments/` directory; it was preserved and not included.

## Next

- Downstream owner: execute `RQ408` while preserving `Current READY prompt: none`; classify `OP2-01` through `OP2-49` and create only confirmed, collision-safe WAITING follow-up prompts.
