# Evidence — analytics re-audit 2026-10-04

- Agent: Grok Bot (executor), on behalf of Ivan Jovicic
- Base: origin/main 7de14c18 (worktree Trendplus2-grok, detached)
- Production runtime: /api/runtime/version → commit 02f9915887f99115348bd241590da581dafee45d, build 2026-10-04T16:56:14Z (18:56 CEST), process web
- Probe window: 2026-10-04 18:04–18:12Z (20:04–20:12 CEST), GET only, no auth, no writes
- Report: docs/qa/ANALYTICS_REAUDIT_2026-10-04.md

## Live probes (key values)
| Probe | Result |
|---|---|
| validation/freshness | lastImport 2026-08-12T10:30Z, freshnessHours 1279.6, critical |
| refresh-status | all 6 jobs unknown, "Worker nije registrovan u web procesu", cache in-memory |
| analytics health | salesFacts 5545, salesLineFacts 67517, productsDim 12422 |
| daily/supplier/shoe-type/color/dashboard(dated)/kpi-snapshot 2026-07-07..2026-08-06T00:00Z | 1,561,120 RSD / 307 units each |
| supplier-sales-stats toDate=2026-08-05 | 1,540,870 / 301 (exclusive end) ; margin 46.48 %, historical cost coverage 100 % |
| shoe-type 2026-09-04..10-04 | popRevenueChangePct -100 (Ž.Cipela 6980, Sandala 5490, Papuca 7780 previous) |
| reports/pilot-intake default | 2026-07-07..08-05, readiness 67, 307 lines, 1078 missing cost, 12422 missing category |
| cached/dashboard/bootstrap (no dates) | meta requested 09-05..10-04; summary 836,350/15/145 observed 04-05..04-21; payment/weekday/hour all-time (5550 tx, 240.6M Nepoznato, hour 0 = 5530 tx); 18.5 s cold |
| bootstrap dated July | executive.topSuppliers revenue 0 (olihem, Gemeli…) |
| cached/products/decision-center top=500 July | 500 FIX_DATA rows, revenue 0, 13,256,489 bytes, 9–10 s; top=2000: FIX_DATA 1078 / INSUFFICIENT 662 / WATCH 260, actionable 0 |
| color-sales-stats July | single row Nepoznato 100 % (1,540,870; 128 articles) |
| data-quality/health | score 100 excellent; windowTo 2026-10-04T23:59:59.9999999Z with half_open_utc; list: 0 issues no_open_issues |
| inventory balance | value 93,389 RSD for 3,566 units; lowStock 0 (bootstrap 213); aging 12422 in 31–60 (Ulaz robe 2026-08-12T10:31:51Z amount 0); forecast/size-curve/rebalance/alerts missing_relation |
| vendor-sales-nivelacija | contract_missing: column change_percent_revenue_semantic missing in public.vw_vendor_sales_nivelacija; integrity nivelacija unverified; admin diagnostics 401 |
| reports/supplier-decision June | MISSING_OBJECT (90d dataset), label "Poslednjih 30 dana" |
| decision-hub/summary July | supplierCount 0, insufficient_data, label "Poslednjih 90 dana", provenance mv_supplier_decision_score_cache_90d |
| pre-nivelacija-prioriteti | window 04-07..10-04 (UtcNow), 537 candidates, 371 insufficient ("Insufficient data"), #1 sku 85 STARO 2017 5559 days, highlightNow contains row with recommendationAllowed=false |
| integrity | supplier_shoe_type reconciled 0=0; daily receiptReconciliation verified matched 0 / unmatched 25; inventory degraded |
| insight studio | "NeodreÄ‘eno" in 7 endpoints; intelligence asOf 2026-03-25 / 2026-01-31; daily-analysis/weekly-changelog anchored to today |
| actions | smoke fixtures visible (id 3 "Smoke Inventory Final" 2026-05-22), counts new 3 / done 1 |
| timings | bootstrap 18.5 s, decision-board 11.4 s, decision-pulse 11.6 s, PDC 9–10 s |

## Code references (origin/main 7de14c18)
- Api/Endpoints/CachedAnalyticsEndpoints.cs:2322-2323, 2374-2492, 2536 (bootstrap periods/executive), 6623-6628 and 8446-8455 (FIX_DATA ordering)
- Api/.../ProductDecisionReasoningHelper.cs:181-185 (category blocker, MinStock)
- Api/Endpoints/InventoryEndpoints.cs:56-58, 365, 1005-1013
- Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:98, 118, 738, 763 (UtcNow anchor)
- Api/Endpoints/SupplierDecisionHubEndpoints.cs:1148-1170, 3260-3274 (period label)
- Api/Endpoints/InsightStudioEndpoints.cs:294, 398, 556, 684; InsightStudioV2Endpoints.cs (mojibake bytes C3 84 E2 80 98)
- Klijent/clientapp/src/App.tsx:101-125 (analytics routes); PilotReadinessPage.tsx:681 (undated bootstrap)

## Queue state at audit
560 unique RQ: DONE 486, WAITING 61, PARTIAL 6, READY 2 (RQ569, RQ553), OBSOLETE 5. Max before registration RQ569. Registered RQ570–RQ585. Locks: none.

## Limitations
No browser rendering, no DB/provider access, no admin key. C: had ~47 MB free during the audit (C:\tp2tmp tar files ~540 MB, not created by this agent, untouched).
