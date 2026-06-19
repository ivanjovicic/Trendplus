# Analytics Queue Reconciliation

Updated: 2026-06-19

## Snapshot

- Current local HEAD: `bb5e9334cdb491ef9d707cd8bcefc5fc0b1bffe8`
- Local tracking branch `origin/main`: `afb575ac02a9e43f6ab0a3ce2520997fd0ade69f`
- Queue gap: Q49 is not present in the current queue snapshot, so it cannot be marked DONE/PARTIAL in the same way as the numbered tasks that exist.

## Q38-Q49 Status Table

| Q | Status | Evidence | Tests / Docs | Risk |
| --- | --- | --- | --- | --- |
| Q38 | DONE | `36c5e54` plus `docs/qa/ANALYTICS_REGRESSION_RISK_AUDIT.md` | `git diff --check`, `npm run check:analytics-guardrails`, `npm run build` | Broader `|| 0` / `?? 0` audit is still useful outside the reviewed surfaces. |
| Q39 | DONE | `55717df` plus `docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md` | `npm run test -- --run ExecutiveDecisionBoard` | Repeated cards still appear across sections by design, but section context is explicit. |
| Q40 | DONE | `2f694cd` plus `docs/qa/ANALYTICS_OBSERVABILITY_REVIEW.md` | `npm run test -- --run AnalyticsRefreshStatusBanner`, `npm run build` | Correlation IDs still depend on the backend emitting them, but the client preserves them. |
| Q41 | DONE | `3a0def1` plus `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md` | gap review docs and linked plan files | Ledger is still implicit in `MetadataJson` and notes; canonical structured metadata is still future work. |
| Q42 | DONE | `58165dc` plus `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md` | `npm run test -- --run ProductDecisionCenterPage.confidence` | Product Decision Center still lacks a dedicated calibration UI; learning belongs in the outcome layer. |
| Q43 | DONE | `ee23a61` plus `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md` | `npm run test -- --run src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts` | Supplier ranking still uses internal fallback ordering, but visible confidence remains backend-gated. |
| Q44 | DONE | `7b24b38` plus `docs/qa/INVENTORY_DECISION_CONTRACT_AUDIT.md` | `npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts` | Some inventory widgets still render derived `estimatedValueAmount`; the action-impact contract is fixed, but not every display value. |
| Q45 | DONE | `a8602ae` plus `docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_READINESS.md` | readiness review docs | Frontend board fan-out was still the active model at the time of the review. |
| Q46 | DONE | `c79f50b` plus `docs/analytics/DECISION_BOARD_BACKEND_AGGREGATE_CONTRACT.md` | contract design docs | The aggregate contract must preserve nullable impact and section context. |
| Q47 | DONE | `3b488f6` plus backend aggregate implementation files | `dotnet build Trendplus2.sln --no-restore --configuration Release`, `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~DecisionBoard|FullyQualifiedName~AnalyticsCriticalRouteMappings"`, `npm run check:analytics-guardrails`, `npm run build` | Backend aggregate exists, but the frontend had to be switched to it separately. |
| Q48 | DONE | Current worktree changes in `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`, `Klijent/clientapp/src/services/analyticsApi.ts`, `Klijent/clientapp/src/types/analytics.ts`, `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts` | `npm run check:analytics-guardrails`, `npm run typecheck`, `npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`, `npm run build` | Legacy multi-source helper code still sits in the file and can be cleaned up later if a dedicated cleanup task is added. |
| Q49 | BLOCKED | No Q49 entry exists in the current queue snapshot | none | The queue jumps from Q48 to Q50, so Q49 needs an explicit task definition before it can be assessed. |

## Multi-Topic Commit Risk

Commit `8fb614121d5e6ad944743b6f4e6aff809debaf72` bundles several analytics topics into one change set:

1. Endpoint-level no-fake-zero tests
2. KPI methodology rollout audit
3. Production readiness checklist
4. Retail KPI roadmap
5. Supplier Negotiation Pack MVP
6. Replenishment/OOS decision workflow
7. Markdown optimizer MVP

Risk note:

- One commit now spans multiple decision surfaces, which makes review, rollback, and deploy-status diagnosis harder.
- The commit also raised a Vercel status concern tied to GitHub commit email settings, so release hygiene needs to stay ahead of new feature work.

## Next Recommended Tasks

1. Q51 - Fix Vercel status blocker caused by GitHub commit email settings
2. Q52 - Review and harden Supplier Negotiation Pack MVP
3. Q53 - Audit Replenishment/OOS decision workflow trust states
4. Q54 - Audit Markdown Optimizer MVP safety and trust boundaries
5. Q55 - Add KPI methodology consistency review and tests
6. Q56 - Close Analytics production readiness checklist

## Notes

- Q50 has been used to reconcile the queue and document this snapshot.
- Q55 and Q56 are now marked DONE in the main queue snapshot, so the reconciliation note is historical rather than a live blocker list.
- No application logic was changed for this task.
