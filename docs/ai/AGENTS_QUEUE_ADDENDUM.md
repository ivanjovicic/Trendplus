# Deprecated AGENTS Queue Addendum

Status: **DEPRECATED — compatibility pointer only**

This file used to duplicate the live queue workflow for older `AGENTS.md` variants. Do **not** use it as a router, checklist, selector or source of claim/takeover/lock/no-READY rules, and do not copy its historical workflow into new agent instructions.

Current canonical sources:

- repository-wide behavior: `AGENTS.md`
- entry/read order: `docs/ai/AGENT_START_HERE.md`
- cross-program routing data: `MASTER_ROADMAP.md`
- per-program task status/owner/READY data: the current owner queue
- **all queue mechanics:** `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- evidence contract: `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md`

Critical invariants retained here only so old references fail safely:

- another owner's `IN_PROGRESS` task remains exclusive through its close-out phase; a commit on `main`, missing local lock, or absent branch/PR does not release it;
- when there is no READY prompt, use the canonical **Mandatory no-READY action ladder** before any Zero-READY conclusion;
- never infer current routing from `docs/ai/NEXT_PROMPT_QUEUE.md`; it is a historical ledger.

If an older document points here for operational instructions, follow the canonical files above and update that old reference when it is part of the current task.
