# Analytics Reliability Prompt Queue — Supplier Audit Addendum

Date: 2026-09-30
Repo: ivanjovicic/Trendplus
Source audit: docs/ai/SUPPLIER_ANALYTICS_DEEP_AUDIT_PROMPTS_2026-09-30.md
Current READY prompt: none (RQ532 DONE — negative source path)
Additional READY prompts: none; RQ524, RQ527, RQ528, RQ533, RQ534, RQ535 and RQ536 are DONE
Owner promotion/claim 2026-10-02: idle recovery verified RQ520, RQ527 and RQ500 DONE on current `main`, found no active RQ532 lock/branch/PR collision, and promoted/claimed RQ532 for source discovery. Local lock: `.ai/task-locks/RQ532-codex.lock.md`; branch: `cursor/rq532-supplier-size-curve-78b0`.
Queue reconciliation 2026-10-02: RQ532 DONE on negative source path — repository-owned supplier × footwear-type sold/received/on-hand by size remains absent; explicit unavailable evidence is now pinned in backend contract/tests, Supplier Asortiman UI, and run log. Future aggregation requires owner-approved source + DiD maturity/control contract. Run log: `.ai/runs/2026-10-02-RQ532-evidence.md`.
Queue reconciliation 2026-10-02: RQ530 PARTIAL on `main` — Supplier Pregled exposes Inventory snapshot buying signals (on-hand, estimated value, aging) with explicit unavailable metrics for supplier-aggregated cover/sell-through, returns, period margin trend, PO/lead-time; backend contract/tests pin proven vs absent sources. Full acceptance remains gated on RQ487 overview query-cost baseline. Run log: `.ai/runs/2026-10-02-RQ530-evidence.md`.
Queue reconciliation 2026-10-02: RQ523 DONE on `main` — Pregled keeps backend population share when a supplier is focused; revenue rank badges only for descending sort; Asortiman control chips reflect applied filters with Serbian data-quality labels; assortment category persists in URL/canonical filters. RQ529 unblocked on RQ523. Run log: `.ai/runs/2026-10-02-RQ523-evidence.md`.
Queue reconciliation 2026-10-02: RQ500 DONE on `main` — Supplier Pregled/Skorkarta/Asortiman first-screen KPIs match each tab's question; scorecard quality/risk metrics and assortment type/coverage/elasticity are primary; generic revenue/concentration metrics moved to secondary `<details>`; consolidated shell shows cross-tab role cue. RQ523/RQ529 dependency on RQ499 unchanged; RQ530 and RQ532 unblocked on RQ500. Run log: `.ai/runs/2026-10-02-RQ500-evidence.md`.
Queue reconciliation 2026-10-02: RQ499 DONE on `main` — consolidated Supplier shell exposes critical/pending trust states, tab-specific fallback sources, scorecard signal-only mode, and no stale cross-tab trust flash. RQ523 is dependency-complete after RQ499; RQ500 waits on RQ498/RQ499 (both DONE). Run log: `.ai/runs/2026-10-02-RQ499-evidence.md`.
Queue reconciliation 2026-10-02: RQ498 DONE on `main` — Supplier Pregled/Skorkarta/Asortiman headline labels and pp units are explicit in UI and scorecard report/export; calculations unchanged. RQ523 remains WAITING on RQ499; RQ529 on RQ523/RQ499; RQ530 on RQ500/RQ524/RQ474/RQ487; RQ532 on RQ500; RQ531 owner-gated. Run log: `.ai/runs/2026-10-02-RQ498-evidence.md`.
Queue reconciliation 2026-10-01: RQ535 DONE. Exact deployed SHA `3a6a6886` confirms the Supplier gates (Overview 503, Scorecard `MISSING_OBJECT` plus a store-filtered 500, Assortment and Pre/Post contract missing) with sharper evidence. RQ474 is READY in the canonical RQ queue. RQ475 waits on RQ536 (shared Decision Hub surface). Q83 stays READY in the SQL queue. The live root causes need Render/Neon logs (STAB16).
Queue reconciliation 2026-10-01 (latest): owner reports Docker Desktop available and today's Render deploy successful. RQ534 is DONE and the Pre/Post spec CI regression is closed. Two non-duplicate lanes are now promoted: RQ535 (P0 primary) performs exact-SHA public deployed smoke/gate reclassification without DB/provider writes; RQ536 (P2 additional) versions/explains the current Supplier scorecard model without changing policy. Q83 is separately re-promoted in the SQL queue for the remaining price-direction/category fake-zero contract. RQ531 stays owner-gated for actual policy changes after RQ536.
Re-entry/completion 2026-10-01: RQ536 moved PARTIAL -> IN_PROGRESS after .NET and Docker became available, then IN_PROGRESS -> DONE after a successful Release build and focused PostgreSQL oracle proof (15/15). Implementation `7c2e6a54` and PR head/base were already ancestors of current `main`; stale PR #90 was closed after validation because it contained no unique undelivered code. Run log `.ai/runs/2026-10-01-RQ536-evidence.md`.
Queue registration 2026-10-01: RQ534 was registered from a red current-main CI classification. Analytics Quality Gates has failed since `13a01ebd` because `ProdajaPrePostNivelacijePage.spec.tsx` still uses the pre-RQ520 recommendation statuses and focus-chip labels. It was deduplicated against the existing queues, then promoted and claimed in the same idle-recovery run.
Queue reconciliation 2026-10-01: RQ533 DONE. Assortment vendor and totals change percent are null without mature comparable evidence or a revenue baseline; the error fallback reports null instead of 0; cache key v7. No Supplier-audit prompt is runnable: RQ523, RQ529, RQ530 and RQ532 wait on RQ498, RQ499 and RQ500 (RQ474/RQ475 live access), and RQ531 waits on owner approval. Follow-up for the Q83 Pre/Post owner: price-direction and category change percent `?? 0m`.
Queue registration 2026-10-01: RQ533 was registered from the RQ527 follow-up (Assortment fake 0% change percent without comparable evidence), deduplicated against the existing queues, and promoted and claimed in the same idle-recovery run.
Queue reconciliation 2026-10-01: RQ524 DONE through idle recovery. The Supplier reconciliation pack now runs on Testcontainers fixtures with all sixteen verdicts pinned, read-only proof, and owned FAIL verdicts for missing objects. It fixes the previous-only window, the `42P01` aborts and the false SUP-012 FAIL on non-comparable rows, and adds the SUP-016 maturity check. No Supplier-audit prompt is runnable: RQ523 waits on RQ498; RQ529 on RQ523/RQ499; RQ530 on RQ500/RQ474/RQ487; RQ531 on owner approval; RQ532 on RQ500. RQ498, RQ499 and RQ500 wait on RQ474/RQ475, which need read-only live/provider access.
Queue reconciliation 2026-10-01: RQ527 DONE — independent Assortment oracle and golden fixture match startup view 014 and the bounded scoped source on real PostgreSQL; the oracle exposed and the run fixed a scoped-SQL `42803` failure (store/dataScope-filtered Assortment) and the unreachable `immature` vendor state. No Supplier-audit prompt is runnable: RQ523 waits on RQ498, RQ529 on RQ523/RQ499, RQ530 on RQ500/RQ524/RQ474/RQ487, RQ531 on owner approval, RQ532 on RQ500; RQ524 is PARTIAL.
Queue reconciliation 2026-10-01: RQ522 DONE — every Supplier tab publishes backend `meta.basis` rendered as "Kako se broji"; nivelacija sales exclude DUG/KOREKCIJA; Overview/Assortment use one `Nepoznato` supplier bucket. RQ527 promoted to primary READY; RQ528 is dependency-complete and promoted to additional READY; RQ523 stays WAITING on RQ498, RQ530 on RQ500/RQ524/RQ474/RQ487.
Queue reconciliation 2026-10-01: RQ521 DONE — Supplier scorecard return rate uses gross sold units with neutral missing-return rank, sale-time supplier attribution bounded to the published window, and canonical DUG/KOREKCIJA exclusion from full-price turnover evidence; existing databases pick it up through outdated-definition readiness and one-time stale windowed cache recreation. RQ522 is dependency-complete (RQ519/RQ521 DONE) and promoted to primary READY; RQ527 stays additional READY.
Queue reconciliation 2026-10-01: RQ526 DONE — independent Supplier scorecard oracle and golden fixture match real PostgreSQL article signals and all-time/90d/180d score caches; known input defects are pinned for RQ521 to flip. RQ521 is dependency-complete and primary READY; RQ527 was dependency-complete and is promoted as additional READY.
Queue reconciliation 2026-09-30: RQ520 DONE — assortment vendor-sales-nivelacija uses dedicated price-change effect policy (non-actionable), mature-post zero semantics, aligned vendor change totals, and Supplier Footwear labels decoupled from Supplier PoP. RQ518, RQ519 and RQ525 are DONE.

This addendum registers only non-duplicate Supplier follow-ups after second-pass source verification. Existing RQ474, RQ475 and RQ487 remain authoritative for overview error/readiness/query-cost work. RQ517 is already DONE for Daily Sales and is not reused.

## Registration and de-dup map

| Audit id | Queue owner |
|---|---|
| SA-F1 | RQ518 |
| SA-F2 | RQ519 |
| SA-F3 | RQ520 |
| SA-F4 corrected | RQ521 |
| SA-F5 | existing RQ474/RQ475; RQ525 for Analytics 015; security handoff only if deploy gap proven |
| SA-F6 | RQ522 |
| SA-F7 corrected | RQ523 |
| SA-P1 | RQ524 |
| SA-P2 | RQ525 |
| SA-P3 | RQ526 |
| SA-P4 | RQ527 |
| SA-P5 | RQ528 |
| SA-I1 | existing RQ474/RQ475 |
| SA-I2 | RQ529 |
| SA-E1 | RQ530 |
| SA-E2 | RQ531 |
| SA-E3 | RQ532 |
| RQ527 follow-up (fake 0% change) | RQ533 |
| RQ520 follow-up (Pre/Post spec on stale recommendation statuses; red main CI) | RQ534 |

---

## RQ518 - Fix Supplier scorecard materialized-view capability detection

Status: DONE
Priority: P1
Type: backend/sql/tests
Feature family: supplier-scorecard-mv-capability
Parallel-safe: no
Owner: Analytics Reliability / Supplier Decision
Claimed: 2026-09-30 by ChatGPT connector workspace after exact-main refresh; no open PR or rq518 branch collision found. Local filesystem lock was unavailable in connector-only execution, so the canonical IN_PROGRESS row served as the exclusive remote claim.
Completion: delivered on main through `306076b4`; evidence `.ai/runs/2026-09-30-RQ518-evidence.md`. Endpoint/startup now share catalog truth and fail closed with explicit materialized-view readiness states. Post-push CI remains residual evidence, not a delivery gate.

### Problem

Supplier Decision Hub checks materialized-view columns through information_schema.columns, while startup detects the same objects through PostgreSQL catalogs. This can yield MISSING_SCHEMA for an existing MV.

### Evidence

Current SupplierDecisionHubEndpoints uses information_schema.columns for mv_supplier_decision_score_cache variants; DatabaseInitializer uses pg_class relkind=m. This is a deterministic code defect, independent of the inherited live observation.

### Scope

Capability detection/readiness meta/tests only; no score formula, threshold, MV-data or production-schema changes.

### Read first

AGENTS.md; RQ401/RQ404/RQ475; SupplierDecisionSchemaSqlTests; Hub contract tests.

### Do

Use one pg_class/pg_namespace/pg_attribute helper plus pg_matviews.ispopulated; distinguish MISSING_OBJECT, MISSING_COLUMNS and NOT_POPULATED; reuse the same semantics in endpoint/startup; apply it to ML-score capability.

### Tests

Static regression forbidding information_schema for MVs; real-PostgreSQL cases when harness exists: populated, missing-column and WITH NO DATA; retain fail-closed missing-schema tests.

### Acceptance

Required populated MVs are never rejected as MISSING_SCHEMA; absent/incomplete/unpopulated objects remain explicitly blocked.

### Dependencies

None; RQ475 consumes the corrected readiness states.

---

## RQ519 - Make Supplier nivelacija schema lifecycle idempotent and preserve scorecard dependencies

Status: DONE
Ready after: RQ518 DONE (satisfied 2026-09-30)
Claimed: 2026-09-30 by ChatGPT on `cursor/rq519-reentry-78b0`; closed after Testcontainers repeated-start proof plus earlier SQL changed-history fixture evidence.
Progress: startup uses data-only nivelacija normalization, keeps Analytics 014 as the canonical view owner, rechecks and repairs 016 control/DiD dependencies after destructive view scripts, and bounds scoped sales aggregation to the selected event windows. `DatabaseMigrationBootstrapLifecycleSmokeTests` passed through Docker/Testcontainers (`1/1`), and the earlier PostgreSQL fixture plus focused schema tests remain synchronized. Run log: `.ai/runs/2026-09-30-RQ519-evidence.md`; supplement: `.ai/runs/2026-09-30-RQ525-testcontainers-evidence.md`; evidence state: synchronized; main verification: `origin/main` contains implementation `3cee77a3ae9fc957c97e827e2609a35244157f6d`.
Priority: P1
Type: backend/sql/startup/tests
Feature family: supplier-nivelacija-schema-lifecycle
Parallel-safe: no
Owner: Analytics Reliability / Schema

### Problem

Startup can check readiness before scripts that replace dependent views. 014 Fix and 016 contain DROP ... CASCADE; Analytics 014 owns semantic columns; later rebuild can repair objects, so the lifecycle risk is real but live causality is unproven.

### Evidence

014 Fix replaces vw_vendor_sales_nivelacija with CASCADE; Analytics 014 defines semantic columns; 016 drops vw_nivelacija_did with CASCADE; readiness/history and later Supplier rebuild are separate phases. Scoped vendor-sales SQL also scans unbounded history.

### Scope

Startup ordering/idempotence/readiness re-check and behavior-preserving query bounding; no pre/post semantic change.

### Read first

RQ475/RQ518/RQ525; migration 014 Fix; Analytics 014; migrations 016/018/029; DatabaseInitializer.

### Do

Capture object/history state read-only; establish one canonical view owner; re-check after destructive scripts; deterministically rebuild dependencies before readiness is green; bound scoped sales_daily and avoid equivalent source work three times.

### Tests

Real PostgreSQL repeated-start and changed-history cases; semantic column/MVs survive restart; bounded query equals prior fixture output.

### Acceptance

Restart/hash/order cannot silently break assortment contract or scorecard chain; performance changes preserve values.

### Dependencies

RQ518 DONE; RQ525 is preferred proof and may develop in parallel.

---

## RQ520 - Separate Assortment price-change effect from Supplier PoP recommendation semantics

Status: DONE
Ready after: RQ519 DONE (satisfied 2026-09-30)
Delivered: 2026-09-30 — dedicated `VendorSalesNivelacijaPriceChangeEffectPolicy`; endpoint aggregates mature comparable cohort; response `recommendationAllowed=false`; Supplier Footwear effect labels.
Priority: P1
Type: backend/frontend/sql/tests
Feature family: supplier-assortment-price-change-semantics
Parallel-safe: no
Owner: Analytics Reliability / Supplier

### Problem

Assortment feeds markdown pre/post change into the shared Supplier PoP recommendation input. It also lacks maturity, has inconsistent no-post/zero-baseline semantics and can aggregate Change over a different population than Pre/Post.

### Evidence

Current endpoint maps vendor pre/post fields to PopRevenueChangePct/PreviousPeriodRevenue; Analytics 014 uses fixed 30-day windows; endpoint percentage/vendor aggregation reproduces the mismatches.

### Scope

Assortment price-change effect semantics/labels only; no invented buying-action thresholds and no Overview PoP-engine change.

### Read first

RQ519; vendor-sales-nivelacija endpoint/view/scoped SQL; recommendation engine; assortment tests.

### Do

Use dedicated descriptive effect states; keep them non-actionable until owner approval; add elapsed/mature state; mature valid-baseline no-post -> zero; zero baseline -> unavailable/no_baseline; Change uses same comparable rows; margin benchmark uses documented weighted covered population.

### Tests

Mature/immature, no-post, zero-baseline, same-population and weighted-margin fixtures; frontend proves Overview PoP labels are not reused.

### Acceptance

No markdown effect is presented as ordinary PoP; no fake +100 baseline; Change equals Post minus Pre for declared comparable rows.

### Dependencies

RQ519 DONE; owner approval only if effect becomes actionable.

---

## RQ521 - Repair confirmed Supplier scorecard input bugs without changing model policy

Status: DONE
Ready after: RQ518 DONE and RQ526 DONE (satisfied 2026-10-01)
Claimed: 2026-10-01 by Cursor workspace after exact-main refresh (`HEAD == origin/main == 0043ce55`); no open PR, `rq521` branch or task lock collision found. Lock released at close.
Priority: P2
Type: sql/backend/tests
Feature family: supplier-scorecard-input-correctness
Parallel-safe: no
Owner: Analytics Reliability / Supplier Decision

### Problem

Confirmed input defects: return rate divides by net units, sales_in_period requires current and sale-time supplier equality, and receipt population is inconsistent. Coverage gates, inventory penalty and confidence weights are policy questions, not this fix.

### Evidence

Windowed score SQL derives returns from negative units and denominator from signed/net units; it cross-checks current supplier with supplier_id_at_sale; full-price/nivelacija sources do not consistently apply canonical DUG/KOREKCIJA policy.

### Scope

Only proven input/population correctness; no thresholds, weights, cutoffs, inventory normalization or cost-fallback policy.

### Read first

RQ445; RQ473; RQ526; 018/029 Supplier SQL.

### Do

Return rate uses gross positive sold units and missing stays unavailable/neutral; use canonical sale-time attribution; bound sales evidence to documented dates; apply canonical receipt exclusions; fix suspected double-return subtraction only if oracle proves it.

### Tests

Oracle cases for returns, supplier change, receipt exclusion and period boundary; update SQL guards.

### Acceptance

Oracle/MV agree on corrected inputs without changing owner-approved model policy.

### Dependencies

RQ518 DONE and RQ526 DONE; RQ445/RQ473 remain authority.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: Supplier scorecard inputs repaired in all three caches (018 all-time, 029 90d/180d) and the live endpoint SQL. Return rate = returned / gross positive sold units (N11); missing return rate ranks neutral 0.5 and known rates rank only against each other (single-supplier cohort keeps the shared guard of 1); `sales_in_period` attributes only by `supplier_id_at_sale`, bounded to the published `period_from..period_to` (N12); DUG/KOREKCIJA are excluded from full-price turnover evidence (pre-window qty/revenue/cost, first sale, had-sales) but still count as physical movement in the stock proxy (N19). Oracle defaults now are the corrected formula and match real PostgreSQL field by field.
- Changed files: `Database/Migrations/018_AddSupplierDecisionHubViews.sql`, `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`, `Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Infrastructure/Seed/DatabaseInitializer.cs`, `Api.Tests/SupplierScorecardOracle.cs`, `Api.Tests/SupplierScorecardOracleTests.cs`, `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- Contract/runtime behavior changed: yes — return_rate, return_rate_missing_evidence_reason, full-price pre-markdown signals and dependent score/recommendation values change; response shape and view/MV column lists unchanged. Deployment: core-view readiness now also requires the RQ521 definition markers, so existing databases re-run the cheap 018 CREATE OR REPLACE VIEW batches; 029 drops a windowed score cache only when its stored definition predates RQ521 and recreates it WITH DATA; the all-time cache picks up the replaced view on its next refresh.
- Checks run: `dotnet build Api.Tests` pass; `dotnet test --filter SupplierScorecardOracleTests|SupplierDecisionSchema` pass `60/60` with Testcontainers PostgreSQL actually started (includes the existing initializer repair integration test, the readiness predicate executed against repaired views, and stale 90d cache recreation); mutation counterexample (disable the 90d stale-cache drop) fails the upgrade test, file restored; `git diff --check` clean; governance validators pass.
- Checks not run: full backend suite (change bounded to Supplier scorecard SQL/initializer readiness; focused tests cover the touched contracts); live endpoint SQL is guarded statically, not executed against PostgreSQL.
- Run log: `.ai/runs/2026-10-01-RQ521-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: fe0ee347b5233b36140147559eeb71aca149a13d
- Main verification: `origin/main` `291647a8af160e17cab6955110f24c406d0bb480` contains fe0ee347b5233b36140147559eeb71aca149a13d; post-push CI `36832537665`/`36832537756` in progress at close
- Missed: N20 (customer return possibly subtracted twice from the stock proxy) left unchanged — the oracle proves only the conditional arithmetic, not that real data records a return in both places; the nivelacija `sales_daily` DUG/KOREKCIJA alignment is RQ522 (SA-F6) scope.
- Follow-up: RQ531 owns coverage gates (N13), confidence weights (N16), inventory penalty (N15) and first-markdown cohort (N17) policy; N20 needs a read-only real-data check before any fix.
- Residual risk: windowed cache recreation runs synchronously in the 029 startup transaction (same cost class as the existing missing-cache path) and the all-time cache shows pre-RQ521 values until its next refresh.
- Next: RQ522 (primary READY), RQ527 (additional READY).
- Prompt defect / scope repair: none; "bound sales evidence to documented dates" interpreted as the already-published `period_from..period_to` window, now applied in the join instead of per-CASE.

---

## RQ522 - Declare Supplier cross-tab basis and align cheap canonical mismatches

Status: DONE
Ready after: RQ519 and RQ521 DONE (satisfied 2026-10-01)
Claimed: 2026-10-01 by Cursor workspace after exact-main refresh (`HEAD == origin/main == 04bd151e`); no open PR, `rq522` branch or task lock collision found; BCI/STAB routers report no READY.
Priority: P2
Type: backend/frontend/contract/tests
Feature family: supplier-cross-tab-basis
Parallel-safe: no
Owner: Analytics Reliability / Supplier

### Problem

Overview, Scorecard and Assortment differ in attribution, cost, receipt population, cohort and period semantics. Some differences can be valid, but they are not consistently disclosed.

### Evidence

Overview uses sale-time/snapshot-aware basis; scorecard/assortment use markdown-event cohorts with different attribution/cost; assortment filters event date while sales windows extend around it.

### Scope

Machine-readable/backend basis/provenance metadata plus low-risk canonical alignment; no silent cohort or attribution rewrite. Visible cross-tab metric naming, percentage-point units and report/export labels remain owned by RQ498 and must consume this basis rather than being duplicated here.

### Read first

RQ445/RQ473/RQ498/RQ519/RQ521; shared context/provenance contracts; Supplier shell/endpoints.

### Do

Add basis metadata and 'Kako se broji'; collapse unresolved supplier ids into one unknown bucket with diagnostic count; reuse existing business timezone contract or document UTC; check nivelacija store grain read-only; record rather than auto-apply broad attribution changes.

### Tests

Basis contract, unknown-bucket, timezone and store-grain fixtures.

### Acceptance

Every tab states its basis; remaining differences are explicit and explainable.

### Dependencies

RQ519 and RQ521 DONE; RQ445/RQ473 prior authority.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: every Supplier tab publishes backend-owned `meta.basis` (`SupplierTabBasisPolicy`, `supplier_tab_basis_v1`: supplier attribution, cost basis, receipt population, cohort, period semantics, store scope, unknown-supplier policy, as-of date, timezone) and the shell renders it as "Kako se broji" with Serbian labels and a safe fallback (no raw codes). Cheap alignment applied: nivelacija `sales_daily` (startup 014 views and store-scoped SQL) excludes DUG/KOREKCIJA like `SalesReceiptPopulationPolicy`; Overview and Assortment collapse missing/unmapped/"Nepoznato" suppliers into one `Nepoznato` bucket with a diagnostic source-id count. Timezone documented as UTC (no business-timezone owner exists). Store grain checked read-only: local data inconclusive, `IDObjekat` can be NULL by import code, so the Assortment store filter is unchanged and declared as `store_filter_applies_to_events_and_sales`.
- Changed files: `Api/Dtos/AnalyticsResponseMetaDto.cs`, `Api/Dtos/SupplierTabBasisDto.cs`, `Api/Services/SupplierTabBasisPolicy.cs`, `Api/Services/SupplierUnknownBucketPolicy.cs`, `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Models/VendorSalesNivelacijaModels.cs`, `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`, `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`, `Api.Tests/SupplierTabBasisPolicyTests.cs`, `Klijent/clientapp/src/{types/analytics.ts,pages/supplierSharedState.ts,pages/SupplierConsolidatedPage.tsx,pages/SupplierConsolidatedPage.css,pages/SupplierSalesStatsPage.tsx,pages/SupplierDecisionHubPage.tsx,pages/SupplierFootwearAnalyticsPage.tsx,pages/__tests__/SupplierConsolidatedPage.spec.tsx,utils/supplierTabBasisLabels.ts}`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- Contract/runtime behavior changed: yes — optional `meta.basis` added (backward compatible); Overview/Assortment unknown suppliers merge into one row with `unknownSupplierSourceIdCount` / `unknownVendorSourceIdCount`; Assortment pre/post sales exclude DUG/KOREKCIJA; cache keys `supplier-sales-stats:v6`, `vendor-sales-nivelacija:v6:vendor`. Scorecard values unchanged.
- Checks run: `dotnet build Api.Tests` pass; `dotnet test --filter SupplierTabBasisPolicyTests` 13/13; `dotnet test --filter Supplier|VendorSales|CacheKey|SalesReceiptPopulation` 275 pass / 27 pre-existing skips / 0 fail; vitest Supplier consolidated/overview/scorecard/assortment specs 109/109; `npm run check:analytics-guardrails` pass (three baseline line numbers shifted, no new debt); `npm run build` pass; `git diff --check` clean; governance validators pass.
- Checks not run: full backend suite (bounded change); live PostgreSQL execution of the changed nivelacija SQL with DUG/KOREKCIJA fixtures (static guards; RQ527 oracle is the executable owner); real-data store-grain fixture (production not queried).
- Run log: `.ai/runs/2026-10-01-RQ522-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: c338e46fd847944bb81f0fbd48cac3b6fa981626
- Main verification: `origin/main` `87809b56d38f8b4a224a614531f20b790ee15ad9` contains c338e46fd847944bb81f0fbd48cac3b6fa981626; post-push CI `36833795890`/`36833795889`/`36833795939` queued at close
- Missed: broad attribution change N22 (Scorecard/Assortment to sale-time supplier), N10 Assortment current cost, N17 cohort and N23 period semantics are declared, not changed (owner decision); visible cross-tab naming/units remain RQ498.
- Follow-up: product owner decides N22/N10 attribution/cost alignment; RQ527 can pin the DUG/KOREKCIJA exclusion executably; RQ528 consumes `meta.basis`.
- Residual risk: v6 cache cold start; Assortment vendor count drops where unresolved vendors merge; Scorecard unknown suppliers intentionally not collapsed, so cross-tab supplier counts may differ (declared).
- Next: RQ527 (primary READY), RQ528 (additional READY).
- Prompt defect / scope repair: three existing analytics guardrail baseline entries shifted by +2 lines because of the added `basis` lines (same entries, no new debt).

---

## RQ523 - Fix Supplier frontend residuals without redefining metric policy

Status: DONE  
Delivered: 2026-10-02 on `main` (see `.ai/runs/2026-10-02-RQ523-evidence.md`)  
Ready after: RQ520 and RQ522 DONE  
Priority: P3
Type: frontend/copy/tests
Feature family: supplier-frontend-residuals
Parallel-safe: no
Owner: Analytics UI

### Problem

Focused Supplier share is recomputed over one visible row, revenue rank badges also appear for ascending sort, assortment chips can show draft/raw state, and category is not URL-persistent. C22 engine-English claim is stale; RQ488 fixed it. Shared pending/critical/gated trust semantics are owned by RQ499; visible cross-tab basis/unit wording is owned by RQ498.

### Evidence

Current display projection runs after focused filtering; rank is index+1 whenever sort field is revenue regardless of direction; assortment applied-state handling is inconsistent. AnalyticsTrustHeader trust-state wording remains evidence for RQ499, not this prompt.

### Scope

Presentation/state residuals only. No recommendation-code, shared trust-state semantics, cross-tab basis/unit naming or signed margin-share denominator change; those remain RQ499, RQ498 and RQ531 as applicable.

### Read first

RQ476/RQ488/RQ498/RQ499; Supplier overview/assortment specs.

### Do

Preserve backend whole-population share when focused; badges only for descending revenue; chips use activeFilters and Serbian enum labels; persist category. Do not redefine shared trust-state copy or the signed margin-contribution denominator here; those remain RQ499 and RQ498/RQ531 respectively.

### Tests

Focused share, asc/desc rank, applied-filter chip and category deep-link tests.

### Acceptance

No focused 100% artifact, inverse badge, raw enum/jargon or silent denominator change.

### Dependencies

RQ520, RQ522 and RQ498 DONE.

---

## RQ524 - Add read-only Supplier analytics reconciliation pack

Status: DONE
Claimed: 2026-10-01 by Cursor agent on `main` (direct) through idle recovery. The PARTIAL blocker was repository-local: Docker was missing in the earlier VM, and the run log's explicit Next asked for immature/no-post and startup-history fixture rows. Testcontainers is available here. Previous claim: 2026-09-30 by ChatGPT on `cursor/rq524-pack-extend-78b0`; that branch is fully contained in `main`, and no open PR or lock exists.
Progress: the read-only pack now exposes fifteen checks (`SUP-001`..`SUP-015`) for previous-only suppliers, attribution drift, startup-history presence, nivelacija store grain, assortment comparable totals, overview-vs-scorecard explained delta, assortment baseline flags and scorecard refresh history. Local execution against the shared operations seed returned no FAIL (`6` PASS, `9` EXPLAINED). Production/replica execution was not attempted and remains exclusively RQ454/STAB16. Run log: `.ai/runs/2026-09-30-RQ524-evidence.md`; evidence state: synchronized; main verification: `origin/main` contains `a93ae60ae3eb011f1995b40efd4fa94246faf827`.
Priority: P1
Type: sql/qa/evidence
Feature family: supplier-reconciliation-evidence
Parallel-safe: yes
Owner: Analytics Reliability / QA

### Problem

The live audit could not validate Supplier numbers, so source review cannot prove remaining data/deploy hypotheses.

### Evidence

Overview has strong fixtures, but scorecard/assortment lack equivalent live/read-only reconciliation and cross-tab basis proof.

### Scope

Read-only scripts/evidence only. Repository-local fixture execution is in scope; production/replica execution is explicitly out of scope and remains RQ454/STAB16. No writes.

### Read first

Existing check_supplier_sales_stats.sql; RQ407/RQ447/RQ454; RQ518-RQ523; context/receipt/cost policies.

### Do

Add parameterized checks for Supplier sum, unknown split, previous-only suppliers, cost coverage, attribution drift, DUG/KOREKCIJA share, MV health/refresh/capability, vendor-sales columns/startup history, assortment comparable totals, scorecard-vs-overview explained delta, nivelacija store grain, immature/no-post events, and NabavnaCenaDin vs NabavnaCena unit sanity.

### Tests

Run against deterministic fixture and prove the pack is read-only. Do not run it against production here; RQ454 consumes the reusable pack when STAB16 grants the approved production gate.

### Acceptance

Every check has pass/fail/explained result and failures map to an owner; hypotheses stay hypotheses without evidence.

### Dependencies

None for repository-local fixture work. RQ454/STAB16 exclusively own production/replica reconciliation execution.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: `Api.Tests/SupplierReconciliationPackTests.cs` executes the real pack on Testcontainers PostgreSQL across three fixtures:
  - an adversarial fixture with real view 014, startup-history rows and seeded immature, no-post, previous-only, drift, DUG, missing-cost and unknown-supplier cases, where all sixteen verdicts are pinned;
  - a fixture with missing schema objects, which returns owned FAIL/EXPLAINED verdicts instead of aborting;
  - a tampered assortment view, where SUP-012 and SUP-016 FAIL.
  A content snapshot proves the pack is read-only, and a static guard pins the read-only transaction and the exact startup-history ids.
- Changed files: `scripts/check_supplier_reconciliation_pack.sql`, `Api.Tests/SupplierReconciliationPackTests.cs`
- Contract/runtime behavior changed: pack only; no product runtime change. Six fixes:
  - SUP-008 now compares against the equally long previous window; before, it could never fire.
  - A missing score-cache MV, vendor view or startup-history table now yields its owned verdict instead of `42P01`.
  - SUP-012 ignores rows with unknown pre or post revenue instead of failing them.
  - SUP-003 is EXPLAINED on an empty population.
  - New SUP-016 (RQ520) checks that an immature row without post sales keeps post revenue NULL and a mature one shows an explicit 0.
  - History ids match DatabaseInitializer exactly.
- Checks run: `SupplierReconciliationPackTests` 4/4 with Testcontainers executed. Counterexample: the same tests fail 4/4 on the previous pack, including `42P01` on missing objects. `git diff --check` clean; governance validators pass.
- Checks not run: psql CLI run (Npgsql executes the identical SQL with variables substituted); production/replica (RQ454/STAB16); full DatabaseInitializer sequence (RQ525 owns it).
- Run log: `.ai/runs/2026-10-01-RQ524-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: ebc5925caeb9d80690433545c9614eb05e15f4c0
- Main verification: `origin/main` contains ebc5925c (ancestor check after push)
- Missed: old SUP-008/SUP-012 adversarial outcomes are inferred from the SQL, because the counterexample failed earlier on the check set. Supplier-sum parity stays with RQ528, and vendor-view column readiness stays with RQ519/RQ525.
- Follow-up: none for RQ524. Production read-only execution belongs to RQ454/STAB16.
- Residual risk: XML-based optional-object reads depend on PostgreSQL XML support; fixtures anchor on session `CURRENT_DATE`.
- Next: RQ454/STAB16 may consume the pack for approved read-only production reconciliation. The RQ524 dependency of RQ530 is now satisfied; RQ530 still waits on RQ500/RQ474/RQ487.
- Prompt defect / scope repair: the prompt's immature/no-post check was missing and is added as SUP-016 (same owner script). The other five pack fixes were required for "every check has pass/fail/explained result".

---

## RQ525 - Add real-PostgreSQL Supplier schema readiness harness

Status: DONE
Priority: P1
Type: tests/infrastructure
Feature family: supplier-schema-readiness-tests
Parallel-safe: yes
Owner: Analytics Reliability / Schema

### Problem

Static SQL tests did not catch MV capability or startup-order/CASCADE risks.

### Evidence

N01 is catalog-level; 014/016/018/029 require real PostgreSQL. Analytics 015 contains invalid/transaction-sensitive SQL but current startup does not execute that optional overlay.

### Scope

Test infrastructure/schema proof only; do not wire Analytics 015 into startup to make tests pass.

### Read first

RQ518/RQ519; current Testcontainers/integration setup; 013/014/016/018/029 and Analytics 014/015.

### Do

Apply Supplier startup sequence to seeded PostgreSQL; prove MV capability, repeated-start idempotence, semantic-column survival and refresh prerequisites; validate Analytics 015 in an isolated explicit test and preserve its current non-startup status.

### Tests

Harness plus focused schema/queue tests.

### Acceptance

Reproduces pre-fix MV issue, protects lifecycle after fixes and makes optional-015 validity explicit.

### Dependencies

None; coordinate with RQ518/RQ519 paths.

### Completion note

- Date: 2026-09-30
- Status: DONE
- Completion: Supplier schema readiness harness, isolated Analytics 015 transaction-safety proof and Testcontainers execution evidence are synchronized.
- Changed files: `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`, `Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql`
- Checks run: governance validators passed; focused Supplier SQL contract tests passed (43); local PostgreSQL sequence passed; Testcontainers filter `SupplierDecisionSchemaReadinessIntegrationTests|DatabaseMigrationBootstrapLifecycleSmokeTests` passed (`3/3`).
- Checks not run: full backend suite skipped as wider risk not in scope.
- Run log: `.ai/runs/2026-09-30-RQ525-evidence.md`, `.ai/runs/2026-09-30-RQ525-testcontainers-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main (implementation previously delivered)
- Main commit SHA: 749ede7a2d196817a078347a6cdd0ee525c577f7
- Main verification: `origin/main` contains 749ede7a2d196817a078347a6cdd0ee525c577f7
- Missed: none known
- Follow-up: claim `RQ526` or primary `RQ520` after collision checks
- Residual risk: Docker is not yet baked into the default cloud snapshot; future agents may need ad-hoc setup to repeat Testcontainers proof.
- Prompt defect / scope repair: added nullable `supplier_id_at_sale` to the analytics compatibility view so missing sale-time attribution stays explicit NULL and 018 can be validated without inventing supplier ownership.

---

## RQ526 - Add independent Supplier scorecard oracle and golden fixture

Status: DONE
Claimed: 2026-10-01 by Cursor workspace after exact-main refresh (`HEAD == origin/main == 1cb72699`); no open PR, `rq526` branch or task lock collision found. Local lock `.ai/task-locks/RQ526-cursor.lock.md` (removed at close).
Completion: delivered on main through `6fd58eb287cd0c1d83051913e8b5e9e23fb657f0`; evidence `.ai/runs/2026-10-01-RQ526-evidence.md`.
Ready after: RQ525 DONE (satisfied 2026-09-30)
Priority: P2
Type: tests/sql
Feature family: supplier-scorecard-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier Decision

### Problem

Scorecard contract/static tests do not independently prove formula inputs, rankings or recommendation output.

### Evidence

Audit found confirmed input bugs plus model-policy choices whose effects cannot be safely judged from source alone.

### Scope

Independent oracle/golden evidence; matching SQL does not approve disputed policy.

### Read first

RQ521/RQ531; 018/029 SQL; current scorecard tests.

### Do

Seed hand-computable suppliers for sell-through, margin, markdown, stock, returns, supplier change, DUG/KOREKCIJA, missing cost and first-markdown edges; implement independent C# oracle; test monotonicity/clamping and unrelated-supplier percentile effects.

### Tests

Real-PostgreSQL oracle comparison plus property/counterexample tests.

### Acceptance

Every component is independently reproducible; input bugs fail before/pass after; policy choices remain explicit.

### Dependencies

RQ525 DONE.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: independent C# oracle recomputes the deployed 018/029 Supplier scorecard from raw fixture rows; a six-supplier golden fixture (sell-through, margin, markdown share, stock, returns, supplier change, DUG, missing cost, foreign-cost fallback, price increase, first markdown before the window, missing DiD, unknown sale-time supplier) matches real PostgreSQL article signals and all three score caches (all-time, 90d, 180d) field by field.
- Changed files: `Api.Tests/SupplierScorecardOracle.cs`, `Api.Tests/SupplierScorecardOracleTests.cs`
- Contract/runtime behavior changed: none; tests only. Known input defects N11/N12/N13/N17/N19/N20 are pinned as current SQL behaviour with opt-in RQ521 corrections that produce different values; policy (weights, thresholds, coverage gates, inventory penalty, confidence weights, cost fallback) stays explicit and unswitchable in the oracle.
- Checks run: `dotnet build Api.Tests` pass; `dotnet test --filter SupplierScorecardOracleTests` pass `12/12` with Testcontainers PostgreSQL (pgvector/pg16) actually started; mutation counterexample (demand weight 0.60 -> 0.50 in 029) fails the oracle comparison on 90d/180d, file reverted; governance validators pass.
- Checks not run: full backend suite (no runtime/SQL change; wider risk not in scope).
- Run log: `.ai/runs/2026-10-01-RQ526-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 6fd58eb287cd0c1d83051913e8b5e9e23fb657f0
- Main verification: `origin/main` `050ac70dda4c4bb124be60c09d5481a36b1d2fdb` contains 6fd58eb287cd0c1d83051913e8b5e9e23fb657f0; post-push CI `36831681616`/`36831681812` queued at close
- Missed: the nivelacija event layer (`vw_vendor_sales_nivelacija`, `vw_nivelacija_did`) is a fixture-controlled seam; its own window/aggregate math is RQ527 scope.
- Follow-up: RQ521 flips the known-defect assertions together with the SQL fix; RQ531 owns policy changes.
- Residual risk: MV and anchor use the session `CURRENT_DATE`; a run crossing midnight between seeding and assertion could shift windows.
- Next: RQ521 (primary READY), RQ527 (additional READY).
- Prompt defect / scope repair: none; RQ527 was dependency-complete (RQ519/RQ525 DONE) but still WAITING, so it was promoted as an additional parallel-safe READY.

---

## RQ527 - Add Assortment pre/post oracle and golden fixture

Status: DONE
Ready after: RQ519 DONE and RQ525 DONE (satisfied 2026-09-30; promoted 2026-10-01)
Claimed: 2026-10-01 by Cursor workspace after exact-main refresh (`HEAD == origin/main == ba450485`); no open PR, `rq527` branch or lock collision.
Priority: P2
Type: tests/sql
Feature family: supplier-assortment-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier

### Problem

Assortment lacks an independent oracle for event windows, maturity, no-post/zero-baseline and vendor aggregation.

### Evidence

Current tests do not prove N04-N09/N23/N24 aggregate semantics.

### Scope

Fixture/oracle evidence only.

### Read first

RQ519/RQ520; vendor-sales-nivelacija SQL/policy tests.

### Do

Seed mature/immature, no-post, zero-baseline, repeated-event, price-increase, store-vs-chain and partially comparable cases; independently compute windows/aggregates; compare bounded/unbounded source equivalence.

### Tests

Golden JSON plus real-PostgreSQL comparison.

### Acceptance

Totals match declared population; Change equals Post-Pre for comparable rows; maturity/baseline states explicit; bounded SQL value-equivalent.

### Dependencies

RQ519 DONE and RQ525 DONE.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: an independent Assortment oracle recomputes nivelacija windows, maturity, baselines and the declared endpoint population (latest event per article, changed price, mature comparable totals/vendor sums, production effect policy). A golden snapshot and a real-PostgreSQL row-for-row comparison cover startup view 014 and the bounded scoped source (chain-wide and store 1). Fixture cases: mature, immature, no-post, missing and netted-zero baseline, repeated event, price increase, unchanged price, duplicate rows, store-vs-chain, partially comparable vendor, unknown bucket, DUG/KOREKCIJA. The bounded source is value-equivalent to the unbounded view after endpoint dedup; Change = Post − Pre and totals = vendor sums.
- Changed files: `Api/Endpoints/AllEndpoints.cs`, `Api.Tests/AssortmentNivelacijaOracle.cs`, `Api.Tests/AssortmentNivelacijaOracleTests.cs`, `Api.Tests/Golden/assortment-nivelacija-oracle.json`, `Api.Tests/SupplierCrossTabParityContractTests.cs` (compile fix)
- Contract/runtime behavior changed: yes — the scoped source SQL no longer fails with PostgreSQL `42803`, so store-filtered and imported/existing-scope Assortment requests return data instead of the error fallback. Vendors with only immature comparable evidence now get effect status `immature` instead of `insufficient_data`. Response shape is unchanged.
- Checks run: `dotnet build Api.Tests` 0 errors; `AssortmentNivelacijaOracleTests` 11/11 with Testcontainers PostgreSQL executed; mutation check (014 without the DUG/KOREKCIJA predicate fails on event 1, restored); counterexample (scoped tests failed with `42803` before the fix); `Supplier|VendorSales|Nivelacija|Assortment|CacheKey` 332 pass / 28 pre-existing skips / 0 fail; `git diff --check` clean; governance validators pass.
- Checks not run: full HTTP execution of the endpoint over the fixture (aggregation lives in the endpoint lambda and needs the full app schema; static guard pins the Change formulas and effect wiring); frontend (no change).
- Run log: `.ai/runs/2026-10-01-RQ527-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: a4c8d5144376231c57587ce92065bacc8c5476bb
- Main verification: `origin/main` contains a4c8d514 and the CI repair commit 57ee54c6 (`git merge-base --is-ancestor`). The red "Analytics Tests & Data Integrity" bootstrap had failed since RQ520 run 36754474929 with `42P16 post_revenue numeric -> numeric(18,2)` in 014. It was classified as a pre-existing RQ520 view-lifecycle regression and fixed in 57ee54c6 with a regression test; details are in the run log.
- Missed: a vendor without mature comparable rows reports semantic change 0% (`ComputeSemanticChangePercent(0,0)`), a fake-zero risk pinned in the golden; the legacy `change_percent_*` view/scoped divergence (immature no-post) is pinned, not changed; N09 stays with the RQ520 unit tests.
- Follow-up: the Assortment semantics owner should return null instead of 0 for a vendor semantic % without mature comparable evidence.
- Residual risk: store-filtered Assortment now shows numbers where users saw an error; PostgreSQL tests anchor on session `CURRENT_DATE` (midnight crossing).
- Next: no Supplier-audit READY; RQ523/RQ529/RQ530/RQ531/RQ532 WAITING, RQ524 PARTIAL.
- Prompt defect / scope repair: two same-owner repairs proven by the oracle and required by acceptance (scoped `post_window` GROUP BY; vendor-effect comparable count including immature). Unblocking repair `6eaf09d2`: the RQ528 parity test did not compile on main `1120a063` (explicit nullable tuple array, assertions unchanged).

---

## RQ528 - Add Supplier cross-tab parity contract

Status: DONE
Ready after: RQ522 DONE (satisfied 2026-10-01)
Priority: P2
Type: tests/contract
Feature family: supplier-cross-tab-parity
Parallel-safe: yes
Owner: Analytics Reliability / Supplier

### Problem

No automated contract prevents silent basis drift among Overview, Scorecard and Assortment.

### Evidence

Audit confirmed attribution, cost, receipt, cohort and period differences.

### Scope

Cross-tab reconciliation only; intentional differences allowed when metadata fully explains them.

### Read first

RQ522; shared context/evidence contracts.

### Do

Call all three surfaces on one fixture; assert equality where basis is equal and exact explained deltas where it differs; assert consistent supplier identity.

### Tests

Focused cross-tab fixture suite.

### Acceptance

Silent basis changes fail; intentional differences remain machine-readable/explainable.

### Dependencies

RQ522 DONE.

Owner promotion/claim 2026-10-01: fresh recovery verified `RQ522` DONE on exact `origin/main` `ba4504857634a27d4fcb4dc3744a408d8a9e4ed3`, confirmed no active RQ528 lock/branch/open-PR owner or feature-family collision, and found RQ527 separately claimed by Cursor with its own `supplier-assortment-oracle` scope. RQ528 moved `READY -> IN_PROGRESS` as the additional parallel-safe Supplier cross-tab parity prompt. Local lock: `.ai/task-locks/RQ528-codex.lock.md`.

Owner completion 2026-10-01: RQ528 moved `IN_PROGRESS -> DONE` and was delivered directly to `main` in implementation/evidence commit `b45085668683c91bc84f23d594494692a0e042e2`. The focused cross-tab contract uses one 90-day supplier fixture, asserts shared version/receipt/timezone/as-of semantics, pins exact attribution/cost/cohort/period/store differences, and verifies consistent unknown-supplier identity normalization. Focused API proof is 13/13; Release API build, prompt governance and `git diff --check` pass. RQ527 remains the primary READY prompt and is separately claimed by Cursor; Q83 remains PARTIAL and RQ491 remains WAITING. Run log: `.ai/runs/2026-10-01-RQ528-evidence.md`. Evidence state: synchronized.

---

## RQ529 - Fix Supplier accessibility, date formatting and terminology help

Status: DONE  
Delivered: 2026-10-02 on `main` (see `.ai/runs/2026-10-02-RQ529-evidence.md`)  
Ready after: RQ523 DONE and RQ499 DONE  
Priority: P3
Type: frontend/a11y/copy/tests
Feature family: supplier-a11y-formatting
Parallel-safe: no
Owner: Analytics UI

### Problem

Supplier shell lacks one H1, tab semantics are inconsistent, sortable headers need aria-sort, native date display is locale-dependent, duplicate store labels can remain ambiguous and metric terminology needs accessible help. Shared trust-state severity/pending/gated wording is explicitly outside this prompt and remains RQ499.

### Evidence

Current shell uses H2; tab buttons combine aria-selected/aria-current without tab roles; audit observed locale/label ambiguity. Shared trust-header state wording is handled by RQ499.

### Scope

Accessibility/date/terminology-help formatting only; preset alignment is owner decision if it changes filters. Do not change trust-state meaning or severity copy.

### Read first

Supplier shell/page specs; RQ499/RQ523; shared accessibility patterns.

### Do

Add coherent heading/tab or link semantics, aria-sort/direction, Serbian date display, stable duplicate-store disambiguation and glossary InfoTips; get owner decision before changing canonical presets. Do not alter pending/critical/recommendation trust semantics here.

### Tests

RTL/axe-style tab/sort, date-format and duplicate-label tests.

### Acceptance

Keyboard/screen-reader semantics coherent, dates stable Serbian, jargon explained.

### Dependencies

RQ523 DONE and RQ499 DONE.

---

## RQ530 - Add Supplier buying-value panel with independently proven metrics

Status: PARTIAL
Ready after: RQ522 DONE, RQ500 DONE, RQ524 evidence available, and RQ474/RQ487 make Overview reliably usable
Priority: P2
Type: backend/frontend/product/tests
Feature family: supplier-buying-panel
Parallel-safe: no
Owner: Analytics Product / Supplier

### Problem

Current surfaces describe sales/markdown behavior but omit several buyer decisions such as stock cover, return burden, margin trend and article winners/losers.

### Evidence

This is an enhancement gap, not a current-number bug; PO/lead-time sources are not yet proven.

### Scope

Read-only source discovery first; only metrics with authoritative source/formula.

### Read first

RQ522/RQ524; Inventory/Supplier contracts; purchase/receipt entities.

### Do

Discover PO/receipt/lead-time sources; add proven stock units/value, days cover, sell-through, gross return rate, equal-period margin trend, top/bottom articles and aged stock with basis/coverage; PO/lead time only if authoritative, otherwise unavailable reason.

### Tests

Fixture endpoint tests per metric, reconciliation and detail-drawer UI tests.

### Acceptance

Every metric has source/formula/unit/coverage/missing behavior and independent proof.

### Dependencies

RQ522 DONE, RQ500 DONE and RQ524 evidence; RQ474/RQ487 make Overview usable.

### Completion note

- Date: 2026-10-02
- Status: PARTIAL
- Completion: Inventory snapshot buying signals (on-hand units, estimated inventory value, SKU/aging buckets, top aged items) ship on Supplier Pregled using cached Inventory balance/insights with store/supplier/dataScope filters. Extended buyer metrics (supplier-aggregated cover/sell-through, gross return rate, equal-period margin trend, sales top/bottom, PO/lead-time) remain explicitly unavailable with pinned reason codes in `SupplierBuyingValueEvidenceContract`, contract tests, shared frontend copy, and the limitations panel. RQ487 (overview query-cost baseline) is still WAITING in the canonical queue.
- Changed files: `Application/Analytics/SupplierBuyingValueEvidenceContract.cs`, `Api.Tests/SupplierBuyingValueEvidenceContractTests.cs`, `Klijent/clientapp/src/utils/supplierBuyingValueEvidence.ts`, `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/SupplierSalesStatsPage.css`, `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_SUPPLIER_AUDIT_ADDENDUM.md`, `.ai/runs/2026-10-02-RQ530-evidence.md`
- Contract/runtime behavior changed: additive Inventory snapshot panel; no fake zero for missing buying metrics; no PO/lead-time or unproven aggregates.
- Checks run: `SupplierBuyingValueEvidenceContractTests`, focused `SupplierSalesStatsPage.premium.spec.tsx`, `npm run check:analytics-guardrails`, `node scripts/check-prompt-queues.mjs` (after queue edit)
- Checks not run: full backend/frontend suites; live RQ487 EXPLAIN/timing baseline.
- Run log: `.ai/runs/2026-10-02-RQ530-evidence.md`
- Evidence state: pending main SHA sync after delivery
- Delivery mode: direct-main
- Missed: supplier-aggregated cover/sell-through/returns/margin trend and PO/lead-time await authoritative endpoints and RQ487 performance acceptance.
- Follow-up: promote RQ487 or a successor prompt for overview query bounds; extend buying panel when periodized supplier metrics are proven.
- Residual risk: Inventory snapshot is current-state only and must not be read as period sales or as overriding Supplier recommendation semantics.

---

## RQ531 - Decide Supplier scorecard model weights and thresholds

Status: WAITING
Ready after: RQ536 DONE and product owner approves model-policy decisions
Priority: P2
Type: product/sql/frontend/docs/tests
Feature family: supplier-scorecard-model-governance
Parallel-safe: no
Owner: Analytics Product / Supplier Decision

### Problem

Scorecard uses relative percentile ranks, absolute-stock exposure, strict coverage gates, clamping and hard-coded confidence/recommendation thresholds. These are implementation facts, not automatically bugs.

### Evidence

029 contains percentile ranks, gates, inventory-value rank, confidence weights and thresholds; endpoint has page-level coverage gate. First audit mixed some policy choices into bug-fix scope.

### Scope

Policy decisions and any approved weight/threshold/gate changes only. Current-model documentation, formula versioning and explainability are owned by RQ536.

### Read first

RQ521/RQ526; current scorecard formula SQL/UI.

### Do

Using the RQ536 baseline, obtain explicit owner decisions for coverage gate, inventory normalization, confidence weights, cutoffs and period-scaled samples. Apply only approved policy changes, bump the formula version, and update oracle/explainability evidence. Do not change a threshold merely because it is hard-coded.

### Tests

Update the RQ526 oracle to the approved version and prove policy-version parity; RQ536 explainability tests must remain green.

### Acceptance

Every changed model policy is explicitly owner-approved, versioned and oracle-matched; unchanged policy remains exactly as documented by RQ536.

### Dependencies

RQ536 DONE and explicit product-owner decision.

---

## RQ532 - Add Supplier size-curve and controlled markdown effectiveness evidence

Status: DONE
Claimed: 2026-10-02 by Codex after dependency recovery; negative-path acceptance delivered when authoritative size source is absent.
Ready after: RQ520, RQ527 and RQ500 DONE
Priority: P3
Type: backend/frontend/product/tests
Feature family: supplier-assortment-size-curve
Parallel-safe: no
Owner: Analytics Product / Supplier

### Problem

Assortment lacks size-curve evidence and controlled markdown-effect view separating simple pre/post from causal control comparison.

### Evidence

Repository has DiD/control lineage, but footwear-size source availability at article/sale grain must be proven.

### Scope

Source discovery plus additive evidence; no causal claim from raw pre/post.

### Read first

RQ520/RQ527; nivelacija DiD/control contracts; article/sale schemas.

### Do

Prove size availability first; if absent stop with explicit unavailable evidence. If present calculate sold/received/on-hand by size per supplier x footwear type. For mature events expose controlled uplift only through approved DiD/control contract with population/maturity/confidence metadata.

### Tests

Fixture size-curve and controlled-effect tests with maturity/control counterexamples.

### Acceptance

Every insight states population, maturity/control basis; raw pre/post is never presented as causal uplift.

### Dependencies

RQ520, RQ527 and RQ500 DONE.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Negative-path acceptance is complete. Size availability at supplier × footwear-type sold/received/on-hand grain is absent; the repo pins that fact in `SupplierAssortmentSizeCurveEvidenceContract`, fixture contract tests, and explicit unavailable panels on Supplier Asortiman. Descriptive pre/post remains non-actionable; controlled DiD uplift is not presented as causal without maturity/control/confidence metadata. Full aggregation panel remains a follow-up when an owner-approved source contract lands.
- Changed files: `Application/Analytics/SupplierAssortmentSizeCurveEvidenceContract.cs`, `Api.Tests/SupplierAssortmentSizeCurveEvidenceContractTests.cs`, `Klijent/clientapp/src/utils/supplierAssortmentSizeCurveEvidence.ts`, `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`, `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.css`, `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_SUPPLIER_AUDIT_ADDENDUM.md`, `.ai/runs/2026-10-02-RQ532-evidence.md`
- Contract/runtime behavior changed: additive unavailable-evidence contract and UI only; no invented size-curve KPIs or causal uplift.
- Checks run: governance validators, `SupplierAssortmentSizeCurveEvidenceContractTests`, focused Supplier Footwear spec, guardrails/typecheck as recorded in run log, `git diff --check`
- Checks not run: full backend/frontend suites — not required for this bounded negative-path delivery.
- Run log: `.ai/runs/2026-10-02-RQ532-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `819afb3ac753911e28f3e279493f075705ea7d83`
- Main verification: `origin/main` contains `819afb3ac753911e28f3e279493f075705ea7d83`; implementation commit `3a1d0978` is ancestor
- Missed: sold/received/on-hand by size per supplier × footwear type and mature controlled-uplift panel await an authoritative backend source contract.
- Follow-up: register/promote a new prompt when repository-owned size/receipt/on-hand + approved DiD population/maturity metadata exist.
- Residual risk: inventory SKU/store size-curve must not be repurposed as supplier assortment evidence; optional article-level `didRevenue` must not be labeled causal on Assortment.
- Next: owner source-contract work before any aggregation implementation.
- Prompt defect / scope repair: prior docs-only close was incomplete relative to prompt tests and explicit unavailable product evidence; repaired in this follow-up.

---

## RQ533 - Return unknown instead of 0% for Assortment change percent without comparable evidence

Status: DONE
Claimed: 2026-10-01 by Cursor agent on `main` (direct). Registered and promoted WAITING -> READY -> IN_PROGRESS in the same idle-recovery run (user authorized promotion). Dependencies RQ520 and RQ527 are DONE; no other active owner, lock or PR touches the vendor-sales-nivelacija change-percent contract.
Priority: P1
Type: backend/frontend-contract/tests
Feature family: supplier-assortment-change-percent-truth
Parallel-safe: no
Owner: Analytics Reliability / Supplier

### Problem

`/api/analytics/vendor-sales-nivelacija` reports a fake 0% change in two cases:

- **Vendor without mature comparable rows** (only immature, or none). Pre and post sums are 0, so `ComputeSemanticChangePercent(0, 0)` returns 0 and `SemanticChangePercentRevenue = 0`.
- **Mature vendor or totals without a revenue baseline** (pre 0, post > 0). The semantic percent is correctly null, but the legacy `ChangePercent` is filled with `semantic ?? 0m`.

The Supplier Asortiman page reads `semanticChangePercentRevenue ?? changePercent`, and Prodaja pre/posle nivelacije reads `changePercent`. Both therefore render 0% where no baseline exists, which violates the no-fake-zero invariant. The error fallback also returns `ChangePercent = 0` in its empty totals.

### Evidence

- `AllEndpoints.cs`: vendor `ChangePercent = semanticChangePercent ?? 0m`; totals `ChangePercent = SemanticChangePercent(totalPre, totalPost) ?? 0m`.
- `VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeSemanticChangePercent(0, 0)` returns 0.
- RQ527 golden snapshot pins vendor Gama (immature only) with semantic 0; RQ527 run log "What was missed".
- `SupplierFootwearAnalyticsPage.tsx` trend fallback; `ProdajaPrePostNivelacijePage.tsx` `trustedMetric(item.changePercent, item)`.

### Scope

Change-percent semantics of the vendor-sales-nivelacija vendor and totals contract, the frontend type/schema that validates them, and the oracle/golden that pins them. Do not change effect statuses, thresholds, windows, cohort, totals population or page layouts.

### Read first

RQ520, RQ527; `VendorSalesNivelacijaPriceChangeEffectPolicy`; the endpoint vendor/totals block; `vendorSalesNivelacijaApi.ts`; `analyticsResponseSchemas.ts`.

### Do

1. Add one policy helper: a cohort change percent is null when the cohort has no mature comparable rows, otherwise it is the existing semantic percent.
2. Use the helper for vendor and totals semantic percent. `ChangePercent` carries the same nullable value; drop the `?? 0m` fallback.
3. Make vendor and totals `ChangePercent` nullable in the DTO and in the frontend type and schema. The error fallback then reports null, not 0.
4. Update the RQ527 oracle and golden snapshot to the same rule.

### Tests

- Policy unit tests: no mature rows gives null; no baseline gives null; a valid baseline keeps the existing value; flat 0/0 with mature evidence stays 0.
- Golden snapshot: the immature-only vendor has null semantic and change percent.
- Static guard: the endpoint uses the helper and no `?? 0m` change-percent fallback remains.
- Frontend: a mature vendor with null change percent renders unavailable, not 0%; the schema accepts null.

### Acceptance

No Assortment vendor or total shows a 0% change without mature comparable evidence and a revenue baseline. A real flat 0/0 mature cohort still shows 0%. Response shape is unchanged apart from nullability.

### Dependencies

RQ520 and RQ527 DONE.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: vendor and totals change percent are now null without mature comparable evidence or without a revenue baseline. A real flat 0/0 mature cohort still reports 0%.
- Changed files:
  - `Application/Analytics/VendorSalesNivelacijaPriceChangeEffectPolicy.cs`
  - `Api/Endpoints/AllEndpoints.cs`, `Api/Models/VendorSalesNivelacijaModels.cs`
  - `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
  - `Api.Tests/AssortmentNivelacijaOracle.cs`, `Api.Tests/AssortmentNivelacijaOracleTests.cs`, `Api.Tests/Golden/assortment-nivelacija-oracle.json`, `Api.Tests/VendorSalesNivelacijaPriceChangeEffectPolicyTests.cs`
  - `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts`, `Klijent/clientapp/src/validation/analyticsResponseSchemas.ts` and their specs
  - `SupplierFootwearAnalyticsPage.spec.tsx`
- Contract/runtime behavior changed:
  - new helper `ComputeCohortChangePercent`;
  - `?? 0m` vendor and totals fallbacks removed;
  - vendor and totals `ChangePercent` is nullable, including the error-fallback totals;
  - best/worst insights explain an unavailable percent instead of printing 0%;
  - cache key v6 changed to v7;
  - the frontend type and schema accept null, and the Asortiman trend cell renders N/A.
- Checks run:
  - golden counterexample: Gama 0 vs null failed before regeneration;
  - `AssortmentNivelacijaOracleTests` pass on Testcontainers;
  - backend slice `Supplier|VendorSales|Nivelacija|Assortment|CacheKey|AnalyticsResponseMeta`: 360 passed, 28 skipped, 0 failed;
  - frontend focused specs: 38/38;
  - `check:analytics-guardrails` and `npm run build` pass;
  - `git diff --check` clean; governance validators pass.
- Checks not run: full `dotnet test` and full vitest suites. `ProdajaPrePostNivelacijePage.spec.tsx` has 8 failures that reproduce with the RQ533 frontend changes stashed, so they are pre-existing and unrelated.
- Run log: `.ai/runs/2026-10-01-RQ533-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 4aadc2cd706e41b512f5f21a78fe9e22c98ed878
- Main verification: `origin/main` contains 4aadc2cd (ancestor check after push)
- Missed: price-direction and category `ChangePercent ?? 0m` in the same endpoint feeds Prodaja pre/posle nivelacije (Q83 owner) and was kept out of scope.
- Follow-up: the Q83 Pre/Post owner should make price-direction and category change percent null without a baseline and repair the pre-existing `ProdajaPrePostNivelacijePage.spec.tsx` failures.
- Residual risk: external consumers that assumed vendor or totals `changePercent` was non-null now receive null.

---

## RQ534 - Align the Pre/Post page spec with the RQ520 price-change effect statuses

Status: DONE
Claimed: 2026-10-01 by Cursor agent on `main` (direct). Registered and promoted WAITING -> READY -> IN_PROGRESS in the same idle-recovery run (user authorized promotion). Dependencies RQ520 and RQ533 are DONE. The frontend spec is outside Q83's SQL-only scope, and no lock, open PR or branch touches it.
Priority: P1
Type: frontend-tests/ci
Feature family: supplier-prepost-effect-status-tests
Parallel-safe: yes
Owner: Analytics Reliability / Supplier

### Problem

Analytics Quality Gates on `main` has been red since `13a01ebd` ("align prepost price effect status UI"). Eight tests in `ProdajaPrePostNivelacijePage.spec.tsx` fail. The page now uses the RQ520 effect statuses (`effective`, `neutral`, `ineffective`, `immature`, `insufficient_data`) and their labels ("Pozitivan efekat", "Prozor u toku", ...), but the spec still seeds the old recommendation statuses (`increase_focus`, `review`) and looks for the old chip names "Pojacaj" and "Pregledaj". Focus filters therefore match nothing: the data table is replaced by the filtered-empty state and the chip queries fail.

### Evidence

- Run 36754474801 (`f187d59e`): no Pre/Post failures. Run 36765810848 (`17a68507`, which includes `13a01ebd`) and every main run since: the same 8 Pre/Post failures.
- `vendorSalesNivelacijaApi.ts` types `recommendation.status` as the five effect statuses. `VendorSalesNivelacijaPriceChangeEffectPolicy` emits only those.
- Local reproduction: 8 failed, 37 passed; the RQ533 changes stashed or applied make no difference.

### Scope

`Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx` only: fixture statuses and labels, chip-name queries and the expected focus-context text. Do not change page behavior, focus URL keys (`increaseFocus`, `review`, ...), backend policy or other specs.

### Do

1. Replace the stale fixture statuses with the matching effect statuses (`increase_focus` becomes `effective`, `review` becomes `immature`) and use backend-style labels.
2. Query the focus chips by their current effect labels and update the expected focus-context text.
3. Keep the URL focus-key assertions unchanged; they are a compatibility contract.

### Tests

- `npx vitest run src/pages/ProdajaPrePostNivelacijePage.spec.tsx`: all tests pass.
- Counterexample: the unchanged spec fails 8 tests on the current page.

### Acceptance

The Pre/Post spec passes on current `main` with fixtures that are valid for the typed effect-status contract, and Analytics Quality Gates no longer fails on this spec.

### Dependencies

RQ520 and RQ533 DONE.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: the Pre/Post spec now seeds the typed effect statuses (`effective`, `immature`) with backend-style labels and queries the current focus chips ("Pozitivan efekat", "Prozor u toku"). The URL focus keys are unchanged. Page behavior was not touched.
- Changed files: `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- Contract/runtime behavior changed: none (test only).
- Checks run: counterexample on the unchanged spec gives 8 failed, 37 passed; after the fix 45/45. `git diff --check` clean; governance validators pass.
- Checks not run: full local vitest (the CI gate on the delivered SHA is inspected instead).
- Run log: `.ai/runs/2026-10-01-RQ534-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: e5cc93c3d83e66144778b31108277f28d8d1eef3
- Main verification: `origin/main` contains e5cc93c3 (ancestor check after push)
- Missed: intermittent `InventoryPage.queueStatus` and `InventoryPage.signalWindow` failures in some main runs are outside this scope.
- Follow-up: an owner for Inventory spec flakiness if it recurs; the Q83 price-direction/category change-percent follow-up from RQ533 remains.
- Residual risk: specs are not type-checked, so other specs may carry status values that are invalid for their contracts.

---

## RQ535 - Reclassify Supplier and Pre/Post gates on the successful Render deploy

Status: DONE
Claimed: 2026-10-01 by Cursor agent on `main` (direct) as the primary READY prompt. No lock, open PR or unmerged branch touches it; RQ536 and Q83 stay available to other agents.
Priority: P0
Type: deployed-smoke/read-only/evidence
Feature family: supplier-post-deploy-gate-reclassification
Parallel-safe: yes
Owner: Analytics Reliability / Runtime QA
Commit suggestion: `test(analytics): reclassify supplier gates after deploy`

### Trigger

Operator report on 2026-10-01: Docker Desktop is available and the Render deploy completed successfully today. This is operator evidence, not yet exact-SHA runtime proof.

### Problem

RQ474/RQ475 and Q83/RQ491 still route from older live evidence (Overview 503, Scorecard MISSING_SCHEMA, Assortment contract missing). Since RQ518-RQ528 and RQ533/RQ534 have landed and a new deploy was reported, those gates may now be stale. Provider/database access must not be required merely to discover whether the public deployed contract is healthy.

### Scope

Read-only public/deployed HTTP evidence and queue reclassification only. No production DB connection, no provider mutation, no worker action, no schema write and no browser/export certification. RQ448 owns full browser/render/export reconciliation; RQ454/STAB16 own production raw-fact reconciliation and provider/worker proof.

### Read first

RQ474, RQ475, Q83, RQ491, RQ448, RQ454, RQ514; current runtime-version endpoint and Supplier/PrePost API clients.

### Do

1. Fetch `/ready` and `/api/runtime/version`; bind evidence to the exact deployed SHA. If the SHA is stale or unknown, stop with a stale-deploy classification.
2. Read-only probe the canonical deployed endpoints for Supplier Overview, Supplier Scorecard, Supplier Assortment and Pre/Post using one explicit period/dataScope plus a valid alternate store/scope where supported.
3. Record HTTP status, safe error code/correlation id, `meta.success`, requested/effective period, `scopeApplied`, readiness/error state and whether the semantic revenue-change contract is actually present.
4. Reclassify, without guessing:
   - RQ474: if Overview is now healthy, mark the historical 503 premise superseded/closure-ready; if 503 persists, keep it gated and attach the new correlation evidence for provider logs.
   - RQ475: if Scorecard/Assortment are healthy on the exact deployed SHA, remove the stale live-readiness gate and promote the next dependency-complete Supplier UX prompt; if not, record the exact current missing/stale object state.
   - Q83/RQ491: live Pre/Post success may satisfy runtime availability evidence, but Q83 repository-local semantic work remains governed by its SQL queue; never infer raw DB reconciliation from HTTP success.
5. Regenerate/update exact-SHA analytics readiness evidence if the existing RQ514 snapshot is stale.
6. Run idle recovery after classification and promote only dependency-complete, collision-safe successors (typically RQ498/RQ499 before RQ500).

### Checks

- read-only GETs only;
- exact deployed SHA captured;
- no credentials/secrets persisted;
- queue/planning validators;
- evidence file with request timestamps and current-main/deployed SHA comparison.

### Acceptance

The repository no longer routes Supplier work from September live evidence when the October deploy proves a different state. Every remaining live/provider gate names the exact evidence still missing. No production write or fake VERIFIED claim occurs.

### Dependencies

None beyond the operator-reported successful deploy; step 1 must independently verify the exact deployed SHA before any gate is considered satisfied.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: public read-only GETs bound to the exact deployed SHA `3a6a6886` (build 2026-10-01T08:53:20Z), which is contained in main; the only runtime delta is RQ533.
  - Health passes and the analytics data is present.
  - Supplier Overview returns 503 on every scope: a catch-all `NpgsqlException` mapping with no correlation id.
  - Scorecard fails closed with `MISSING_OBJECT` (`mv_supplier_decision_score_cache_90d` absent), and its store-filtered request returns an unhandled 500.
  - Assortment and Pre/Post return `vendor_sales_nivelacija_contract_missing` on every scope.
  - The September premises are confirmed with sharper object names, not superseded.
- Reclassification:
  - RQ474 WAITING -> READY (repository-local error contract).
  - RQ475 stays WAITING behind RQ536 (shared surface); its live gate is satisfied and the store-filtered 500 is added to its scope.
  - Q83 gets a runtime evidence line and RQ491 stays behind it.
  - RQ487/RQ498/RQ499/RQ500 unchanged.
  - The RQ514 readiness manifest now carries the deployed SHA and four runtime records; the regenerated snapshot is overall `failed`.
- Changed files:
  - `docs/qa/ANALYTICS_PRODUCTION_READINESS_EVIDENCE.json` and the generated STATUS json/md
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
  - this addendum, `MASTER_ROADMAP.md`
- Contract/runtime behavior changed: none (evidence and routing only).
- Checks run: read-only GETs with timestamps and correlation ids; deployed SHA containment check; `test:analytics-readiness` 6/6; governance validators pass; `git diff --check` clean.
- Checks not run: provider logs, database/schema queries, browser/export (RQ448), raw-fact reconciliation (RQ454/STAB16).
- Run log: `.ai/runs/2026-10-01-RQ535-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: recorded in the run log after push (docs-only close commit)
- Main verification: `origin/main` contains the close commit (ancestor check after push)
- Missed: the live root causes (why the 014/029 objects are absent despite the startup logic, and which SQL error lies behind the Overview 503) remain unproven without Render/Neon logs.
- Follow-up: the operator/STAB16 owner should supply Render startup logs (2026-10-01T08:53Z), Overview error logs (09:37:16Z) and the Neon storage state; the next runnable prompts are RQ474, RQ536 and Q83.
- Residual risk: the 24-hour evidence expiry applies, and a deploy of current main makes this evidence stale.


---

## RQ536 - Version and explain the current Supplier scorecard model without changing policy

Status: DONE
Claimed: 2026-10-01 by ChatGPT on `cursor/rq536-scorecard-explainability-51d0`; re-entered and completed by Codex in the main workspace after .NET/Docker became available. Local lock `.ai/task-locks/RQ536-codex.lock.md` was removed before commit.
Priority: P2
Type: backend/frontend/docs/tests
Feature family: supplier-scorecard-model-explainability-baseline
Parallel-safe: yes
Owner: Analytics Product / Supplier Decision
Commit suggestion: `feat(analytics): expose supplier scorecard model explanation`

### Problem

RQ531 mixes safe explainability/versioning work with owner-gated policy tuning. The current model can be made inspectable now without deciding whether any weight, threshold or gate should change.

### Scope

Current-behavior documentation and machine/UI explainability only. Do not change score weights, percentile formulas, coverage gates, inventory normalization, confidence thresholds, recommendation cutoffs or sample-size policy.

### Read first

RQ521, RQ526, RQ531; migration 029 scorecard formula; Supplier Decision Hub endpoint/page; SupplierScorecardOracle.

### Do

1. Freeze a formula/model version for the exact current behavior.
2. Publish every current component, range, weight, penalty, clamp, gate and cutoff as machine-readable explainability metadata or a stable documented contract.
3. Expose rank/reference population, clamping, current-stock versus historical inputs, and the signed margin-contribution denominator explicitly.
4. Add a per-row contribution/explanation projection sufficient to reproduce the displayed score from the documented current formula.
5. Mark policy choices that lack owner rationale as `owner_decision_pending`; do not alter their values.
6. Update RQ526 oracle/golden proof so current production formula and explainability metadata are version-locked.

### Tests

- oracle reproduces the displayed score/component contributions for all-time/90d/180d fixtures;
- formula version is present and stable;
- contribution sum/clamping/gates match current SQL exactly;
- changing a documented weight in the oracle fails parity until the formula version/contract is intentionally updated;
- UI shows current-stock/relative-rank limitations without presenting them as causal facts.

### Acceptance

A reviewer can explain exactly why a Supplier score/recommendation has its current value and which policy constants require an owner decision, while all numeric policy behavior remains unchanged.

### Dependencies

RQ521 and RQ526 DONE. RQ531 consumes this baseline for later owner-approved policy changes.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: current Supplier scorecard behavior is versioned as `supplier-scorecard-v1`; machine-readable components, weights, transforms, ranges, clamps, gates, owner-decision markers and limitations are exposed through trust metadata; summary/quadrant/ranking/details rows expose contribution explanations and active rank-population provenance; the Supplier Decision Hub renders the model version, relative-rank and current-stock limitations in Serbian. Numeric policy constants remain unchanged.
- Changed files: `Api/Services/SupplierScorecardModelContract.cs`, `Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api.Tests/SupplierScorecardOracle.cs`, `Api.Tests/SupplierScorecardOracleTests.cs`, `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`, `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`, `MASTER_ROADMAP.md`, this addendum
- Checks run: focused Supplier Decision Hub Vitest `17/17`; `npm run check:analytics-guardrails` passed (encoding, guardrail self-test, analytics guardrails and TypeScript); `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` passed with 0 errors; focused `SupplierScorecardOracleTests` passed `15/15` with PostgreSQL Testcontainers; repository governance validators and `git diff --check` passed.
- Checks not run: full backend suite and remote CI; neither is a named RQ536 acceptance gate. The full frontend build passed earlier in the initial run before its final backend-only safety correction; no frontend files changed afterward.
- Run log: `.ai/runs/2026-10-01-RQ536-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `7c2e6a546f6f5f58234c3318fe9f2110cf6eb2c8`
- Main verification: fresh `origin/main` contains the implementation SHA; re-entry used current main and did not change product code.
- Missed: none for RQ536 acceptance; no score-policy decision was made.
- Follow-up: keep RQ531 owner-gated for any numeric policy change; select RQ475/Q83 through fresh queue and collision checks.
- Residual risk: live production/provider cause analysis and the full backend suite were outside RQ536 scope.
- Prompt defect / scope repair: status was `PARTIAL` solely because required backend proof was unavailable; re-entry closed that evidence gap without changing product policy.

