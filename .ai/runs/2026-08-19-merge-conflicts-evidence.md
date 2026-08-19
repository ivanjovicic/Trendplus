Task ID: merge-conflicts
Queue: repository merge resolution
Date: 2026-08-19
Agent/tool: Codex / shell
Delivery target: main
Main commit SHA: pending
Main verification: pending

## What was done
- Resolved merge conflicts in `MASTER_ROADMAP.md`, the queue/roadmap planning docs, and the BCI/QDB decision docs.
- Merged `SourceMappingPreviewService.cs` back into a single service class with both the instance preview flow and the static helper API used by admin endpoints and tests.
- Added a compatibility overload to `SqlServerSourceDataSession` so existing one-argument callers still compile while the logger-based proof connector remains intact.
- Preserved the no-ready routing guardrail in `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## What was missed
- Validation is still pending.
- Commit and push are still pending.

## Risks
- The repository already contains a very large staged change set, so any validation failure may be unrelated to the conflict resolution itself.
- `SourceMappingPreviewService.cs` and `SqlServerSourceDataSession.cs` should be compiled and tested before commit because they were manually merged.

## Next
- Run the relevant diff, build, and test checks.
- Commit the merge resolution.
- Push the result to `main`.
