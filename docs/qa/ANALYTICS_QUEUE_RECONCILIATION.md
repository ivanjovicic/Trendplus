# Analytics Queue Reconciliation

Updated: 2026-08-05
Task: `STAB02`
Validator: `node scripts/check-prompt-queues.mjs`

## Canonical ownership matrix

| Feature family / concern | Canonical queue | Current runnable | Duplicates / replacements | Blocker |
|---|---|---|---|---|
| Deploy / CI / live-smoke truth | `STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` | none (`STAB01` DONE with WARN) | June smoke docs are historical only | Backend SHA parity + Actions NuGet restore |
| Queue governance | same STAB queue | was `STAB02` (this task) | `NEXT_PROMPT_QUEUE.md` TODO workflow | none after STAB02 |
| Auth runtime boundary | STAB queue | `STAB03` after STAB02 | older access-control notes in `NEXT_PROMPT_QUEUE` Q22 | STAB02 |
| Analytics correctness (impact, units, fake zero/green, dates, scope) | `ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` → named RQ/SQL queue | no global READY in reliability/cross-surface addenda | WAITING RQ55/56/63 and later addenda | owner unblocking / earlier DONE evidence |
| Cross-surface lineage | `ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md` | none READY; RQ55/56/63 WAITING | RQ51–RQ62 DONE | RQ34/RQ46 etc. as listed |
| SQL trust | `SQL_ANALYTICS_PROMPT_QUEUE.md` | none READY | Q69–Q82 lane in priority review | Q69 vocabulary first |
| Premium UI polish | `ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` | `P-UI-05` READY (parallel-safe) | least-improved addendum WAITING | none for P-UI-05 |
| GenAI / RAG / MCP | `GENAI_PRODUCT_PROMPT_QUEUE.md` | none READY (dormant) | must not use TODO/OPEN | unresolved STAB P0 gates |
| Legacy Codex ledger | `NEXT_PROMPT_QUEUE.md` | historical only | Q20/Q22/Q67 already DONE | do not start from TODO rules |

## Stale-entry reconciliation (evidence-based)

| Task | Previous claim | Current verdict | Evidence |
|---|---|---|---|
| Q20 Demo verification smoke | once OPEN / later PARTIAL notes | **DONE** with remaining auth-gated risk | `docs/qa/DEMO_VERIFICATION_SMOKE_RESULT.md`; live `/api/admin/demo-verification` returns `401` anonymously |
| Q22 Access-control P0 group | claimed writes still unprotected / OPEN | **DONE** for analytics action write gate | `Api/Endpoints/AnalyticsActionsEndpoints.cs` uses `AdminAccessControl.GetDecision` on create/status/outcome writes; broader refresh/export/log surfaces remain STAB/security follow-up |
| Q67 Encoding guardrail | PARTIAL (typecheck blocker) | **DONE** | `Klijent/clientapp/scripts/check-encoding.mjs` wired into `check:analytics-guardrails`; STAB01 local guardrails pass |
| GenAI `TODO` statuses | unsupported live vocabulary | converted to **WAITING** | protocol vocabulary only |
| GenAI GAI02 `IN_PROGRESS` | started under unresolved STAB P0 | reset to **WAITING** | GenAI gate rule in AGENT_START_HERE + validator |

## Current next READY prompts

- Cross-cutting / governance: `STAB03` (after STAB02 completion)
- Analytics correctness: none READY globally; use priority review for the next unblocked WAITING family when owners promote it
- Premium UI: `P-UI-05` (parallel-safe docs/tests)
- GenAI: none (dormant)

## Validator coverage

`scripts/check-prompt-queues.mjs` fails with `file:line` for:

- unsupported statuses (`OPEN`, `TODO`, …)
- duplicate task IDs in one queue file
- multiple exclusive READY tasks in one feature family
- Current READY prompt missing / not READY|IN_PROGRESS
- GenAI READY/IN_PROGRESS while unresolved STAB P0 READY/PARTIAL/BLOCKED/IN_PROGRESS remains

Self-test:

```powershell
node scripts/check-prompt-queues.mjs --self-test
```

## Historical note

Earlier 2026-06-19 Q38–Q49 snapshot below is retained as archive context and is not the live router.

---

## Archive: Q38-Q49 Status Table (2026-06-19)

| Q | Status | Evidence | Tests / Docs | Risk |
| --- | --- | --- | --- | --- |
| Q38 | DONE | `36c5e54` plus `docs/qa/ANALYTICS_REGRESSION_RISK_AUDIT.md` | `git diff --check`, `npm run check:analytics-guardrails`, `npm run build` | Broader `|| 0` / `?? 0` audit is still useful outside the reviewed surfaces. |
| Q39 | DONE | `55717df` plus `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md` | `npm run test -- --run ExecutiveDecisionBoard` | Repeated cards still appear across sections by design, but section context is explicit. |
| Q40 | DONE | `2f694cd` plus `docs/qa/ANALYTICS_OBSERVABILITY_REVIEW.md` | `npm run test -- --run AnalyticsRefreshStatusBanner`, `npm run build` | Correlation IDs still depend on the backend emitting them, but the client preserves them. |
| Q41 | DONE | `3a0def1` plus `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md` | gap review docs and linked plan files | Ledger is still implicit in `MetadataJson` and notes; canonical structured metadata is still future work. |
| Q42 | DONE | `58165dc` plus `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md` | `npm run test -- --run ProductDecisionCenterPage.confidence` | Product Decision Center still lacks a dedicated calibration UI; learning belongs in the outcome layer. |
| Q43 | DONE | `7b24b38` plus `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md` | `npm run test -- --run src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts` | Supplier ranking still uses internal fallback ordering, but visible confidence remains backend-gated. |
| Q44 | DONE | `a8602ae` plus `docs/qa/INVENTORY_DECISION_CONTRACT_AUDIT.md` | `npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts` | Some inventory widgets still render derived `estimatedValueAmount`; the action-impact contract is fixed, but not every display value. |
| Q45 | DONE | `c79f50b` plus `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_READINESS.md` | readiness review docs | Frontend board fan-out was still the active model at the time of the review. |
| Q46 | DONE | `c79f50b` plus `docs/analytics/DECISION_BOARD_BACKEND_AGGREGATE_CONTRACT.md` | contract design docs | The aggregate contract must preserve nullable impact and section context. |
| Q47 | DONE | `3b488f6` plus backend aggregate implementation files | backend/frontend checks from that period | Backend aggregate exists, but the frontend had to be switched to it separately. |
| Q48 | DONE | ExecutiveDecisionBoard aggregate switch | board specs/guardrails/build | Legacy multi-source helper code may still sit in the file. |
| Q49 | BLOCKED | No Q49 entry exists in the current queue snapshot | none | The queue jumps from Q48 to Q50. |
