# Supplier / Shoe Type accuracy contract

Contract version: `SST-ACCURACY-1.0`
Contract owner: Analytics Reliability / Supplier + Shoe Type
Effective date: 2026-09-26
Certification task: `RQ445`
Contract evidence id: `RQ445-SST-ACCURACY-1.0-2026-09-26`

This is the canonical semantic contract for a bounded Supplier Sales or Shoe Type
Sales accuracy claim. It is not evidence that the current deployment is already
certified. Runtime, browser, production and customer proof remain separate queue
gates.

## 1. Claim boundary

The only permitted accuracy claim is scoped to all of the following:

- tenant/deployment and authorized store set;
- requested and effective UTC period;
- `storeId` (or all stores);
- normalized `dataScope`;
- application commit, schema/migration state and contract version;
- the source population and evidence id used by the proof.

Preferred customer wording:

> For the certified dataset, period, store and application build, Supplier and
> Shoe Type sales totals and buckets were independently reconciled with the
> source sale lines with zero unexplained deltas; unknown, estimated and
> cost-limited portions are shown explicitly.

The wording must not be shortened to an unconditional “100% accurate” claim.
Incomplete cost, unknown attribution, skipped tests, stale evidence, missing
browser/export proof or a runtime integrity state other than `verified` makes
the result `UNVERIFIED`, `DEGRADED`, `DRIFT_DETECTED` or `NOT_RUN`.

## 2. Canonical sales population

The source population is sale headers joined to sale lines. A line is included
only when its header satisfies the requested period, store and data-scope
predicates. The same population must feed the API totals, rows, chart, detail,
export/report and independent oracle for a certification run.

### Period and store

- Date-only filters are normalized to UTC calendar boundaries.
- The effective interval is half-open: `[fromUtc, toUtc)`; the exclusive end is
  the next UTC midnight for an inclusive calendar-date selection.
- A row at `fromUtc` is included; a row at `toUtc` is excluded.
- `storeId = null` means the authorized all-store population. A supplied store
  filter is applied to the sale header, not inferred from the article.
- Requested and effective period/store values must be present in provenance
  metadata. A fallback or unsupported filter is never silently presented as an
  exact match.

### Receipt/document rule

`BrojRacuna`, after trim and case-insensitive comparison, equal to `DUG` or
`KOREKCIJA` is a non-standard debt/adjustment document and is excluded from
certified retail turnover on Daily Sales, Supplier, Shoe Type and Color. It may
be reported separately as an auditable excluded population. Signed retail
returns remain included; a negative quantity or revenue is not silently removed.
The cross-surface implementation and oracle parity are owned by `RQ456`.

### Data scope

The normalized scope is one of `all`, `imported` or `existing`; an invalid or
missing value normalizes to `all` only when that fallback is reported as the
effective scope. For the current Operations contract:

- `all` is the union of the eligible rows;
- `imported` selects the Access-origin rows (`DataOrigin = access`);
- `existing` selects `DataOrigin = existing`, null or blank legacy-origin rows.

The scope predicate is applied before aggregation and pagination. The exact
source-column mapping and receipt predicate must be identical across the API,
oracle and all surfaces; a row count from a truncated page is never the total
population. If `RQ456` changes the source-of-truth mapping, it must version this
contract and update the certification fixture rather than silently changing
the meaning of `existing` or `imported`.

## 3. Dimension identity and attribution

### Supplier

- Grouping key: immutable `supplier_id_at_sale` from the sale line.
- `null` is the single unknown-supplier bucket.
- A label is presentation only; it cannot change bucket identity.
- Frozen, reconstructed and unknown attribution bases remain distinct in
  metadata. Current catalog master values must not reclassify historical lines.

### Shoe Type

- Grouping key: immutable `shoe_type_id_at_sale` from the sale line.
- `null` is the single unknown-type bucket.
- A non-null ID remains a known identity even when its label is blank or exactly
  `Nepoznato`; that is a label-quality issue, not an unknown bucket.
- Previous-period-only IDs are retained in a PoP row with current signed units
  and revenue equal to zero. Current-period cost, margin or recommendation
  values are not invented for that row.

Unknown rows are part of the certified aggregate. They are shown separately so
that identity coverage is measurable; they are not silently dropped from totals.

## 4. Metric formulas and denominator rules

All formulas use the same filtered source population and signed values.

| Metric | Canonical definition | Missing/invalid behavior |
|---|---|---|
| Signed quantity | `sum(line.quantity)` | preserve sign; no absolute-value conversion |
| Revenue | `sum(line.quantity * line.unitSalePrice)` in RSD | preserve returns/negative rows |
| Row revenue share | `rowRevenue / totalPopulationRevenue * 100` | unavailable when the declared denominator is unavailable; never replace with zero |
| Row quantity share | `rowQuantity / totalPopulationQuantity * 100` | unavailable when the declared denominator is unavailable |
| Covered revenue | sum of revenue for lines with qualified cost evidence | uncovered revenue is reported separately |
| Margin contribution | `sum(signedRevenue - qualifiedCost)` for cost-covered lines | incomplete coverage is visible in coverage metadata |
| Weighted average margin | `sum(marginContribution) / sum(costCoveredRevenue)` over the full current response population | unavailable when covered-revenue denominator is not positive |

The headline `Prosečna marža` is the full-population weighted aggregate,
including the unknown dimension bucket. A known-identity recommendation
benchmark is a separate metric with a separate denominator and provenance; it
must not be relabelled as the headline margin.

### Supplier shares and concentration

The normal Supplier table share uses the full eligible response population,
including the unknown bucket, not only named suppliers. A known-only view is a
different population and must say so in its label and metadata.

The `Udeo top 5` KPI and concentration chart must not use a hidden known-only
denominator or a page-local denominator. Until the dedicated concentration
owner (`RQ431`) finalizes its aggregate contract, the safe rule is:

- use the same full eligible supplier-row population as the table;
- keep unknown as an explicit row in the denominator;
- preserve signed net revenue for a supplier with returns or other negative
  revenue; do not use `abs`, clamp to zero or move it to “Ostali”;
- never show a mathematically misleading concentration percentage when the
  declared denominator is zero/non-positive; render unavailable/N/A with the
  reason and keep absolute RSD totals available;
- if a later concentration contract intentionally ranks positive contribution,
  it must name that population and denominator instead of reusing the label
  “Udeo u prometu”.

This keeps the table, chart and KPI semantically aligned without pre-empting
RQ431's remaining concentration decision.

### Margin-contribution share

Margin-contribution share is available only when the aggregate margin
contribution denominator is strictly positive. For a zero or negative total,
the share is `N/A`; the truthful fallback is absolute RSD contribution with
the denominator reason. A negative contribution is not converted to a positive
share and `0 / 0` is not rendered as `0%`.

## 5. Previous-period and PoP contract

For a selected supplier, known-only view or other subset, the previous-period
total must be computed by the backend over the same population definition and
the same filters, using the corresponding previous interval. The current page
must not infer that baseline by summing visible current rows.

- The unfiltered Supplier trend uses the backend full-population previous total.
- A known-only or selected-supplier trend needs a backend-scoped previous total
  for that exact population.
- `N/A` is acceptable only when the backend explicitly reports that the
  comparable baseline is unavailable, the previous population is empty, or the
  required scope cannot be evaluated. It is not acceptable as a permanent
  substitute for a baseline that the backend can compute.
- A previous-period-only identity is included in the union population and may
  show truthful `-100%` PoP when its previous denominator is positive.
- No-baseline and zero-baseline cases are distinct from a measured zero change.

## 6. Cost and evidence qualification

Cost evidence is qualified per line or aggregate according to the existing
margin policy. The contract must expose at least:

- covered revenue and margin-contribution totals;
- coverage percentage/counts;
- cost source/provenance (historical, snapshot, fallback or missing);
- attribution basis and identity coverage;
- requested/effective period, store and scope;
- freshness and Operations integrity state;
- evidence id, contract version, build and schema/migration identity.

Missing or estimated evidence remains visibly qualified. It cannot produce a
green `verified` state, a fake zero margin or an unqualified accuracy claim.

## 7. Surface parity

The API is the decision source of truth. The following surfaces must preserve
the same definitions or display an explicit population/denominator conversion:

1. KPI cards and trust header;
2. table rows and aggregate totals;
3. charts and concentration labels;
4. row detail and previous-period detail;
5. CSV/PDF/report export;
6. certification artifact and customer wording.

Frontend code may format and explain backend values, but must not recreate a
different recommendation, share, cost qualification or confidence rule.

## 8. Certification status

`VERIFIED` is allowed only when the same contract version, source population,
period/store/scope, fixture/build/schema and evidence id prove:

1. independent raw-fact oracle and API agree with zero unexplained deltas;
2. table, KPI, chart, detail and export reconcile to the API/oracle;
3. attribution, unknown identity and cost coverage are explicit;
4. required tests actually executed, with skipped/unavailable cases accounted;
5. integrity evidence is durable and traceable;
6. production/customer wording is used only when its separate access and
   acceptance gates are complete.

Otherwise the result is `UNVERIFIED`, `DEGRADED`, `DRIFT_DETECTED` or
`NOT_RUN`, with a reason. No status may be inferred from a successful HTTP
response alone.

## 9. Required certification artifact metadata

Every certification artifact must name:

- `contractVersion: SST-ACCURACY-1.0`;
- `evidenceId` and source fixture/population;
- requested/effective period, store and data scope;
- application commit and schema/migration identity;
- formula/denominator policy used;
- executed versus skipped test counts;
- unknown/attribution and cost coverage;
- integrity status and freshness;
- affected surfaces and any residual limitation.

The RQ412 raw-fact manifest remains the arithmetic oracle input, while RQ456,
RQ457, RQ446-RQ455 own the subsequent population, identity, execution,
reconciliation, evidence-history, UI, certificate, CI, production and
acceptance layers. This document does not reopen those owners.
