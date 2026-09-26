# Supplier / Shoe Type semantic closure evidence

Date: 2026-09-25
Repository: ivanjovicic/Trendplus
Delivery target: main
Scope: DUG/KOREKCIJA population + Shoe Type margin share / identity / previous-only PoP / headline margin semantics

## Findings

### 1. DUG/KOREKCIJA population mismatch — confirmed

`Api/Services/DailySalesStatsService.cs` explicitly excludes receipt numbers `DUG` and `KOREKCIJA` using trimmed/case-insensitive matching and reports them as excluded non-standard debt/correction documents.

The Supplier, Shoe Type and Color sales endpoint queries in `Api/Endpoints/AllEndpoints.cs` currently join `ProdajaStavke -> ProdajaZaglavlja -> Artikli` without the same `BrojRacuna` exclusion in their current/previous population queries. This permits cross-screen turnover disagreement for the same period/store/scope.

Decision: certified retail turnover excludes DUG/KOREKCIJA everywhere. Signed retail returns remain included. New owner: RQ456.

### 2. Non-positive margin contribution denominator — confirmed and narrow frontend fix delivered

`Klijent/clientapp/src/utils/shoeTypeMarginComparison.ts` previously returned a percentage for negative total margin contribution and returned 0% for 0/0 in detail formatting, while the chart already correctly switched to absolute RSD mode for non-positive totals.

Decision: margin-contribution share is meaningful only when the total margin contribution is strictly positive. For zero/negative total, share is unavailable/N/A and absolute RSD contribution may be shown.

Delivered:
- `e9a98603794819fc590fefc6cd0c49b4f3c8adc5` — helper fails closed for `totalMarginContribution <= 0`.
- `57119a8a39e5fbb4167de4d274e51b5143533aea` — focused regression expectations for negative and 0/0 denominators.

Broader backend/detail/export parity remains under RQ457.

### 3. Shoe Type unknown identity by display name — confirmed

The canonical grouping already uses frozen `ShoeTypeIdAtSale`, but several paths infer unknown from the display label `Nepoznato`:
- Shoe Type endpoint unknown revenue / known-margin cohort / recommendation identity.
- `AnalyticsDetailReadService` unknown detail selection, known-margin evidence and unknown-revenue calculation.
- Some integration/snapshot helper expectations.

This can misclassify a legitimate non-null Shoe Type whose name is blank or literally `Nepoznato`.

Decision: only `tipObuceId == null` defines the unknown bucket. A non-null ID is known regardless of its label. New owner: RQ457.

### 4. Previous-period-only Shoe Types are absent from response rows — confirmed

The endpoint builds `previousShoeTypeMetrics` from the previous period, but constructs `shoeTypes` by grouping only current-period `stavke`. A type with previous sales and zero current sales contributes to the previous total but receives no row.

Decision: row-level PoP uses the union of current and previous IDs. Previous-only type: current revenue/units = 0, previous values retained, -100% when previous denominator is positive; current margin/cost/recommendation evidence must not be invented. Owner: RQ457.

### 5. Headline average margin denominator — resolved

The screen already consumes backend `totals.prosecnaMarza`. Current backend weighting is covered-revenue-weighted but excludes Shoe Types using the display text `Nepoznato`.

Decision:
- Headline `Prosečna marža` = full current response covered-revenue-weighted margin:
  `sum(marginContribution) / sum(costCoveredRevenue) * 100`.
- A recommendation benchmark may intentionally exclude only `tipObuceId == null`, but it must be a separate known-identity metric/provenance field and must not masquerade as the headline KPI.
Owner: RQ457.

## Planning changes

- Updated `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md`:
  - RQ445 contract now records the semantic decisions.
  - RQ446 adversarial fixture must include DUG/KOREKCIJA, previous-only Shoe Type, non-null ID with `Nepoznato`/blank labels, and zero/negative margin denominator.
  - RQ447 certification execution now waits for RQ456/RQ457.
  - Added RQ456 retail receipt population owner.
  - Added RQ457 Shoe Type identity/PoP/margin owner.
- Updated `docs/qa/SUPPLIER_SHOETYPE_ACCURACY_CERTIFICATION_PLAN_2026-09-25.md`.
- Updated `MASTER_ROADMAP.md` with the new blocking relationship.
- Existing RQ447/RQ449 IDs were not repurposed.

Planning commits:
- `30adb54c13c81757f656493e9946c773982d6112`
- `7eb1995e2f3368cbccf1e31a06db79ff97a9fa34`
- `7eb66c5247ef510410997712ae08daa2a4e72e3f`

## Validation

Repository source inspection and deterministic text-level change review were performed through the GitHub connector.

Not run in this chat:
- frontend Vitest/runtime suite;
- backend build/tests;
- PostgreSQL integration tests;
- queue/planning node validators.

Those executions remain required before the immediate helper change or RQ456/RQ457 implementation is promoted as certification evidence. A not-run check must never be recorded as pass.
