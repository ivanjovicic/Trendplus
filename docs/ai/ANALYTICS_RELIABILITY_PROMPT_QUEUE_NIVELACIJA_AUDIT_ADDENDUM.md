# Analytics Reliability Prompt Queue — Nivelacija Audit Addendum

Date: 2026-10-01
Repo: ivanjovicic/Trendplus
Source audit: `docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md`
Registration code base: `f2c047b45f405d7dcbebbc19fb1cf4a0a199a9f5`

Owner promotion/claim 2026-10-02: fresh idle recovery verified RQ540 is DONE on current `main`, RQ545 is PARTIAL only for external deployed acceptance, and no RQ claim/lock/branch/open-PR collision remains for the exclusive `pre-nivelacija-scenario-truth` family. Git history proves the intended highlight factor: initial implementation `15f273ff` used `baseline × 30 × (1 + highlightBoost)`; refactor `5a75ad25` removed `1 +` while extracting the stock-capped helper without documenting a deliberate semantic change. RQ537 moved `WAITING -> READY -> IN_PROGRESS`. Local lock: `.ai/task-locks/RQ537-codex.lock.md`.
Owner completion 2026-10-01: `RQ540` moved `IN_PROGRESS -> DONE` with implementation `e8d5ef08fc1e16507a689c1d14c3e9b37f7a7b9a`; closure/evidence synchronization is `e73421614d67b647766fcfc87beb51c627e54640`, and fresh fetch verifies `origin/main` contains both. Sales and markdown query failures now log and throw a typed exception that escapes the cache factory, so transient error payloads are mapped outside caching; request cancellation is rethrown without data-failure logging. Focused API proof is 14/14 after audit regression additions; Release API build passes. Run log: `.ai/runs/2026-10-01-RQ540-evidence.md`; evidence state: synchronized.
Current READY routing: primary `RQ548`; additional parallel-safe READY lanes: `RQ546`, `RQ549`, `RQ550`; additional bounded UI READY: `RQ554`. RQ537/RQ540/RQ547 are DONE. RQ545 (NV-F9) is PARTIAL only for deployed endpoint acceptance with RQ535/STAB16. RQ538/RQ539 stay WAITING until the RQ548 pre-change oracle baseline is captured; RQ541 follows proof-first RQ550; RQ543/RQ542 follow proof-first RQ549 so the oracle cannot be rewritten around the fix.
Owner recovery 2026-10-03 after RQ563: the prior "no safe RQ prompt" conclusion was superseded by a deeper dependency audit. RQ548 depends only on DONE RQ525; RQ546 has no dependencies; RQ554's required backend error states are already delivered by the code-side portion of PARTIAL RQ545 and its remaining deployed acceptance is not a start gate. Two circular queue defects were also found: RQ542<->RQ549 and RQ541<->RQ550. Both are repaired proof-first: RQ549/RQ550 must pin current defects before RQ542/RQ541 change semantics. Fresh GitHub checks found no open PR or matching RQ538-RQ550 task branch/lock. Primary READY is RQ548; additional READY lanes are RQ546/RQ549/RQ550/RQ554.
Registered range: RQ537–RQ559.
Direct integration fixes: core NV-F9 relation capability and core NV-F10 preload suppression only. Business-model and data-semantic changes remain queued until their proof/owner gates are satisfied.

## Registration map

| Audit id | Queue id | Initial status |
|---|---|---|
| NV-F1 | RQ537 | DONE |
| NV-F2 | RQ538 | WAITING |
| NV-F3 | RQ539 | WAITING |
| NV-F4 | RQ540 | DONE |
| NV-F5 | RQ541 | WAITING |
| NV-F6 | RQ542 | WAITING |
| NV-F7 | RQ543 | WAITING |
| NV-F8 | RQ544 | WAITING |
| NV-F9 | RQ545 | PARTIAL |
| NV-F10 | RQ546 | READY |
| NV-P1 | RQ547 | DONE |
| NV-P2 | RQ548 | READY |
| NV-P3 | RQ549 | READY |
| NV-P4 | RQ550 | READY |
| NV-I1 | RQ551 | WAITING |
| NV-I2 | RQ552 | WAITING |
| NV-I3 | RQ553 | WAITING |
| NV-I4 | RQ554 | READY |
| NV-I5 | RQ555 | WAITING |
| NV-E1 | RQ556 | WAITING |
| NV-E2 | RQ557 | WAITING |
| NV-E3 | RQ558 | WAITING |
| NV-E4 | RQ559 | WAITING |

## Reconciliation rules

- Do not reopen DONE RQ518–RQ535 merely because an older audit line references their former state, and do not duplicate the separately active Q83 SQL residual.
- RQ545 (NV-F9) is residual after the direct catalog/search-path hotfix: repair-preflight parity, privilege/search-path diagnostics, PostgreSQL proof and deployed verification.
- RQ546 (NV-F10) is residual after the direct event-suppression hotfix: route-level retry/error-boundary UX and config hardening.
- RQ537 (NV-F1) must establish intended highlight semantics before changing recommendation arithmetic; current git history does not prove the intended multiplier.
- Production/provider writes are not authorized by registration. Use read-only proof first where the prompt says PROVE.
- Existing queue collisions/dependencies remain authoritative. Re-run collision/dependency checks before WAITING -> READY.

---

## RQ537 - NV-F1 - Fix the Pre-Nivelacija highlight scenario multiplier and pin scenario semantics

Status: DONE
Priority: P1
Type: backend/tests
Feature family: pre-nivelacija-scenario-truth
Parallel-safe: no (`Api/Services/PreNivelacijaScoringService.cs`)
Owner: Analytics Reliability / Pricing
Findings: NV-N01
Commit suggestion: `fix(analytics): make pre-nivelacija highlight scenario an uplift, not a drop`

#### Problem

`SimulateScenarios` computes the highlight scenario as `baseline × 30 × (0.15 + score/100 × 0.30)`. That predicts 15–45% of baseline sales, so highlighting looks like a 55–85% sales drop. The markdown scenario predicts about 1.05× baseline revenue. As a result `RevenueDeltaHighlightVsMarkdown` is negative for almost every unconstrained row, `increase_focus` ("Pojačaj") is rare, and the "Procena povećanja prihoda" KPI is empty or biased.

#### Evidence

- `Api/Services/PreNivelacijaScoringService.cs:124-125` (highlight boost), `:132-136` (markdown boost `1 + d × 1.8`), `:232-243` (`baseline × 30 × boost`).
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:412-415`, `:431-447`, `:560-565` (KPI eligibility).
- `Api.Tests/PreNivelacijaScoringServiceTests.cs:77-78` only asserts `>= 1` unit.

#### Scope

- Scenario arithmetic, reason codes and tests only.
- Weights and thresholds stay as they are (owner-gated, see NV-E1).
- Bump `FormulaVersion` from `pre_nivelacija_v8` and the cache key.

#### Read first

`AGENTS.md`; RQ489–RQ493 sections; `PreNivelacijaScoringServiceTests.cs`; `PreNivelacijaKpiDefinitionTests.cs`.

#### Do

1. Make the highlight multiplier `1 + boost` (same boost range), or name and document the intended semantics if the drop was deliberate. If you can't establish the intent from git history, stop and record that.
2. Assert the invariant `highlightUnits >= baselineUnits` when stock is not binding, and `markdownUnits >= baselineUnits`.
3. Expose the scenario assumptions in `formulaDescription`/`modelEvidence`: highlight uplift range, markdown depth, elasticity 1.8 and the stock cap.
4. Re-run the KPI definitions and confirm `ExpectedHighlightRevenueUplift` is computed from the corrected deltas.

#### Tests

- Table tests: unconstrained stock gives highlight units above baseline; a stock-capped case gives equal units, so the revenue delta is driven by price only.
- Fix the existing KPI tests so they don't rely on negative deltas.

#### Acceptance

On a fixture with unconstrained stock and a high score, the highlight scenario is at least the baseline and the status distribution is no longer biased toward `review`. The formula version has changed.

#### Dependencies

None. NV-P2 adds the end-to-end oracle.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Restored highlight scenario uplift to 1.15–1.45× smoothed baseline based on original git-history semantics; documented scenario assumptions, bumped formula/cache versions, and verified the revenue-uplift KPI consumes the corrected positive delta.
- Changed files: scoring service, Pre-Nivelacija model/endpoint metadata, analytics cache-key builder, focused scoring/KPI/cache tests, queue/roadmap routing and `.ai/runs/2026-10-02-RQ537-evidence.md`.
- Checks run: focused API tests 36/36; prompt queue governance 671 tasks; `git diff --check`.
- Checks not run: live PostgreSQL/API/browser proof; these were not assigned acceptance gates for the repository-local arithmetic change.
- Run log: `.ai/runs/2026-10-02-RQ537-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `4759ad66bb7102252e6312fb2fda6539fa356fd3`
- Main verification: fresh `git fetch origin main` confirmed exact `HEAD == origin/main == 4759ad66bb7102252e6312fb2fda6539fa356fd3`; `git merge-base --is-ancestor` passed.
- Missed: live runtime/browser proof; no in-scope local acceptance remains.
- Follow-up: fresh RQ selector/recovery.
- Residual risk: heuristic scenarios remain uncalibrated and do not establish causal uplift.
- Prompt defect / scope repair: reconciled stale RQ537 semantic-gate note with git evidence: initial commit `15f273ff` used `1 + highlightBoost`; refactor `5a75ad25` removed it without documenting a deliberate meaning change.

---

---

## RQ538 - NV-F2 - Attribute, validate and consistently scope application-created nivelacija events

Status: WAITING
Priority: P1
Type: backend/frontend/sql/tests
Feature family: nivelacija-event-provenance
Parallel-safe: no (`AllEndpoints.cs` nivelacija write/read, scoped SQL, Pre-Nivelacija markdown lookup)
Owner: Operations / Analytics Reliability
Findings: NV-N02, NV-N03, NV-N17
Commit suggestion: `fix(nivelacija): record store, supplier and user on price changes and align event scope`

#### Problem

`POST /api/nivelacija` writes a `DnevnikPromena` row without `IDObjekat`, without `DobavljacId` and with `KorisnikIme = "System"`.
- There is no server-side validation: price ≤ 0, an unchanged price and extreme markdowns are all accepted.
- It returns `ex.Message` in the problem detail.

The three analytics surfaces then treat these chain-wide events differently:
- the canonical view ignores the store;
- the scoped Pre/Post SQL drops NULL-store events whenever a store filter is set;
- the supplier split includes them;
- Pre-Nivelacija matches on the exact (article, store) pair, so it never sees them. Its "markdown opportunity" is inflated and the repeated-markdown alert never fires.

The `/nivelacije` overview lists only `'Nivelacija cena'`, so imported history is invisible. It also treats `datetime-local` input as UTC and uses an inclusive `<=` end.

#### Evidence

- `Api/Endpoints/AllEndpoints.cs:6611-6650` (`:6628-6638` row, `:6637` user, `:6647` `ex.Message`), `:8618` DTO.
- `AllEndpoints.cs:7913` (scoped store predicate), `:1305` (split store predicate), `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:94-105` (all-store sales).
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:257-292,334`.
- `AllEndpoints.cs:6667-6671,6676,6698-6699`; `Klijent/clientapp/src/pages/NivelacijePage.tsx:36-53`.

#### Scope

- The write path, one explicit event-scope policy shared by the three readers, and the overview filter.
- No historical data rewrite. The repair tool handles that if the owner approves it.

#### Read first

`TipPromeneConstants`; `NivelacijaRepairService.cs`; `VendorSalesNivelacijaCohortPolicy`; RQ492 (store grain); `SalesReceiptPopulationPolicy`.

#### Do

1. Extend `NivelacijaRequest` with an optional `StoreId`. NULL means chain-wide, and the UI must say so explicitly.
2. Set `DobavljacId` from the article, `KorisnikIme` from the authenticated user, `DataOrigin = 'existing'`.
3. Validate on the server: new price > 0, new ≠ old, and a configurable maximum markdown with explicit override.
4. Replace `ex.Message` with the safe analytics error contract plus a correlation id.
5. Introduce one `NivelacijaEventScopePolicy`: a chain-wide event (NULL store) applies to every store; a store event applies only to its store. Apply it in the scoped SQL (`d."IDObjekat" IS NULL OR d."IDObjekat" = @storeId`), in the Pre-Nivelacija lookup (fall back from (article, store) to (article, NULL)) and in the split.
6. Make `/api/nivelacije` list both `TipPromene` values with a type column. Interpret date filters as Europe/Belgrade calendar days with an exclusive end.

#### Tests

- Write-path tests: rejects ≤ 0 and unchanged prices; records store, supplier and user; no raw exception text.
- Scope-policy tests: a chain-wide event is visible under every store filter on all three readers; a store event only under its store.
- Overview tests: imported `'Nivelacija'` rows are listed; a 23:30 local event falls on the right day.

#### Acceptance

The same event is counted identically, for the same store filter, by Pre/Post, Pre-Nivelacija and the supplier split.

#### Dependencies

No hard implementation dependency. Routing rule: capture the independent RQ548 pre-change oracle/golden baseline first because RQ538 changes the same event/store semantics that the oracle must prove. Keep the Q83 nullability contract when touching the scoped SQL.

---

---

## RQ539 - NV-F3 - Stop ranking new arrivals and below-cost items as markdown priorities

Status: WAITING
Priority: P1
Type: backend/frontend/tests
Feature family: pre-nivelacija-candidate-truth
Parallel-safe: no (`PreNivelacijaPriorityEndpoints.cs`, `PreNivelacijaScoringService.cs`)
Owner: Analytics Reliability / Pricing
Findings: NV-N04, NV-N05
Commit suggestion: `fix(analytics): separate never-sold and new stock from stale stock in pre-nivelacija`

#### Problem

An article without a positive sale in 180 days gets `daysSinceLastSale = 999`, so recency risk and velocity risk are both 100. Stock received yesterday therefore ranks like dead stock, and the UI prints "999".

Gross margin is clamped to 0–100%, and the scenario margin is `max(0, price − cost)`. Items already priced below cost look like 0% margin, and the markdown loss below cost disappears from "izbegnut gubitak".

#### Evidence

- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:327-329,757-758,163-164`.
- `Api/Services/PreNivelacijaScoringService.cs:69-73,128,138`.
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx:1550,1640`.

#### Scope

- Candidate evidence, display and reason codes.
- No new weights (see NV-E1).

#### Read first

`PreNivelacijaPopulationTests`, `PreNivelacijaMarginEvidenceTests`, `AnalyticsMarginPolicy`, the `DnevnikPromena` `'Ulaz robe'` semantics.

#### Do

1. Derive `firstReceiptDate` per (article, store) from `DnevnikPromena` `'Ulaz robe'`, falling back to the first sale. If neither exists, set it to unknown with an explicit reason; `CreatedAt` is import time, not receipt.
2. Replace the 999 sentinel with nullable `daysSinceLastSale` plus `salesHistoryStatus` (`never_sold`, `no_sale_in_window`, `sold`).
3. Exclude, or put into a separate "nova roba" queue, articles younger than a configurable minimum age (default proposal: 30 days; owner confirms). Give them a reason code instead of a high score.
4. Keep the unclamped margin (may be negative) as `grossMarginPctSigned`, plus a `below_cost` flag. Scenario margin must allow negative values. "Izbegnut gubitak" must count below-cost markdowns.
5. In the UI show "Nikad prodato" or "Nema prodaje u 180 d" instead of 999, and a below-cost badge.

#### Tests

- Fixtures: (a) received 3 days ago, no sales → not high priority, reason `new_stock`; (b) received 200 days ago, never sold → high; (c) price below cost → negative margin and a below-cost flag; the scenario margin is negative.
- A frontend spec for the labels.

#### Acceptance

There are no 999 values in the API or UI, new arrivals are not ranked as markdown candidates, and negative margins are visible.

#### Dependencies

RQ537/NV-F1 is DONE. Route after RQ548 captures the never-sold/new-stock/below-cost baseline; the configurable minimum-age policy remains explicit rather than silently hard-coded by the test.

---

---

## RQ540 - NV-F4 - Never cache transient Pre-Nivelacija failures or client cancellations

Status: DONE
Priority: P1
Type: backend/tests
Feature family: pre-nivelacija-cache-truth
Parallel-safe: yes (cache factory only)
Owner: Analytics Reliability
Findings: NV-N06
Commit suggestion: `fix(analytics): do not cache pre-nivelacija query failures`
Completion: delivered on main through `e8d5ef08fc1e16507a689c1d14c3e9b37f7a7b9a`; focused API proof 12/12 and Release API build pass. Run log: `.ai/runs/2026-10-01-RQ540-evidence.md`; evidence state: synchronized.

#### Problem

Inside `cache.GetOrSetAsync`, a failed sales query (a bare `catch` with no logging, which also swallows `OperationCanceledException`) or a failed markdown query returns `BuildEmptyBaseEntry(... error meta)`. `HybridCacheService` stores whatever the factory returns for 20 minutes (`HeavyAnalytics`). One aborted request or a short database blip therefore shows "nije dostupno" to every user for 20 minutes.

#### Evidence

- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:99-105,243-247,294-305,498`.
- `Infrastructure/Services/Caching/HybridCacheService.cs:271-272`; `IAnalyticsCacheService.cs:501`.

#### Do

1. Rethrow `OperationCanceledException` when `ct.IsCancellationRequested`.
2. Log the sales failure the same way the markdown failure is logged.
3. Throw a typed `PreNivelacijaQueryFailedException` out of the factory, so nothing is cached. Map it to the existing error meta outside the cache. As an alternative, add a `shouldCache` predicate to `GetOrSetAsync` and use it for every analytics factory that returns error entries; check the other callers.
4. Keep the existing `BuildQueryFailureMeta` codes.

#### Tests

- A factory failure followed by a success on the next call returns data (fake cache plus a failing db stub).
- Cancellation produces no cache entry and no "unavailable" payload for the next caller.

#### Acceptance

After a failure the cache contains no error entry. Cancellation is not logged as a data failure.

#### Dependencies

None.

---

---

## RQ541 - NV-F5 - Replace the unequal-window first-nivelacija split in Supplier, Footwear-type and Color stats

Status: WAITING
Priority: P1
Type: backend/frontend/tests
Feature family: nivelacija-split-semantics
Parallel-safe: no (`AllEndpoints.cs` supplier, shoe-type and color stats; `AnalyticsNivelacijaSplitPolicy.cs`)
Owner: Analytics Reliability / Supplier
Findings: NV-N07, NV-N08
Commit suggestion: `fix(analytics): compare equal pre/post windows in nivelacija split`

#### Problem

`AnalyticsNivelacijaSplitPolicy.Build` splits the selected period's sales at each article's first-ever nivelacija and compares the pre and post totals. The two windows have different lengths. The first nivelacija may also predate the period, which makes every sale "post" and the article non-comparable.

The result is published as `prePostNivelacijaRevenueImpactPct` and as the legacy alias `promenaPrometa`, and it gates the supplier recommendation. Pre/Post and the scorecard use other cohorts (latest and first respectively, with fixed 30-day windows), so the same supplier shows three different "nivelacija effects".

#### Evidence

- `Application/Analytics/AnalyticsNivelacijaSplitPolicy.cs:56-105,170-176`.
- `Api/Endpoints/AllEndpoints.cs:1301-1312,1463-1470,1615-1626,1800-1805`; shoe-type `:2283,2465`; color `:3062,3172`.
- `AllEndpoints.cs:4488` (latest-event cohort); `029_AddSupplierDecisionWindowedViews.sql:26-27,93` (first-event cohort).

#### Scope

- Split semantics and naming.
- No change to the PoP metrics (`popRevenueChangePct`), which are separate.

#### Do

1. Choose one owner-approved cohort for "nivelacija effect" across surfaces. Proposal: per-event fixed windows of equal length (30 + 30 days) with maturity, reusing the canonical view/scoped source, and the latest mature event per article inside the period.
2. If the split stays a separate concept, normalize it per day (`avg daily revenue post / pre`) with an equal-length cap (`min(pre_days, post_days)` on each side), and rename it so it doesn't read as a causal effect.
3. Stop aliasing it as `promenaPrometa`. Deprecate the alias with a contract note and update the frontend consumers.
4. Re-evaluate the recommendation gate on the corrected signal.

#### Tests

- Unequal-window counterexample: the event on day 5 of 30 with a flat daily rate must give ≈0%, not +400%.
- An event before the period start makes the article non-comparable with an explicit reason.
- Cross-surface parity: the same article and event give the same effect on the Supplier stats and Pre/Post (feeds NV-P4).

#### Acceptance

A flat daily sales rate always reads as ≈0% effect, and every surface names its cohort in `meta.basis`.

#### Dependencies

RQ550/NV-P4 proof-first oracle, plus the DONE RQ528 parity contract as context.

---

---

## RQ542 - NV-F6 - Make DiD/control, OOS, momentum and elasticity event-aligned and honest

Status: WAITING
Priority: P2
Type: sql/backend/tests
Feature family: nivelacija-causal-signals
Parallel-safe: no (`016_AnalyticsNivelacijaEnhancements.sql`, `AllEndpoints.cs:7233-7514`)
Owner: Analytics Reliability
Findings: NV-N09, NV-N10, NV-N11 (backend part)
Commit suggestion: `fix(analytics): align nivelacija DiD, OOS and elasticity with the event`

#### Problem

**Control group.** It is defined as the articles that are absent from `price_history`, but `price_history` is a one-time 013 backfill with no runtime writer. Articles marked down after the backfill count as "never marked down", and a test article can match itself (distance 0), which gives DiD = 0. Controls are not required to have stock or sales in the window.

**Mappers.** None of them is tied to the event:
- DiD is averaged over all events of a SKU, regardless of the row and period;
- OOS is an all-time average;
- momentum is the current value;
- everything is joined by PLU across sizes and stores, and store and `dataScope` are ignored.

**Elasticity.** It is a single unbounded point, with markups mixed in.

#### Evidence

- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql:12-28,62-70,91-106,143-144`; `013_AddVendorSalesNivelacijaViews.sql:164-189`.
- `Api/Endpoints/AllEndpoints.cs:7255-7271,7303-7309,7369-7376,7399-7406,7451-7484`.

#### Scope

- Control definition, event alignment and guards.
- No new causal claims in the UI (NV-E3 owns the controlled-effect product).

#### Do

1. Define treatment from `DnevnikPromena` (the same predicate as the view), not from `price_history`. Exclude from the controls every article with any nivelacija in `[event − 60d, event + 30d]` and the test article itself. Require control stock > 0 and at least one sale in the pre window.
2. Add a date predicate to the control join (`f.day >= event − 30 AND f.day < event + 30`).
3. Return DiD per `price_event_id` and map it by event id, not by SKU average. Do the same for OOS rate inside the post window, from a dated stock source if one exists; otherwise return unavailable with a reason.
4. Compute elasticity only for mature, non-low-signal markdowns (`new < old`) with |Δp| ≥ 5%. Cap or winsorize it and label it a point estimate. Use one aggregation (revenue-weighted median or weighted mean) everywhere and return it in the DTO.
5. Respect the store and `dataScope` in every mapper, or mark the metric `scope_not_applied`.

#### Tests

- Real-PostgreSQL fixtures (NV-P3): a post-backfill markdown article is not a control; no self-match; DiD maps to the right event when a SKU has two events; elasticity is null for a markup or an immature window.

#### Acceptance

No row has `control_article_id = article_id`, DiD is event-specific, and a single elasticity definition is documented and used everywhere.

#### Dependencies

RQ549/NV-P3 proof-first oracle. After the oracle lands, RQ543 should establish direction/maturity/overlap fields before RQ542 consumes them. RQ532 consumes the corrected DiD.

---

---

## RQ543 - NV-F7 - Declare event semantics in the canonical nivelacija views (direction, overlap, maturity)

Status: WAITING
Priority: P2
Type: sql/backend/tests
Feature family: nivelacija-view-semantics
Parallel-safe: no (`Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`, `029`)
Owner: Analytics Reliability (keep the Q83 nullability contract)
Findings: NV-N12, NV-N13, NV-N27
Commit suggestion: `feat(sql): expose nivelacija direction, overlap and post-window maturity`

#### Problem

The view mixes several kinds of event:
- markups and markdowns;
- several changes on the same day;
- events whose windows overlap.

An immature post window with partial sales is returned as if it were a full window.

The endpoint has its own maturity guard (RQ520/RQ533), but the DiD and the scorecard consume the raw view:
- `dead_stock_rate` counts events younger than 30 days as "no sales";
- `avg_did_*` coalesces a missing DiD to 0;
- "markdown dependency" is `post/(pre+post)`.

The view comment also names the wrong sources.

#### Evidence

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:72-78,88,130-133,181-188,194-197,293-294`.
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql:64-65,70-71,116-122,133-134`.

#### Do

1. Add columns at the end (keep the column order guard at `:214-222` in sync): `price_direction` (`markdown`/`markup`/`flat`), `discount_depth_pct`, `post_window_complete` (`event_date + 30 <= CURRENT_DATE`), `overlaps_next_event` and `next_event_date`, `same_day_event_count`.
2. Don't change the existing column semantics; Q83 owns nullability.
3. In 029, use only `post_window_complete AND price_direction = 'markdown'` for `dead_stock_rate`, the post shares and the DiD. Keep the DiD NULL when it is missing (or report coverage next to it) instead of `COALESCE(…, 0)`.
4. Rename or document "markdown dependency" so 50% reads as flat.
5. Fix the view comment.

#### Tests

- Extend the RQ527 golden fixture: a markup row, two events 10 days apart (overlap flagged), an immature event (`post_window_complete = false`), and a 029 dead-stock case that excludes the immature event.

#### Acceptance

Consumers can filter mature markdowns without re-deriving them, and the scorecard excludes immature or markup events.

#### Dependencies

Q83 DONE (keep its nullability contract). NV-F6 consumes `price_direction`.

---

---

## RQ544 - NV-F8 - Harden the 014 event normalization and integer casts

Status: WAITING
Priority: P2
Type: sql/tests/data-safety
Feature family: nivelacija-event-normalization
Parallel-safe: yes
Owner: Analytics Reliability / Data
Findings: NV-N14, NV-N15
Commit suggestion: `fix(sql): make nivelacija event normalization explicit and overflow-safe`

#### Problem

The startup data UPDATE rewrites `TipPromene` using broad `ILIKE` patterns:
- `%povrat%` becomes `'Povrat kupca'`, which can mislabel supplier returns;
- `%nivel%` becomes `'Nivelacija'`, which can turn a cancelled or reversed nivelacija into a markdown.

Both the view and the normalization cast any all-digit `BrojRacuna` to `integer`. A value with more than 10 digits fails the whole statement (`22003`).

#### Evidence

- `Database/Migrations/014_NormalizeNivelacijaEvents.sql:13-28,41-42`; `Infrastructure/Seed/DatabaseInitializer.cs:978`.
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:84-87`.

#### Scope

- Do not run anything against production.
- Use the NV-P1 inventory first. If it shows no risky values, still replace the patterns with an explicit allow-list so future imports are safe.

#### Do

1. Replace the `ILIKE` rewrites with an explicit mapping table (source value → canonical value) that excludes storno/poništ/dobavljač variants. Log the unmapped values instead of guessing.
2. Guard the casts with `length(BrojRacuna) <= 9`, or cast to `bigint` and compare against `"Id"::bigint`.
3. Make the normalization idempotent and auditable: write a count per mapping to the startup log.

#### Tests

- SQL fixture: `'Povrat dobavljaču'` is unchanged; `'Storno nivelacije'` is unchanged and flagged; a 12-digit `BrojRacuna` doesn't break the view.

#### Acceptance

No broad pattern rewrites remain, and the view survives long numeric receipt numbers.

#### Dependencies

NV-P1 (value inventory) as evidence input.

---

---

## RQ545 - NV-F9 - Diagnose the live Pre/Post `contract_missing` (schema/privilege mismatch hypothesis)

Status: PARTIAL
Registration note: (Q83 is DONE; live application stays with RQ535/STAB16, so this is the code-side diagnosis lane)
Priority: P1
Type: backend/diagnostic/tests
Feature family: nivelacija-live-contract
Parallel-safe: yes (column-check helper)
Owner: Analytics Reliability / Runtime
Findings: NV-N16
Commit suggestion: `fix(analytics): resolve nivelacija contract columns through pg_catalog on the search path`

Integration delta: core endpoint capability landed in `c9b0ccf4` with contract proof in `18bf77af`; this RQ retains repair-preflight parity, privilege/search-path diagnostics, real-PostgreSQL proof and deployed verification.

#### Problem

On deployed SHA `3a6a6886`, Pre/Post and Assortiman still return `vendor_sales_nivelacija_contract_missing` in every scope, after the lifecycle fixes from RQ519.

The endpoint checks `information_schema.columns WHERE table_schema = 'public'`. The initializer creates the view in `current_schema()` and checks `current_schemas(FALSE)`. If the deployed `search_path` or role differ, startup reports "ready" while the endpoint reports "missing" forever. `information_schema` also hides the columns of relations the API role cannot read.

#### Evidence

- `Api/Endpoints/AllEndpoints.cs:3962-3984,8588-8609` (`:8597`).
- `Infrastructure/Seed/DatabaseInitializer.cs:762-775,818-835` (`:827`).
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:27,45,227`; `.ai/runs/2026-10-01-RQ535-evidence.md:25,35`.

#### Scope

- The endpoint column check plus read-only diagnostics.
- No production writes.
- Don't change the SQL nullability semantics (Q83).

#### Do

1. Replace the endpoint check with a shared helper based on `to_regclass('vw_vendor_sales_nivelacija')` and `pg_attribute`, on the same search path the query uses (the same helper as the initializer).
2. Add to the `contract_missing` meta which piece is missing: the relation itself, the schema or the column.
3. Add a read-only diagnostic (admin or readiness) that reports `current_user`, `current_schemas(true)`, `to_regclass(...)`, the `has_table_privilege` result and the column list.
4. If the view exists but the column truly doesn't, record that; the live re-apply goes to RQ535/STAB16.

#### Tests

- Real-PostgreSQL: create the view in a non-`public` schema on the search path; the endpoint must find the column. Revoke SELECT: the meta must say `privilege_missing`, not `contract_missing`.

#### Acceptance

The deployed endpoint either serves data or names the exact missing object, schema or privilege.

#### Dependencies

Q83 DONE (SQL contract); RQ535/STAB16 (live application).

Claim note 2026-10-01: fresh `origin/main` is `c99c0208aa3c2cb9fbbbc25947aea25cebfe81c5`; RQ547 is DONE, and Q83/RQ535 dependencies are DONE. No active RQ545 lock, branch or open PR owns the `nivelacija-live-contract` column-check helper. The audit integration run log sequences RQ545 after RQ547. RQ545 moved `WAITING -> READY -> IN_PROGRESS`; local lock `.ai/task-locks/RQ545-codex.lock.md`. Scope is the code-side contract diagnostic and real-PostgreSQL proof; deployment verification remains read-only under the existing RQ535/STAB16 owner.

### Completion note

- Date: 2026-10-01
- Status: PARTIAL
- Completion: Delivered shared search-path-aware contract inspection, exact relation/schema/column/privilege classification, an admin-only read-only diagnostic and focused PostgreSQL proof.
- Changed files: `Infrastructure/Database/PostgresRelationInspector.cs`, `Infrastructure/Seed/DatabaseInitializer.cs`, `Api/Services/VendorSalesNivelacijaContractInspector.cs`, `Api/Dtos/AnalyticsContractDiagnosticDto.cs`, `Api/Dtos/AnalyticsResponseMetaDto.cs`, `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/AdminConfigEndpoints.cs`, `Api.Tests/VendorSalesNivelacijaContractDiagnosticsTests.cs`, `Api.Tests/VendorSalesNivelacijaNullabilityContractTests.cs`, this addendum, `MASTER_ROADMAP.md` and `.ai/runs/2026-10-01-RQ545-evidence.md`.
- Checks run: focused Release tests 40/40 including PostgreSQL Testcontainers; prompt-queue self-test passed; planning architecture self-test and full validation passed (79 tasks); `git diff --check` passed.
- Checks not run: deployed endpoint/live database verification, owned by RQ535/STAB16; full backend suite. Planning Governance runs `36872716556` and `36872866328` are red only for the unrelated P-UI-27 missing `Residual risk:` field. Analytics Tests & Data Integrity run `36872716532` was in progress at final inspection. Full queue validation reports the same unrelated issue.
- Run log: `.ai/runs/2026-10-01-RQ545-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `8ca3d49a2b0184bd9ce18a55b368e8bdf1b6b1af`
- Main verification: passed; fresh `origin/main` exactly matched the implementation SHA at verification.
- Missed: deployed acceptance has not yet shown whether the live endpoint serves data or names the exact live relation/schema/privilege issue.
- Follow-up: RQ535/STAB16 to perform deployed read-only verification; refresh RQ545 acceptance evidence after that check.
- Residual risk: live database role, view schema and connection search path are not verified.
- Prompt defect / scope repair: no defect found; kept production changes and writes outside RQ545.

---

---

## RQ546 - NV-F10 - Do not swallow failed lazy-route imports in chunk-load recovery

Status: READY
Priority: P1
Type: frontend/tests
Feature family: frontend-chunk-recovery
Parallel-safe: yes (`chunkLoadRecovery.ts`, `ErrorBoundary.tsx`)
Owner: Frontend Platform
Findings: NV-N35 (L10, L11)
Commit suggestion: `fix(ui): only suppress vite preload errors when a reload actually happens`

Integration delta: the proven preload cooldown defect landed in `e1da86fa` with regression proof in `87e1982c`; this RQ retains route-level retry/error-boundary UX and Vercel/config hardening.

#### Problem

`installChunkLoadRecovery` always calls `event.preventDefault()` on `vite:preloadError`, but `recoverFromChunkLoadError` reloads only once per 30 seconds. In Vite 7 a prevented preload error makes `__vitePreload` resolve to `undefined`. A second stale-chunk failure inside the cooldown is therefore swallowed: `React.lazy` reads `undefined.default` and the global error boundary shows "Nešto je pošlo naopako". The live audit saw exactly that `TypeError` together with 404s on hashed chunks after a Vercel deploy.

#### Evidence

- `Klijent/clientapp/src/utils/chunkLoadRecovery.ts:22-48`; `src/main.tsx:17`; `src/components/ErrorBoundary.tsx:19-23`; `src/App.tsx:16+` (58 `lazy` routes); `vite.config.ts:15-19`; `package.json:55` (`vite ^7.3.0`).
- `vercel.json` (root and `Klijent/clientapp/vercel.json`): the rewrite excludes `/assets/`, `index.html` is `no-store` and assets are `immutable`, so this is correct and needs no change.

#### Do

1. Call `preventDefault()` only when `recoverFromChunkLoadError` returns `true` (a reload was triggered). Otherwise let the error propagate.
2. Wrap route `lazy()` imports in a helper that retries the import once with a cache-busting query. If that fails and a reload is allowed, reload. If not, throw a typed `ChunkLoadError`.
3. Make `ErrorBoundary` recognize `isChunkLoadError` (and the "reading 'default'" signature as a fallback) and show "Nova verzija aplikacije je dostupna — Osveži" instead of the generic crash.
4. Keep a single `vercel.json` (the one Vercel actually uses) and delete or document the other one.

#### Tests

- Unit: within the cooldown, the event is not prevented and the error propagates; outside the cooldown it is prevented and a reload happens.
- A lazy helper test with a failing import followed by a successful retry.
- An ErrorBoundary test for the chunk-error UI.

#### Acceptance

A stale deploy never produces "reading 'default'". The user gets either an automatic reload or an explicit refresh prompt.

#### Dependencies

None.

---

## RQ547 - NV-P1 - Read-only nivelacija reconciliation SQL pack

Status: DONE
Registration note: (recommended first; unblocks hypotheses)
Priority: P1
Type: sql/tests/evidence
Feature family: nivelacija-reconciliation
Parallel-safe: yes (new files only)
Owner: Analytics Reliability
Findings: NV-N09, NV-N12, NV-N14, NV-N15, NV-N16, NV-N19, NV-N20 (hypothesis proof)
Commit suggestion: `test(sql): add read-only nivelacija reconciliation pack`

#### Problem

Several findings are hypotheses whose impact depends on the data:
- the `TipPromene` values;
- `BrojRacuna` lengths;
- `price_history` staleness and control self-matches;
- overlapping events and the markup share;
- the VAT basis of `cena` vs `NabavnaCena`;
- the timestamp type of `datum_prodaje`;
- the schema and privileges of the view.

#### Evidence

- The seven mapped findings (`NV-N09`, `NV-N12`, `NV-N14`, `NV-N15`, `NV-N16`, `NV-N19`, `NV-N20`) are explicitly marked as data/deployment hypotheses in `docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md`.
- The audit integration run log records that no live SQL/API/schema proof was run and recommends this pack as the first evidence step after fresh dependency/collision recovery.
- RQ524/RQ525 are DONE and provide the read-only SQL/Testcontainers patterns; Q83 is DONE and owns the nullability behavior this pack must observe without changing.

#### Scope

- New files only under `scripts/sql/nivelacija-reconciliation/` and the minimum new Testcontainers bootstrap/test files needed to exercise them.
- Each check returns stable `check_id`, `verdict`, `observed`, `expected`, `owner` and `detail` fields. Use `WARN` for unknown/ambiguous evidence; do not turn missing evidence into a healthy `PASS` or a fabricated zero.
- No edits to product endpoints, existing views/migrations, recommendation semantics or production data. Production execution remains read-only and operator-only; this task creates and fixture-tests the pack but does not run it against production.

#### Read first

- `AGENTS.md` read-only/analytics evidence rules and `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md` entries NV-N09/N12/N14/N15/N16/N19/N20
- `.ai/runs/2026-10-01-nivelacija-analytics-audit-prompts-evidence.md`
- `scripts/check_supplier_reconciliation_pack.sql`, `scripts/fixtures/supplier-reconciliation-pack-bootstrap.sql`, `Api.Tests/SupplierReconciliationPackTests.cs`, and `.ai/runs/2026-10-01-RQ524-evidence.md`
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`, `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`, and `Database/Migrations/014_NormalizeNivelacijaEvents.sql`

#### Do

1. Add `scripts/sql/nivelacija-reconciliation/*.sql`, each check returning `PASS`/`WARN`/`FAIL` plus counts, following the RQ524 pack conventions:
   - R1: `TipPromene` inventory, `ILIKE '%nivel%'` / `'%povrat%'` values that are not canonical;
   - R2: max numeric `BrojRacuna` length on nivelacija rows;
   - R3: nivelacija events missing from `price_history` (by `source_dnevnik_id`), plus the count of `vw_nivelacija_did` rows with `control_article_id = article_id`;
   - R4: the share of markups and flat changes, same-day multi-events, and events whose next event falls within 30 days;
   - R5: events with NULL `IDObjekat` by `DataOrigin`;
   - R6: view row count = deduplicated event count; `post_qty` = an independent re-sum for mature events; `pre_qty` window length = 30;
   - R7: the column type of `prodaja_zaglavlje.datum_prodaje` and the sales count in the local 22:00–02:00 band;
   - R8: a sample comparing `ps.cena` with `Artikli.ProdajnaCena`, and `NabavnaCena*` × 1.2 vs the price (VAT-basis evidence);
   - R9: `current_user`, `current_schemas(true)`, `to_regclass`, privileges and the column list of `vw_vendor_sales_nivelacija`.
2. Run on Testcontainers fixtures in CI. The production run is read-only and operator-only, and its output goes to `.ai/runs`.

#### Tests

- Fixture-based: each check has one passing and one failing seed.

#### Acceptance

Every **H** in this document has a check whose verdict can confirm or reject it.

#### Dependencies

The RQ524/RQ525 harness.

Claim note 2026-10-01: fresh `origin/main` is `cde8307bb1579222f44a4da171f024127917781e`; Q83, RQ524 and RQ525 are DONE. No active task lock, `rq547` branch or open PR owns this new-files-only feature family. RQ547 moved `WAITING -> READY -> IN_PROGRESS` following the latest audit evidence's recommended first step. Local lock: `.ai/task-locks/RQ547-codex.lock.md`. Scope is limited to new read-only reconciliation SQL and fixture tests; no production database run or write is included.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: Added nine stable read-only checks and fixture-tested clean, adverse, missing-relation, missing-privilege and parity-mismatch states; focused PostgreSQL proof passed 6/6.
- Changed files: nine scripts under `scripts/sql/nivelacija-reconciliation/`, `scripts/fixtures/nivelacija-reconciliation-bootstrap.sql`, `Api.Tests/NivelacijaReconciliationPackTests.cs`, this addendum, the root RQ router, `MASTER_ROADMAP.md` and the RQ547 run log.
- Checks run: focused Testcontainers suite 6/6; agent instruction and planning architecture validators pass; prompt-queue self-test passes; `git diff --check` passes. The full prompt validator reports only P-UI-27's missing `Residual risk:` field.
- Checks not run: production SQL (operator-only per prompt); full backend suite. Planning Governance run 36870353311 is red at `Validate prompt queues` for the same unrelated P-UI-27 field. Analytics Tests & Data Integrity run 36870353240 was in progress at last inspection.
- Run log: `.ai/runs/2026-10-01-RQ547-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `c99c0208aa3c2cb9fbbbc25947aea25cebfe81c5`
- Main verification: passed; fresh `origin/main` was `c99c0208aa3c2cb9fbbbc25947aea25cebfe81c5` and contains the implementation.
- Missed: no production data was sampled; the mapped hypotheses remain unconfirmed until the operator-only run.
- Follow-up: RQ545 (NV-F9) is now IN_PROGRESS after fresh dependency and collision recovery.
- Residual risk: deployed schema, view privileges, event distributions and timestamp basis still need the read-only operator run.
- Prompt defect / scope repair: added the required Evidence, Scope and Read first sections before implementation; kept production execution out of scope.

---

---

## RQ548 - NV-P2 - Independent Pre-Nivelacija oracle and golden fixture

Status: READY
Priority: P1
Type: tests
Feature family: pre-nivelacija-oracle
Parallel-safe: yes
Owner: Analytics Reliability
Findings: NV-N01, NV-N03, NV-N04, NV-N05, NV-N06, NV-N31
Commit suggestion: `test(analytics): add pre-nivelacija oracle and golden fixture`

#### Problem

Pre-Nivelacija has unit tests per helper, but nothing proves end to end that the sales, markdown history, store grain, scores, scenarios, recommendation, queues and summary KPIs come out right on real PostgreSQL. That gap is why the highlight multiplier and the 999 sentinel survived.

#### Do

1. Seed a small dataset with 2 stores × 6 articles: new stock, dead stock, a below-cost item, a chain-wide and a store markdown, returns, and DUG/KOREKCIJA receipts.
2. Write an independent oracle (plain SQL or C# without the endpoint helpers) for `units180`, `daysSinceLastSale`, `markdownEvents`, `avgMarkdownPct`, the margin, the score breakdown and the scenario units.
3. Pin the golden JSON for `/api/analytics/pre-nivelacija-prioriteti` (all stores, one store, `dataScope=imported`).
4. Add cache-failure tests (NV-F4) on the real cache service.

#### Acceptance

The oracle and the endpoint agree on every pinned field. Known defects (NV-F1, NV-F3) are pinned as expected-failure assertions that the fix flips.

#### Dependencies

The RQ525 harness. Land it before NV-F1/NV-F3, or in the same PR.

---

---

## RQ549 - NV-P3 - DiD/control and optional-metric mapper oracle on real PostgreSQL

Status: READY
Priority: P2
Type: tests/sql
Feature family: nivelacija-did-oracle
Parallel-safe: yes
Owner: Analytics Reliability
Findings: NV-N09, NV-N10, NV-N11, NV-N31
Commit suggestion: `test(sql): pin nivelacija DiD, control and mapper semantics`

#### Do

1. Extend the RQ527 fixture with: post-backfill markdown articles; a SKU with two events; controls with and without stock; a markup event; an immature event; two sizes sharing one PLU.
2. Assert, for `vw_nivelacija_kontrolna_grupa` and `vw_nivelacija_did`: the treatment/control sets, no self-match, the window sums, and `did_*`.
3. Assert the C# mappers' outputs per article: DiD, OOS, momentum, elasticity and lost sales.

#### Acceptance

The current defects are pinned (expected to fail) and NV-F6 flips them. The query plan for the DiD view uses a date-bounded index scan, or its cost is recorded.

#### Dependencies

RQ527. This is the proof-first lane for NV-F6/RQ542: pin the current defects and independent expected values before changing the implementation. RQ542 depends on this oracle; do not make RQ549 depend on RQ542.

---

---

## RQ550 - NV-P4 - Nivelacija split oracle and cross-surface parity

Status: READY
Priority: P2
Type: tests
Feature family: nivelacija-split-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier
Findings: NV-N07, NV-N08, NV-N31
Commit suggestion: `test(analytics): prove nivelacija split windows and cross-surface parity`

#### Do

1. Add policy tests with unequal windows (an event at day 5, 15 and 25 of the period, at a flat daily rate), an event before the period start, and a store-scoped vs chain-wide event.
2. Add an endpoint-level parity test: for one article and event, supplier-sales-stats, shoe-type, color, Pre/Post and the scorecard report their effect and cohort in `meta.basis`. Where they are meant to agree, they agree.

#### Acceptance

The flat-rate counterexample fails on current main and passes after NV-F5.

#### Dependencies

RQ528 parity contract. This is the proof-first lane for NV-F5/RQ541: the flat-rate and cross-surface counterexamples must be captured on current main before the split implementation changes. RQ541 depends on this oracle; do not make RQ550 depend on RQ541.

---

## RQ551 - NV-I1 - Pre/Post page: honest volatility, elasticity and DiD presentation

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: prepost-driver-presentation
Parallel-safe: no (`ProdajaPrePostNivelacijePage.tsx`)
Owner: Analytics UX
Findings: NV-N18, NV-N11 (frontend part), NV-N26 (driver copy)
Commit suggestion: `fix(prepost): present volatility, elasticity and DiD with their real basis`

#### Problem

"Volatilnost" compares the post revenue of the current period's events with the post revenue of a different set of events from the previous period. The vendor drill averages elasticity, DiD and lost sales without weights, which differs from the backend KPI and from the type insights. The driver cards show internal view names.

#### Evidence

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx:779-800,843-852,884-886,1236-1239,1898,2024,451-482,1146,1158,2063-2065`.

#### Do

1. Rename "volatilnost" to "promena u odnosu na prethodni period (druga populacija događaja)", or replace it with a within-event dispersion metric from the backend. If the previous request fails, show "Nedostupno"; this already happens.
2. Use the backend-provided aggregated driver values (after NV-F6) instead of averaging on the frontend. Show the median and n, and say whether the value is weighted.
3. Replace raw view names with business copy ("Kontrolna grupa još nije dostupna") and keep the technical reason in a collapsible "Detalji za podršku".
4. Fix the diacritics ("Elastičnost").

#### Tests

- Spec: the volatility label and tooltip; driver values come from the DTO fields; no `vw_` strings in the rendered text.

#### Acceptance

No metric suggests a basis it doesn't have, and no DB object names are visible.

#### Dependencies

NV-F6 for the aggregated drivers; RQ534 (spec statuses) is DONE.

---

---

## RQ552 - NV-I2 - Nivelacija query cost, error status and cache invalidation

Status: WAITING
Registration note: (delta to RQ487; register only if RQ487 does not absorb it)
Priority: P2
Type: backend/frontend/sql/perf
Feature family: nivelacija-runtime-cost
Parallel-safe: no
Owner: Analytics Reliability / Performance
Findings: NV-N24, NV-N25
Commit suggestion: `perf(analytics): bound nivelacija page cost and invalidate after price changes`

#### Problem

**Cost.** One Pre/Post page load issues two endpoint calls (current and previous period). Each runs a count, the main query (45 s timeout) and four optional lookups (5 s each). The DiD view scans `mv_daily_sales_facts` per control candidate with no date predicate.

**Errors.** They return 200 plus error meta. Cancellations are logged as unexpected failures.

**Caching.** Partial-metric responses are cached for 20 minutes, and nothing is invalidated after a price change or a repair.

#### Evidence

- `ProdajaPrePostNivelacijePage.tsx:779-800`.
- `Api/Endpoints/AllEndpoints.cs:51-52,4010,4073,4340,4642,5002-5008,5027-5074`; `016:91-95`.
- `Api/Services/NivelacijaRepairService.cs:761,824`; `AllEndpoints.cs:6611-6650`.

#### Do

1. Measure first: run `EXPLAIN (ANALYZE, BUFFERS)` on fixtures for the main, scoped and DiD queries, and record p50/p95 in `.ai/runs`.
2. Let the backend return the previous-period post revenue per vendor in one call (with an optional `includePrevious` flag), or lazy-load the previous period only when the volatility column is visible.
3. Skip optional lookups when no row needs them, and cache them separately with a short TTL. Don't cache a response whose `MetricsStatus` contains "failed" longer than 2 minutes.
4. Map `OperationCanceledException` to 499 without an error log. Keep the 200-plus-meta contract only for documented "contract missing" states, and use 503 plus a correlation id for unexpected failures, if RQ474's contract allows that.
5. Invalidate the `VendorSalesNivelacija*` and `PreNivelacijaPriorityBase*` cache prefixes after `POST /api/nivelacija` and after a live repair.

#### Acceptance

The p95 is recorded and improved. A price change becomes visible in Pre-Nivelacija on the next request.

#### Dependencies

RQ487 (query cost owner); RQ474 (error contract, DONE on `cb3eb7e`).

---

---

## RQ553 - NV-I3 - Nivelacija copy, labels, i18n and accessibility

Status: WAITING
Priority: P3
Type: frontend/backend-copy/tests
Feature family: nivelacija-copy-a11y
Parallel-safe: yes
Owner: Analytics UX
Findings: NV-N26, NV-N04 (display part)
Commit suggestion: `fix(ui): consistent Serbian copy and labels for nivelacija screens`

#### Do

1. Use one name per screen in `navConfig.ts`, `analyticsRouteDefinitions.ts`, the page titles and the breadcrumbs: "Pre/Posle nivelacije" and "Prioriteti nivelacije". Rename "Nivelacija Repair" to "Popravka nivelacija".
2. Serbian backend alert and queue copy: "ponovljena sniženja", "brzina prodaje", "SKU visokog prioriteta", "nedeljna promena", status "Nedodeljeno". Format numbers with `sr-RS`.
3. Diacritics: "Učitavanje", "Greška" (`NivelacijePage.tsx:94`), "Elastičnost".
4. Accessibility on `/nivelacije` and `/nivelacija`: label the form inputs, give errors `role="alert"`, use `aria-sort` on sortable headers, and add a retry button with the same URL state as the analytics pages (move filters to URL params).

#### Tests

- Snapshot/spec for the labels; a lint-style test that fails on `vw_` or English alert strings in user-facing payloads.

#### Acceptance

Navigation, route and title labels match, and the user-facing copy has no English or technical strings.

#### Dependencies

RQ529 (supplier a11y) for shared components.

---

---

## RQ554 - NV-I4 - Failure-state truth on both nivelacija screens

Status: READY
Priority: P1
Type: frontend/tests
Feature family: nivelacija-failure-state
Parallel-safe: no (`ProdajaPrePostNivelacijePage.tsx`, `PreNivelacijaPriorityPage.tsx`)
Owner: Analytics UX / Reliability
Findings: NV-N32, NV-N33 (L1, L2, L3, L7)
Commit suggestion: `fix(analytics): show real nivelacija error codes, period and supplier identity`

#### Problem

Both dedicated screens are down in production, and neither tells the user or support why.
- Pre/Post maps the backend `vendor_sales_nivelacija_contract_missing` message to a generic error and drops the code and correlation id. Asortiman shows the real message for the same endpoint.
- Prioriteti does not show which backend code it got, and on error the header says "Period nije definisan" although the model always uses a fixed 180-day UTC window.
- The supplier dropdown shows duplicate names that can't be told apart, and the URL carries an opaque id.

#### Evidence

- `ProdajaPrePostNivelacijePage.tsx:529-533,824-829,705-706,1454-1458`; `services/analyticsHttp.ts:48-56`; `services/preNivelacijaApi.ts:111-115`; `components/analytics/AnalyticsTrustHeader.tsx:259`; `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:952-957`.
- Live screenshots `01-prepost-80-error`, `02-prioriteti-80-error`, `03-supplier-asortiman-80` (parallel audit report).

#### Do

1. Carry `errorCode` and `correlationId` from analytics meta errors through `fetchAnalyticsJson` and `preNivelacijaApi` (a typed error). Render them in `AnalyticsErrorState` on both pages.
2. Map known codes to business copy: contract missing → "Pre/post analiza čeka ispravku šeme baze (podrška: kod, ID)"; `pre_nivelacija_sales_unavailable` → "Prodaja za skor trenutno nije dostupna".
3. Prioriteti: always show "Prozor: poslednjih 180 dana (UTC)" from a constant shared with the backend contract, even on error. Show `noSaleDaysMin` as a filter chip, not as a period.
4. Supplier select: show "naziv (šifra)" when names collide and sort with `sr` collation. Keep `vendorId` in the URL, but render the selected name in the filter summary chip.
5. Keep retry; the button already exists on both pages.

#### Tests

- Spec: a contract-missing payload renders the real message, code and correlation id; the Prioriteti error state shows the 180-day window; two suppliers with the same name render distinguishable labels.

#### Acceptance

From the error screen alone, support can tell which backend state caused the failure.

#### Dependencies

NV-F9 (backend states). RQ523/RQ529 own the Asortiman retry and supplier date formatting.

---

---

## RQ555 - NV-I5 - One markdown candidate source across Products, Actions and Prioriteti, with URL state

Status: WAITING
Registration note: (owner decision on which model is canonical)
Priority: P2
Type: backend/frontend/product
Feature family: markdown-candidate-source
Parallel-safe: no (`CachedAnalyticsEndpoints.cs` action generation, `AnalyticsActionsPage.tsx`)
Owner: Analytics Product
Findings: NV-N34 (L5, L6)
Commit suggestion: `feat(analytics): align markdown action source with pre-nivelacija candidates`

#### Problem

"Nivelacija" actions are generated only from Product Decision rows with `RecommendationStatus = MARKDOWN` (at least 2 rows, confidence ≥ 55), but they deep-link to Prioriteti, which ranks candidates with a different model. Product Decision currently returns 0 allowed MARKDOWN rows (1,078 rows without cost, everything gated), so the Actions source is always 0, whatever Prioriteti would rank. The source filter is read from the URL but never written to it.

#### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:4447-4460,7537,7599,7269`.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx:594,697-702`.

#### Do

1. The owner decides which model is canonical for "treba sniziti sada". Proposal: Pre-Nivelacija after NV-F1/NV-F3 and NV-E1, because it is store-grain and markdown-specific.
2. Generate `nivelacija` actions from the canonical candidates (allowed recommendations only), with the same evidence gates. Show "0 jer nema dozvoljenih kandidata (razlog)" instead of a silent 0.
3. Make the Products MARKDOWN label and the Prioriteti status reference each other, or explain the difference in the KPI help.
4. Write `sourceType`, `priority` and `dataQualityStatus` to the URL on change, using `replace` for filter edits, so back and share work.

#### Tests

- Backend: a fixture with Prioriteti-allowed candidates and no Product MARKDOWN rows still yields `nivelacija` actions under the chosen policy.
- Frontend: changing the source updates `location.search`.

#### Acceptance

The Actions count for "Nivelacija" is explainable from Prioriteti, and the filter state can be shared by URL.

#### Dependencies

NV-F1, NV-F3; the products owners (RQ475/RQ487) for the gate context.

---

## RQ556 - NV-E1 - Rebuild the Pre-Nivelacija priority signal around weeks of cover, age and season end

Status: WAITING
Registration note: (owner-gated weights)
Priority: P2
Type: backend/frontend/product
Feature family: pre-nivelacija-signal-v9
Parallel-safe: no (`PreNivelacijaScoringService.cs`)
Owner: Analytics Product / Pricing (owner approval required)
Findings: NV-N21, NV-N22
Commit suggestion: `feat(analytics): pre-nivelacija v9 signal with weeks of cover and season end`

#### Problem

The score uses absolute stock and velocity normalized to the cohort maximum, plus a symmetric in-season boost. Footwear markdown timing depends on weeks of cover against the weeks left in the season, on the article's age and on sell-through since receipt.

#### Do

1. Add the evidence fields `weeksOfCover = stock / max(velocity7d·7, ε)`, `weeksToSeasonEnd`, `ageDays` (from NV-F3), `sellThroughSinceReceipt = sold / (sold + stock)` and `coverGap = weeksOfCover − weeksToSeasonEnd`.
2. Propose a v9 formula that uses percentile or absolute thresholds instead of the max-normalization. Ship it behind a flag, run it in parallel with v8 and publish the rank-correlation diff. The owner approves the weights.
3. In the UI show "Pokrivenost: 14 ned. / do kraja sezone: 6 ned." per row.

#### Acceptance

The weights are approved by the owner. v8 and v9 can be compared on the same snapshot, and every score input is visible per row.

#### Dependencies

NV-F1, NV-F3, NV-P2.

---

---

## RQ557 - NV-E2 - Markdown outcome ledger: did it work, what it cost, what to repeat or avoid

Status: WAITING
Priority: P2
Type: sql/backend/frontend
Feature family: markdown-outcome-ledger
Parallel-safe: no (new view/DTO; Pre/Post page section)
Owner: Analytics Product / Pricing
Findings: NV-N23, NV-N28, NV-N29
Commit suggestion: `feat(analytics): add markdown outcome ledger with margin cost and sell-through`

#### Problem

For a past markdown, no screen answers four questions: did sell-through improve, what the markdown cost in margin, whether the stock cleared before the season ended, and which depth bands work for this supplier or footwear type.

#### Do

1. Add a per-event outcome over mature markdowns (`price_direction='markdown' AND post_window_complete`, from NV-F7) with these fields:
   - `stock_at_event` (dated stock if it exists; otherwise derived from receipts minus sales; otherwise unavailable);
   - `sell_through_pre30` and `sell_through_post30` against the stock at the event;
   - `markdown_cost = Σ(old − new) × post units`;
   - the margin before and after, using cost at sale time (the RQ521/RQ522 cost basis);
   - `days_to_clear` or the remaining stock at 30/60 days;
   - `depth_band` (≤10/10–20/20–30/>30%).
2. Aggregate by supplier × footwear type × depth band: the success rate (sell-through uplift above a threshold with margin per unit not worse than the alternative) and the median markdown cost. Label it "ponoviti" or "izbegavati" only above a minimum event count.
3. Feed the history-derived elasticity per type and depth band back into the Pre-Nivelacija markdown scenario, replacing the fixed 1.8 with an explicit fallback.
4. Add a "Ishod sniženja" section on Pre/Post that lists events by outcome, with export.

#### Acceptance

Every number states its population, maturity and cost basis. There are no causal claims; it is labelled "ishod", not "efekat".

#### Dependencies

NV-F6, NV-F7, NV-P1 (stock source evidence).

---

---

## RQ558 - NV-E3 - Controlled markdown effect with seasonality and cannibalization (delta to RQ532)

Status: WAITING
Priority: P3
Type: sql/backend/product
Feature family: markdown-controlled-effect
Parallel-safe: no
Owner: Analytics Product
Findings: NV-N28, NV-N09
Commit suggestion: `feat(analytics): controlled markdown effect with seasonal baseline and cannibalization check`

#### Scope note

RQ532 owns the supplier size-curve and the approved DiD/control contract. This prompt only adds what RQ532 doesn't specify.

#### Do

1. Seasonal baseline: use the same calendar weeks from the previous year for the same footwear type and category, and compare the uplift with the seasonal expectation.
2. Matched controls: several controls per event (k nearest by pre revenue, price band and age, within type and category), excluding recently treated articles (NV-F6). Report a confidence band and n.
3. Cannibalization: the change in full-price sales of same-category, same-price-band, non-marked-down articles in the same stores during the post window. Report it as a separate number, never netted silently.
4. Gate it: show the controlled effect only when n controls ≥ the threshold and the window is mature.

#### Acceptance

The controlled effect, the seasonal expectation and cannibalization are shown separately with their n, and raw pre/post is never labelled causal.

#### Dependencies

RQ532, NV-F6, NV-E2.

---

---

## RQ559 - NV-E4 - Size-run brokenness as a markdown-priority input (footwear)

Status: WAITING
Priority: P3
Type: backend/frontend
Feature family: pre-nivelacija-size-run
Parallel-safe: no (`PreNivelacijaPriorityEndpoints.cs`)
Owner: Analytics Product / Pricing
Findings: NV-N30
Commit suggestion: `feat(analytics): flag broken size runs in pre-nivelacija`

#### Scope note

RQ532 proves size availability and the supplier-level size-curve. This prompt consumes that proof at SKU/model level.

#### Do

1. Group article rows into models by the model key RQ532 establishes (do not assume PLU). Compute the remaining sizes vs the original size run, the core sizes missing, and the share of stock in edge sizes.
2. Add `sizeRunStatus` (`full`/`broken`/`edge_only`) and a reason code to the Pre-Nivelacija candidates and queues. When the owner approves, apply it as a score input; until then, as a filter or badge.
3. Add a UI filter "pokidan broj" and per-model drill-down.

#### Acceptance

Broken-run models are identifiable and filterable. The size data source is proven, or else the field is unavailable with a reason.

#### Dependencies

RQ532 (source proof), NV-E1.

---

## Pokrivenost nalaz → prompt

| Nalaz | Prompt | Hipoteza? |
|---|---|---|
| NV-N01 | NV-F1, NV-P2 | ne |
| NV-N02 | NV-F2 | ne |
| NV-N03 | NV-F2, NV-P2 | ne |
| NV-N04 | NV-F3, NV-I3, NV-P2 | ne |
| NV-N05 | NV-F3, NV-P2 | ne |
| NV-N06 | NV-F4, NV-P2 | ne |
| NV-N07 | NV-F5, NV-P4 | ne |
| NV-N08 | NV-F5, NV-P4 | ne |
| NV-N09 | NV-F6, NV-P1 (R3), NV-P3, NV-E3 | obim: da |
| NV-N10 | NV-F6, NV-P3 | ne |
| NV-N11 | NV-F6, NV-I1, NV-P3 | ne |
| NV-N12 | NV-F7, NV-P1 (R4) | obim: da |
| NV-N13 | NV-F7 | ne |
| NV-N14 | NV-F8, NV-P1 (R1) | da |
| NV-N15 | NV-F8, NV-P1 (R2) | da |
| NV-N16 | NV-F9, NV-P1 (R9) | da |
| NV-N17 | NV-F2 | ne |
| NV-N18 | NV-I1 | ne |
| NV-N19 | NV-P1 (R8), NV-E2 | da |
| NV-N20 | NV-P1 (R7) | da |
| NV-N21 | NV-E1 | ne (model/policy) |
| NV-N22 | NV-E1 | ne (model/policy) |
| NV-N23 | NV-E2 | ne |
| NV-N24 | NV-I2 (delta RQ487) | cena: meriti |
| NV-N25 | NV-I2 | ne |
| NV-N26 | NV-I3, NV-I1 | ne |
| NV-N27 | NV-F7 | ne |
| NV-N28 | NV-E2, NV-E3 | ne |
| NV-N29 | NV-E2 | ne |
| NV-N30 | NV-E4 (delta RQ532) | izvor veličina: RQ532 |
| NV-N31 | NV-P2, NV-P3, NV-P4 | ne |
| NV-N32 | NV-I4 | ne |
| NV-N33 | NV-I4 | duplikat u podacima: da |
| NV-N34 | NV-I5 | ne |
| NV-N35 | NV-F10 | live okidač: da |
| L1 Pre/Post nedostupan (contract missing, generička poruka) | NV-F9, NV-I4 | zašto kolona fali: da |
| L2 Prioriteti nedostupni (backend error meta, <1 s) | NV-F4, NV-P1, NV-I4 | koji kod: da |
| L3 „Period nije definisan“ | NV-I4 | ne |
| L4 Asortiman contract alert bez retry-ja | NV-F9; retry delta RQ523/RQ529 | ne |
| L5 Products MARKDOWN = 0, 1.078 bez troška | NV-I5; delta products owners (RQ475/RQ487) | obim: podaci |
| L6 Akcije „Nivelacija“ = 0, izvor nije u URL-u | NV-I5 | ne |
| L7 duplikat „BIS“, neproziran `vendorId` | NV-I4 | podaci: da |
| L8 ISO/srpski datumi, mešani engleski | NV-I3; delta RQ529 | ne |
| L9 503 `supplier-sales-stats` (Render) | delta RQ487, RQ454/STAB16 (RQ474 DONE) | uzrok: da |
| L10 404 hash chunk-ovi posle deploy-a | NV-F10 | ne |
| L11 „reading 'default'“ → error boundary | NV-F10 | live okidač: da |
| L12 nema kontrole/sell-through/margin cost | NV-E2, NV-E3 | ne |

## Lista promptova

| ID | Sekcija | Prioritet | Naslov |
|---|---|---:|---|
| NV-F1 | FIX | P1 | Fix the Pre-Nivelacija highlight scenario multiplier and pin scenario semantics |
| NV-F2 | FIX | P1 | Attribute, validate and consistently scope application-created nivelacija events |
| NV-F3 | FIX | P1 | Stop ranking new arrivals and below-cost items as markdown priorities |
| NV-F4 | FIX | P1 | Never cache transient Pre-Nivelacija failures or client cancellations |
| NV-F5 | FIX | P1 | Replace the unequal-window first-nivelacija split in Supplier, Footwear-type and Color stats |
| NV-F6 | FIX | P2 | Make DiD/control, OOS, momentum and elasticity event-aligned and honest |
| NV-F7 | FIX | P2 | Declare event semantics in the canonical nivelacija views (direction, overlap, maturity) |
| NV-F8 | FIX | P2 | Harden the 014 event normalization and integer casts |
| NV-F9 | FIX | P1 | Diagnose the live Pre/Post `contract_missing` (schema/privilege mismatch hypothesis) |
| NV-F10 | FIX | P1 | Do not swallow failed lazy-route imports in chunk-load recovery |
| NV-P1 | PROVE | P1 | Read-only nivelacija reconciliation SQL pack |
| NV-P2 | PROVE | P1 | Independent Pre-Nivelacija oracle and golden fixture |
| NV-P3 | PROVE | P2 | DiD/control and optional-metric mapper oracle on real PostgreSQL |
| NV-P4 | PROVE | P2 | Nivelacija split oracle and cross-surface parity |
| NV-I1 | IMPROVE | P2 | Pre/Post page: honest volatility, elasticity and DiD presentation |
| NV-I2 | IMPROVE | P2 | Nivelacija query cost, error status and cache invalidation |
| NV-I3 | IMPROVE | P3 | Nivelacija copy, labels, i18n and accessibility |
| NV-I4 | IMPROVE | P1 | Failure-state truth on both nivelacija screens |
| NV-I5 | IMPROVE | P2 | One markdown candidate source across Products, Actions and Prioriteti, with URL state |
| NV-E1 | ENHANCE | P2 | Rebuild the Pre-Nivelacija priority signal around weeks of cover, age and season end |
| NV-E2 | ENHANCE | P2 | Markdown outcome ledger: did it work, what it cost, what to repeat or avoid |
| NV-E3 | ENHANCE | P3 | Controlled markdown effect with seasonality and cannibalization (delta to RQ532) |
| NV-E4 | ENHANCE | P3 | Size-run brokenness as a markdown-priority input (footwear) |

Predlog redosleda: NV-P1 + NV-F9 (dijagnostika live ugovora) + NV-I4 (vidljiv uzrok greške), NV-F10 (pad aplikacije posle deploy-a), zatim NV-F4, NV-P2 → NV-F1/NV-F3, NV-F2, NV-F5 + NV-P4; posle toga NV-F7 → NV-F6 + NV-P3, NV-I2, NV-I1, NV-E2, NV-E1, NV-I5; na kraju NV-F8 (prema R1/R2), NV-I3, NV-E3, NV-E4.

## Šta nije urađeno / nije provereno

- Live UI nalazi (L1–L12) su preuzeti iz paralelnog audita i mapirani na kod; sam nisam pokretao browser niti API pozive. Tačan backend kod za Prioritete (L2) i razlog nedostajuće kolone (L1) nisu utvrđeni (NV-P1 R9, NV-I4).
- Recharts interop nije posebno proveren; mehanizam L11 je izveden iz koda i Vite 7 semantike `vite:preloadError`, a nije reprodukovan.
- Nije rađen live SQL/API/browser ni pristup bazi. Hipoteze NV-N09 (obim), N12 (obim), N14, N15, N16, N19 i N20 traže NV-P1 proveru.
- Nijedan test ni build nije pokrenut; promena je samo dokumentacija.
- Nije detaljno pročitano: `NivelacijaRepairService.cs` (osim upisa i audita; samo potvrda da nema invalidacije keša), `NivelacijaRepairPage.tsx`, `prePostNivelacijaTrust.ts`, `preNivelacijaDecision.ts`, 018 `vw_supplier_fullprice_signals*` definicija `stock_before_markdown`, `CachedAnalyticsEndpoints.cs` products page osim linkova ka Prioritetima (`:4456-4457,4748,5030`). Products page ne računa nivelacija metriku sam; koristi linkove i supplier split.
- Namera množioca isticanja (NV-N01) nije potvrđena iz git istorije; NV-F1 to traži kao prvi korak.
- Queue fajlovi i `MASTER_ROADMAP.md` nisu menjani; registracija je follow-up.
