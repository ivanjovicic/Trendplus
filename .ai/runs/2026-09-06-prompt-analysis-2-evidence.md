# 2026-09-06 - Prompt analysis pass #2

Owner: direct-user-request (Ivan)
Task: Re-scan repository for undocumented potential bugs and seed new prompts.
Date: 2026-09-06

Files read (high level):
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- Infrastructure/Services/EmbeddingService.cs
- EmbeddingService/app.py
- Api/Endpoints/AllEndpoints.cs
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- multiple Api/Endpoints/* and Infrastructure/Services/* raw SQL call sites

Summary of findings:
1. PythonEmbeddingService client usage and file-safety gaps (base URL validation, unbounded file read, path-safety) — seeded RQ173 (supplement file).
2. Raw SQL call sites inventory and parameterization/timeouts risk — seeded RQ174 (supplement file).
3. Admin API-key equality/rotation and timing-safe compare gap — seeded RQ175 (supplement file).

Actions:
1. Added `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NEW_ADDITIONS_2026-09-06.md` with RQ173, RQ174, RQ175 (status WAITING).
2. Created this evidence run log.
3. Committed and will push changes to `main`.

Next steps for owners:
- Platform: review RQ173; add typed options guard and CI stub for embedding client.
- Backend/DB: run raw-SQL inventory and remediate risky patterns flagged by RQ174.
- Security: adopt timing-safe compare and rotation runbook for admin key (RQ175).

