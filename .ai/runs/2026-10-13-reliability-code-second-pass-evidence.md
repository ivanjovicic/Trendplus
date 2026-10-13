# Trendplus reliability code second pass — 2026-10-13

Task ID: DIRECT-RELIABILITY-SECOND-PASS-2026-10-13
Queue: direct user request, with new RQ637 and expanded RQ634 (not claimed)
Agent/tool: ChatGPT GitHub connector
Audit base: `23c8c1765d7a161f465094db46f4bbc5da3815a1`
Working branch: `fix/2026-10-13-supplier-diagnostics-reliability`
Merge commit SHA: `99ba3baed3af0c316cd68dc830342114660bf124` (PR #135 squash)
Main verification: passed — fresh `main` ref returned exact merge SHA above
Evidence state: synchronized
Ownership transfer: none

## Verified post-PR134 state

- No newer `main` implementation commits at audit start; PR #134 merged into the audited tip. PR final head workflows `38351086653` backend and `38351086632` quality gates are now **success**; `38351086583` governance and exact-merge `38351175549` governance also **success**.
- User-provided Render log, Oct 12 16:50 UTC: missing supplier 90d and 180d MVs, SQL029 startup invocation, /ready/API 503s; **no SQL completion/outcome or readiness JSON supplied**. Browser-tool `/health` and `/ready` requests from this assistant environment returned `not accessible`, NOT an HTTP 503 or HTTP 200. Do not assert live recovery/outage.
- Independent application startup paths remain: `BuildSupplierDecisionHubObjectsAsync(...allowHeavyRefresh:false)` can execute expensive SQL018 cache CREATE with **CommandTimeout=0** when missing; PR #133 only guards SQL029 windowed creation.
- Production `Api/appsettings.Production.json`: `AccessImport:RegisterWorkerInWebProcess=true`, `WorkerEnabled=true`, polling interval 2s; `AccessImportBackgroundWorker.ExecuteAsync` performs stale-batch recovery and queue polling before any startup-readiness gating. Supplied logs show polling concurrent with startup SQL. Causality for API 503 not independently proven.
- `AnalyticsConnectionDiagnosticsHostedService` previously checks only core/all-time supplier tables/views; its optional supplier 90d/180d status was invisible. `PostgresMaterializedViewCapabilityReader.InspectAsync` already encodes required-column/populated/existence semantics.

## Changes delivered in this PR

1. Improve diagnostics to inspect supplier scorecard 90d/180d MVs with the canonical capability checker after releasing the first reader. Missing/incorrect/unpopulated MVs emit explicit warning, with no attempt to repair and no change to the core retry/not-ready decision. Add 4 xUnit cases for the new diagnostic status helper.
2. Expand **RQ634** for the previously overlooked SQL018 all-time populated CREATE/command timeout, without touching SQL029 or promoting an untested production setting.
3. Add **RQ637** as a separate P1 READY task for nonblocking web Access-import worker waiting on required database readiness before stale recovery/queue claims, with dedicated-worker isolation and lifecycle tests. RQ636 remains test-factory-only and independent.
4. Register **RQ638** (P2) for the confirmed MediatR PerformanceLog VARCHAR(200/500/4000/2000) bounds gap. In the current implementation, RequestType and exception text are unbounded, and a JSON serialization failure returns unbounded `obj.ToString()`; this is separate from RQ631 HTTP middleware and RQ636 missing-table test noise.
5. Refresh `MASTER_ROADMAP.md`, STAB16, curated audit, and this evidence. RQ601/RQ602 still PARTIAL, RQ610 BLOCKED, STAB16 BLOCKED on effective provider evidence. Do not repeat batch #23 or run live SQL.
6. No edits to `AccessImportBackgroundWorker.cs`, `Database/Migrations/018_*.sql`, `029_*.sql`, production worker config, or persistent production data in this review. Those changes need focused PostgreSQL proofs under queued RQ634/RQ637.

## Verification and residuals

- PR exact-head SHA `0639ea52bf2d98044d47b187f9a76952775eec08`: backend workflow `38367308954` **SUCCESS** (2,002 passed, 42 classified skips, zero failures), including all four `AnalyticsConnectionDiagnosticsCapabilityTests`, and RQ453 certification; frontend quality gates `38367309021` **SUCCESS**; Planning Governance `38367309107` **SUCCESS**. PR #135 was squash-merged on `99ba3baed3af0c316cd68dc830342114660bf124`; fresh `main` ref matched. No local dotnet/Node runtimes in GitHub connector path, so no local build or xUnit execution claimed.
- Existing canonical supplier capability reader has PostgreSQL tests elsewhere. Four new focused unit cases passed within the full backend workflow; a full integration proof of emitted diagnostic log values after real PostgreSQL startup is **not claimed**.
- Missing effective Render config, deployed runtime SHA, final SQL018/029 outcome, /ready JSON, actual Neon view state, dedicated worker status and Vercel dashboard. Remain STAB16 external acceptance.

## Next

Exact PR CI and main SHA synchronized. RQ634/RQ635/RQ636/RQ637/RQ638 remain READY for their own independent acceptance, not DONE for this direct-request slice. Confirm fresh ownership/locks before the next agent claims RQ634 primary or the independent RQ637 P1 lane.
