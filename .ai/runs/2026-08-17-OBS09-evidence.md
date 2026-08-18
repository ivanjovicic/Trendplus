Task ID: OBS09
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-08-17
Agent/tool: Cursor Auto
Delivery target: main
Working branch / PR: main (uncommitted)
Main commit SHA: pending
Main verification: pending - implementation is local until the owner commits
Evidence state: pending

## What was done
- Added `WorkerSlaEvidenceMapper` that projects `/api/workers/health` onto OBS08 field ids.
- Uninstrumented W5/W6/last-success stay null with warning codes. Empty inventory and global pause stay unknown, not healthy zeros.
- Additive `SlaEvidence` on `GET /api/workers/health`. Existing counts unchanged. No alerting.

## Files changed
- Infrastructure/Services/WorkerSlaEvidenceMapper.cs
- Api/Endpoints/AllEndpoints.cs
- Api.Tests/WorkerSlaEvidenceMapperTests.cs
- docs/qa/OBSERVABILITY_WORKER_SLA_EVIDENCE_CAPTURE_2026-08-17.md
- docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
- docs/roadmaps/OBSERVABILITY_ROADMAP.md
- MASTER_ROADMAP.md
- .ai/runs/2026-08-17-OBS09-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~WorkerSlaEvidenceMapperTests` - pass (4/4)
- `node scripts/check-agent-instructions.mjs --self-test` - pass
- `node scripts/check-agent-instructions.mjs` - pass
- `node scripts/check-prompt-queues.mjs --self-test` - pass
- `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
- `node scripts/check-planning-architecture.mjs --self-test` - pass
- `node scripts/check-planning-architecture.mjs` - pass (68 new planning tasks)
- `git diff --check` on OBS09 paths - pass

## Validation not run
- full `dotnet test` suite - focused mapper proof is in scope
- npm frontend checks - backend capture only

## Documentation impact
- OBS09 DONE. OBS Current READY is none. Sequential refill pointer moves to SEC07.

## What was missed
- Per-worker runtime policy pause is not on the snapshot.
- Queue depth and last successful run remain uninstrumented by design.

## Risks
- Legacy health counts can still look green; operators must read `SlaEvidence` for honesty.

## Next
- SEC07 - Frontend production dependency vulnerability triage
