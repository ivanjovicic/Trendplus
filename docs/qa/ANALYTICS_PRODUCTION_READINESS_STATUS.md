# Analytics Production Readiness Status

> This document is generated from the machine-readable snapshot. It is not a hand-maintained global release verdict.
> A missing, expired, SHA-mismatched or externally blocked proof is intentionally not rendered as PASS.

- Snapshot generated: 2026-10-01T09:40:27.487Z
- Repository SHA: `b01ac6872b38acae4c7677989fa759974389cc2e`
- Deployed SHA: `3a6a6886ca2c6d0d9e5924dfbce4f7788409f427`
- Overall state: **failed**
- Schema/contract/context generations: `{"schema":"operations-analytics-integrity-schema-v1","contract":"analytics-readiness-contract-v1","context":"analytics-context-fingerprint-policy-v1"}`
- Expiry policy: Code evidence: 7 days. Browser/provider/runtime evidence: 24 hours unless a source-specific expiry is shorter.

## Evidence by family

| Family | State | Evidence | Checked SHA | Checked at | Expires | Limitation |
| --- | --- | --- | --- | --- | --- | --- |
| readiness-tooling | **stale-evidence** | .ai/runs/2026-09-30-RQ514-readiness-validation.json | `d393c63695f7f28a1d77c32b71c1de1c6b8d36ad` | 2026-09-30T07:52:06.607Z | 2026-10-07T07:52:06.607Z | Code-level tooling proof does not imply deployed or browser proof. |
| frontend-runtime | **stale-evidence** | docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md (historical evidence reference) | `817964c63560eb7f442b8c57b0099544d8667a97` | 2026-06-19T17:20:00.000Z | 2026-06-20T17:20:00.000Z | Retained historical live evidence; it is not current release proof. |
| provider-runtime | **blocked** | Current run environment | `unknown` | unknown | unknown | No connected browser/provider session is available for current runtime proof. |
| operations-integrity | **blocked** | RQ513 enrolled-family evidence | `unknown` | unknown | unknown | Inventory, Data Quality and Decision Board probes are explicitly unverified until executed. |
| prepost-runtime | **blocked** | Q83 live-schema gate | `unknown` | unknown | unknown | Q83 is PARTIAL; this readiness snapshot does not promote RQ491 or bypass the gate. |
| prepost-runtime | **failed** | .ai/runs/2026-10-01-RQ535-evidence.md (GET /api/analytics/vendor-sales-nivelacija, all/store/imported) | `3a6a6886ca2c6d0d9e5924dfbce4f7788409f427` | 2026-10-01T09:37:20.000Z | 2026-10-02T09:37:20.000Z | HTTP 200 with meta.success=false, vendor_sales_nivelacija_contract_missing and scopeApplied=false on every scope, including the store-scoped raw path. The live view still lacks the semantic revenue-change column despite the startup re-apply logic in the deployed SHA. |
| deployed-health | **stale-evidence** | .ai/runs/2026-10-01-RQ535-evidence.md (GET /ready, /health/dependencies, /api/analytics/health) | `3a6a6886ca2c6d0d9e5924dfbce4f7788409f427` | 2026-10-01T09:37:43.000Z | 2026-10-02T09:37:43.000Z | Public health only: default and analytics databases reachable, analytics facts present. Says nothing about analytics views or materialized views. |
| supplier-overview-runtime | **failed** | .ai/runs/2026-10-01-RQ535-evidence.md (GET /api/analytics/supplier-sales-stats, all/store/imported) | `3a6a6886ca2c6d0d9e5924dfbce4f7788409f427` | 2026-10-01T09:37:16.000Z | 2026-10-02T09:37:16.000Z | HTTP 503 'Problem pri povezivanju sa bazom' on every scope while database health passes. The endpoint maps every NpgsqlException to that 503 without a correlation id, so the SQL cause needs provider logs (RQ474). |
| supplier-scorecard-runtime | **failed** | .ai/runs/2026-10-01-RQ535-evidence.md (GET /api/analytics/suppliers/decision-hub/summary, all/store) | `3a6a6886ca2c6d0d9e5924dfbce4f7788409f427` | 2026-10-01T09:37:19.000Z | 2026-10-02T09:37:19.000Z | All-scope fails closed with MISSING_OBJECT (mv_supplier_decision_score_cache_90d absent; 30d request falls back to the 90d dataset). The store-filtered request returns an unhandled HTTP 500 INTERNAL_ERROR (RQ475 scope). |

## Limitations

- RQ535 (2026-10-01): deployed SHA read from public GET /api/runtime/version on https://trendplus-api.onrender.com (Render, build 2026-10-01T08:53:20Z). No connected provider-log, database or browser session was used.
- Deployed 3a6a6886 is contained in main; the only runtime delta to current main is RQ533 (vendor/totals change percent nullability).
- Pre/Post production verification still fails on the live view contract (vendor_sales_nivelacija_contract_missing); Q83 owns the remaining semantics and the live gate is not bypassed.
- RQ513 integrity families without an executed probe remain unverified rather than green.

## State semantics

- `code-ready`: current repository evidence is fresh and SHA-bound.
- `runtime-unproven`: code evidence is current, but matching deployed/runtime proof is absent.
- `verified-current`: required runtime proof is fresh and matches repository, deployment and generations.
- `stale-evidence`: evidence expired or no longer matches SHA/generation/deployment.
- `blocked`: proof was skipped, unavailable, integrity-unverified or externally gated.
- `failed`: executed evidence failed.
