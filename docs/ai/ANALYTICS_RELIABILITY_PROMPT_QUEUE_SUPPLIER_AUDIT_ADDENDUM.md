# Analytics Reliability Prompt Queue — Supplier Audit Addendum

Date: 2026-09-30
Repo: ivanjovicic/Trendplus
Source audit: docs/ai/SUPPLIER_ANALYTICS_DEEP_AUDIT_PROMPTS_2026-09-30.md
Current READY prompt: none
Additional READY prompts: none; RQ527 and RQ528 are DONE
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

Status: WAITING
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

Status: PARTIAL
Claimed: 2026-09-30 by ChatGPT on `cursor/rq524-pack-extend-78b0`; extended the repository-local reconciliation pack and reran deterministic PostgreSQL fixture evidence.
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

Status: IN_PROGRESS
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

Status: WAITING
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

Status: WAITING
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

---

## RQ531 - Govern and explain Supplier scorecard model weights and thresholds

Status: WAITING
Ready after: RQ526 DONE and product owner approves model-policy decisions
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

Model documentation, owner decision, formula versioning/explainability; no unapproved weight/threshold change.

### Read first

RQ521/RQ526; current scorecard formula SQL/UI.

### Do

Document every component/range/weight/penalty/gate/cutoff with rationale or owner-needed marker; expose contributions/rank population/clamping/formula version; owner approves or changes coverage gate, inventory normalization, confidence weights, cutoffs and period-scaled samples; make signed margin-share denominator explicit; label current-stock inputs honestly.

### Tests

Update RQ526 oracle to approved version plus explainability/version API/UI tests.

### Acceptance

Every model policy is versioned, explainable, owner-approved and oracle-matched.

### Dependencies

RQ526 DONE and explicit product-owner decision.

---

## RQ532 - Add Supplier size-curve and controlled markdown effectiveness evidence

Status: WAITING
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
