# BCI07 evidence log

Prompt: BCI07 - Admin requeue enqueue contract for demo verification
Date: 2026-08-11
Status: DONE

Root cause:
- Demo verification TestHost evaluated Guid.NewGuid() inside AddDbContext options lambda
- Each scoped TrendplusDbContext got a fresh empty InMemory database
- Seeded failed batch was invisible to RequeueBatch (HTTP 200 + success:false "Batch not found", enqueue count 0)

Fix:
- Capture InMemory database name once for the host lifetime
- Assert RequeueResponse.Success in the allow-path test

Changed files:
- Api.Tests/DemoEnvironmentVerificationEndpointTests.cs
- docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
- MASTER_ROADMAP.md

Checks:
- DemoEnvironmentVerificationEndpointTests.RequeueBatch* - pass (3/3)
- DemoEnvironmentVerificationEndpointTests - pass (13/13)

Next:
- BCI05 READY (resume GHA / full-suite evidence)
