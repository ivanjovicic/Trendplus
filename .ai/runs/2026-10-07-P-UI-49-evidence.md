Task ID: P-UI-49
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/p-ui-49-analytics-state-taxonomy / no PR (direct-main)
Main commit SHA: 1814be27d6d94375ce32f74f816371f3a21b590c
Main verification: passed - refreshed origin/main at 194df3081cd4013b4320e77ffe34329e01547ff6 contains the implementation and follow-up correction commits
Evidence state: synchronized

## What was done
- Added one typed shared taxonomy for analytics empty, warning, loading and error states, extended shared empty/error state components compatibly, and adopted the contract on the Supplier Footwear analytics pilot.
- Inventory includes currently reachable backend reasons such as `no_data_in_period`, `no_sales_in_period`, `no_supplier_sales`, `no_pulse_items`, `no_open_issues`, `no_top_offenders`, `no_import`, `no_intake_evidence`, `no_rows_for_period`, `filtered_out`, `insufficient_data`, `partial_payload`, `schema_fallback`, `STALE_CACHE`, `BOARD_PARTIAL`, `PULSE_PARTIAL`, `MISSING_OBJECT`, `MISSING_SCHEMA`, `contract_missing`, `vendor_sales_nivelacija_contract_missing`, `analytics_db_unavailable`, `analytics_database_unavailable`, `analytics_unavailable`, inventory errors and `supplier_decision_unavailable`.
- `beyond_source_horizon` and `source_dimension_not_populated` are reserved and render only when explicitly returned by the backend. Unknown codes use neutral copy and remain available in a details disclosure. The UI does not infer states from row counts.
- Added schema mismatch and backend-unreachable handling, retry and correlation-ID copy affordances, and an 8-second slow-loading recovery with retry/cancel; cancelling on the pilot invalidates its in-flight request.
- Follow-up correction keeps legacy prose in `emptyReason` as text instead of treating it as an unknown machine code. Updated shared-consumer assertions to verify canonical taxonomy copy and closed technical-code disclosures.
- Retargeted three existing guardrail baseline line references shifted by the page changes; no exceptions were added.

## Files changed
- `.ai/runs/2026-10-07-P-UI-49-evidence.md`
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `Klijent/clientapp/src/components/analytics/AnalyticsEmptyState.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsEmptyState.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsErrorState.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsErrorState.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsEmptyState.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsErrorState.spec.tsx`
- `Klijent/clientapp/src/utils/analyticsStateTaxonomy.ts`
- `Klijent/clientapp/src/utils/__tests__/analyticsStateTaxonomy.spec.ts`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/RecommendationMeasurementStatisticsReview.spec.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`

## Validation run
- `npm run test -- --run src/components/analytics/__tests__/AnalyticsEmptyState.spec.tsx src/components/analytics/__tests__/AnalyticsErrorState.spec.tsx src/utils/__tests__/analyticsStateTaxonomy.spec.ts src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx` -> pass, 4 files / 54 tests.
- Follow-up focused suites across 8 files -> pass, 191 tests; Data Quality empty-state consumer test -> pass, 1/1 (192 total).
- `node scripts/check-analytics-guardrails.mjs` -> pass; 39 existing baseline findings, zero new. `node scripts/check-encoding.mjs` -> pass.
- `npm run build` -> pass; Vite reported the existing Recharts chunk at 548.04 kB (>500 kB warning threshold).
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass.
- `git diff --check` and `git diff --cached --check` -> pass.
- GitHub Planning Governance run `37590771658` -> success on `da0931309dbf0d2145a8f6e42c1912ca567a5d24`.
- GitHub Planning Governance run `37593471184` -> success on final synchronized evidence SHA `51ba47f426832ee4d262b6651ade5dd6508cd121`.
- Analytics Quality Gates run `37590771745` failed on initial SHA `da0931309dbf0d2145a8f6e42c1912ca567a5d24` with 10 stale shared-state assertions; taxonomy consumer assertions were corrected and the focused cases passed locally.
- Follow-up Analytics Quality Gates run `37592911311` on correction SHA `1814be27d6d94375ce32f74f816371f3a21b590c` ran 153 specs: 150 passed and 3 failed. The remaining failures are duplicate trust-header summary/detail text queries in `AnalyticsDashboard.operationalFallback.spec.tsx`, `ColorSalesStatsPage.premium.spec.tsx`, and `SupplierConsolidatedPage.spec.tsx`; they exercise unchanged `AnalyticsTrustHeader` markup (P-UI-43 ownership), not the taxonomy. Build and dependency audit jobs passed; the workflow skipped later guardrail/build steps after the test failure.

## Validation not run
- Full frontend suite was not run locally; the remote full suite is classified above. Local live/browser API smoke was not run because the prompt's focused component/pilot contract does not require deployed evidence.

## Documentation impact
- Updated the P-UI owning queue and `MASTER_ROADMAP.md`; this run log is the durable delivery evidence.

## What was missed
- No additional page families adopted the taxonomy; those migrations stay with their page owners.

## Risks
- Remote full-suite run remains red on three existing duplicate trust-header assertions in files outside P-UI-49's ownership; `AnalyticsTrustHeader` and those page surfaces were not changed here.
- Existing Recharts bundle-size warning remains; build succeeds.

## Post-close routing recovery
- Recovery base `origin/main`: `194df3081cd4013b4320e77ffe34329e01547ff6` (post-close SHA).
- Active owner queue/addendum files scanned: `MASTER_ROADMAP.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ACTION_OUTCOME_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_SUPPLIER_AUDIT_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md`; `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`.
- Completed/changed IDs searched across the active set: P-UI-49 and P-UI-50; relevant dependency truth checked for RQ573, RQ574, RQ570, RQ319 and RQ320.
- Candidate matrix: P-UI-38 remains WAITING for P-UI-51/P-UI-52 and other migrations; P-UI-42 remains WAITING for P-UI-51; P-UI-46 remains WAITING for P-UI-42. P-UI-50's RQ573/RQ574/P-UI-49 dependencies are DONE, but it is BLOCKED by the uncommitted `ProductDecisionCenterPage.tsx` edit in the primary checkout; no matching task branch, lock or open PR exists. RQ current READY and SQL current READY are none; BCI/QDB/MT/GAI expose no higher-priority runnable candidate and STAB16 remains provider/deployment blocked.
- Successor: P-UI-51 remains READY and is the next primary, unclaimed prompt. Its RQ570/P-UI-39 prerequisites are DONE; RQ319/RQ320 are WAITING (not IN_PROGRESS). No P-UI-51 branch, lock, open PR or owned-path collision was found. P-UI-52 remains an independent READY lane.

## Next
- P-UI-51 — Decision-surface controls, READY and unclaimed; P-UI-50 is BLOCKED pending release of its owned Product Decision page path.
