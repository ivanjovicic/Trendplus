# Shoe Type + Daily Sales ("Prodaja po smeni") correctness audit — 2026-09-28

Repo: `ivanjovicic/Trendplus`

Surfaces:
- `/analytics/shoe-type-sales-stats`
- `/analytics/daily-sales`

## Evidence boundary

The browser/web helper available in this session cannot open the Vercel/Render deployment, so this audit does not claim a new production screenshot or fresh production numeric reconciliation. Current-main source, focused tests, same-day repository evidence and the existing RQ407/RQ412/RQ445-RQ457 certification artifacts were inspected. Fixture values are treated as regression evidence, not as current production totals.

## What is already correctly owned / should not be re-queued

The audit explicitly reuses these delivered contracts instead of duplicating them:

- RQ442 — half-open whole-day period boundaries for Supplier/Shoe Type/Color.
- RQ445 — `SST-ACCURACY-1.0` contract.
- RQ446 — adversarial Supplier/Shoe Type golden fixture.
- RQ456 — one trimmed/case-insensitive DUG/KOREKCIJA exclusion policy; signed retail returns remain included.
- RQ457 — Shoe Type sale-time ID identity, previous-only union and weighted margin semantics.
- RQ382 — Daily diagnostics/availability scope metadata.
- RQ383 — measured shift rows vs off-shift/no-time handling.
- RQ384 — safe Daily error/correlation contract.
- RQ429 — Daily no-data rendering, shift-quality counts and MA7 baseline.
- RQ430 — Daily sorting/print/store export metadata.
- RQ431 — signed supplier concentration fail-closed contract.
- RQ441 — Daily sale-time supplier attribution.
- RQ325/RQ318/RQ329 and related UI prompts — localization, URL state and dead truncation copy.

Those fixes remain valuable and are not reopened by this audit.

## Executive finding

The two screens are materially stronger than they were before the September reliability work, but four residual contracts still prevent a strong unconditional correctness claim:

1. certified sales `dataScope` source-of-truth is contradictory between contract, implementation and oracle;
2. Shoe Type/Supplier snapshot cost is generated at sale-line grain but consumed at article-minimum grain;
3. Shoe Type signed share can affect backend recommendations while the frontend hides the same value as invalid; old rank/negative-baseline presentation residuals also remain;
4. Daily shift assignment uses the raw stored `DatumProdaje.Hour` without an explicit business-time/timestamp-basis contract.

## F1 — P0: formal dataScope contract says sale-header origin, both screens use current article origin

The canonical `SST-ACCURACY-1.0` contract says that a sale line is in the certified sales population only when its **sale header** satisfies the requested period, store and data-scope predicates. The older data-scope audit says the same for sales revenue: `ProdajaZaglavlja.DataOrigin` is the sales-source rule, while `Artikli.DataOrigin` belongs to article-quality/inventory semantics.

Current code disagrees:

- Shoe Type current and previous queries join `Artikli` and filter `imported/existing` through `a.DataOrigin`.
- Daily Sales does the same in the main aggregate, receipt diagnostics and excluded-document queries.
- the RQ412 independent Supplier/Shoe Type oracle and its manifest also declare article `DataOrigin` as the data-scope source.

Therefore the oracle can agree perfectly with the endpoint while both disagree with the formal contract. A fixture where article and sale-header origins happen to match cannot detect this.

### User impact

Changing an article's current master origin can move historical sales between “Uvezeni” and “Postojeći”, even though the sale record's own provenance did not change. The same selected scope can also mean something different from Dashboard/PDC sales metrics that already use sale-header origin.

### Decision

For **sales revenue/quantity populations**, use `ProdajaZaglavlja.DataOrigin`. Current article origin may remain a separately named master-data membership/quality dimension only when a feature explicitly needs it. Do not silently combine both under one `dataScope` label.

Owner: **RQ494**.

## F2 — P0/P1: the independent oracle currently certifies the same wrong scope assumption

`SupplierShoeTypeRawFactOracle` and `SUPPLIER_SHOETYPE_INDEPENDENT_ORACLE_MANIFEST_2026-09-25.md` use article origin. RQ445's later formal contract says header origin. That means scoped certification is internally contradictory.

The repair needs an adversarial case where:
- header = `access`, article = `existing`;
- header = `existing`, article = `access`.

The expected sales scope must follow the header, while any explicitly article-scoped quality metric must say so separately.

Owner: **RQ494**. RQ448/browser certification must not claim scoped VERIFIED evidence until this contradiction is closed.

## F3 — P0/P1: snapshot cost is line-level in storage but collapsed to article-level minimum in analytics reads

The snapshot design and table are line-level:
- `AnalyticsSaleLineCostSnapshot.ProdajaStavkaId` identifies the sale line;
- snapshot generation selects only Access-origin sale headers with missing sale-line cost;
- one snapshot row is persisted per eligible sale line.

But Supplier/Shoe Type list and `AnalyticsDetailReadService` load the active batch by:
`GroupBy(ArtikalId) -> Min(ResolvedUnitCost)`.

The endpoint then applies that one article-level minimum whenever a row has no sale-line cost.

### Why this is not equivalent

If the same article had:
- different cost snapshots across historical sale lines;
- an Access snapshot plus a non-Access/legacy sale line with missing cost;
- a cost change over time;

then the line-level snapshot truth is lost. A low historical snapshot can be applied to another line, time or source and change margin contribution, weighted margin, margin coverage and recommendation inputs.

This also contradicts the snapshot implementation plan's stated reason for line-level granularity: preserve atomic cost evidence across arbitrary re-aggregation.

### Decision

Join/bind snapshot cost by `ProdajaStavkaId` (and active batch), never by article-level minimum. Product cost remains the later fallback only when the exact sale line has no historical/snapshot cost.

Owner: **RQ495**.

## F4 — P1: Shoe Type backend and frontend disagree on signed share validity

Backend:
- calculates `sharePct = rowRevenue / totalRevenue * 100` when total revenue is positive;
- passes the raw share into `AnalyticsDecisionRecommendationEngine`;
- a row can mathematically be negative or above 100% when signed returns in other rows change the net denominator.

Frontend:
- `shoeTypePercentRange.ts` accepts only 0..100;
- negative or >100 values become `N/A`;
- concentration removes those rows from its ranked dataset.

Therefore a share that influenced the backend recommendation can be invisible to the operator.

The formal SST contract preserves signed revenue and defines row share from the signed population; it does not authorize silently clamping/dropping a valid signed result.

### Decision

Use one backend-owned signed-share state:
- numeric signed share when the denominator is strictly positive and the arithmetic is valid;
- explicit unavailable only when the denominator/evidence is invalid;
- separately decide whether concentration/ranking should use signed share or a clearly named positive-contribution population.
The recommendation must expose the exact share basis it consumed, and the UI must show/qualify that same basis.

Owner: **RQ496**.

## F5 — P2: Shoe Type rank badges are table-position badges, not business ranks

The page calculates:
`rank = index + 1`
after the user-selected sort. The default sort is recommendation status. If the user sorts by name, margin or PoP, #1/#2/#3 move with that sort while the styling still looks like a top-business ranking.

Decision:
- either remove rank badges from arbitrary sorts;
- or make rank an explicit stable backend/business rank (for example revenue rank) and label it accordingly.
Never style current table position as an authoritative rank.

Owner: **RQ496**.

## F6 — P1/P2: negative previous-period revenue is labelled “Novo”

Current Shoe Type UI shows “Novo” whenever:
`previousPeriodRevenue <= 0 && currentRevenue > 0`.

A negative previous period can be caused by signed returns. That is not “new”; it is a non-positive/invalid percentage baseline.

Decision:
- `previous == 0` can be “Nova baza / bez prethodnog prometa” only when the backend can distinguish a measured zero from unavailable evidence;
- `previous < 0` must be “Nema validne PoP baze” (or equivalent), not “Novo”;
- previous-only rows from RQ457 remain truthful `-100%` only when the previous denominator is positive.

Owner: **RQ496**.

## F7 — P1 contract gap: Daily shifts have no explicit business-time basis

Daily Sales assigns shifts directly from `ProdajaZaglavlje.DatumProdaje.Hour`:
- shift 1 = 06:00-13:59;
- shift 2 = 14:00-21:59;
- other hours = off-shift.

The response exposes assignment status but no `shiftTimeZone`, `timestampBasis` or per-source time interpretation.

Existing forensic documentation says Access local datetimes are stamped with `DateTimeKind.Utc` **without offset conversion**, intentionally preserving the source wall clock. Other code paths can persist real UTC instants. If both timestamp bases occur, the same stored UTC-looking hour has different business meaning and DST can move a true UTC instant across a shift boundary.

This audit does **not** claim a current production misclassification without a mixed-source live sample. It does establish that the current contract cannot prove that “06:00-13:59” means store-local business time for every source.

### Decision

The screen's shift labels are business/store-local concepts. The backend must:
1. declare the timestamp basis for each supported source;
2. normalize an actual UTC instant to the store/business timezone before shift assignment;
3. preserve Access wall-clock semantics only when the importer explicitly marks that timestamp basis rather than relying on a misleading `Utc` kind;
4. fail closed / mark shift evidence partial when source time basis is unknown;
5. return `shiftTimeZone` and `shiftTimestampBasis` provenance.

Owner: **RQ497**.

## F8 — current Daily signed/off-shift behavior is otherwise intentionally correct

Do **not** regress the RQ383/RQ431 contracts while fixing F7:
- full-day revenue/quantity includes signed retail rows even when they are off-shift;
- first/second shift columns contain only measured classified rows;
- no-time fallback does not fabricate first-shift values;
- top supplier/Ostali quantities remain signed and concentration fails closed on impossible positive-denominator totals.

## F9 — current Shoe Type identity/margin behavior is otherwise intentionally correct

Do **not** reopen RQ457:
- group by `ShoeTypeIdAtSale`, not current master type;
- null ID is the unknown bucket; a real type literally named “Nepoznato” remains a known ID;
- previous-only IDs remain visible;
- headline average margin is weighted over covered revenue;
- zero/negative aggregate margin-contribution share remains unavailable rather than sign-inverted.

RQ495 changes only **which exact sale-line cost evidence** feeds that margin.

## Queue routing

| Prompt | Status at registration | Priority | Purpose |
|---|---|---|---|
| RQ494 | WAITING | P0 | make sale-header DataOrigin the certified sales-scope source and repair oracle/certification |
| RQ495 | WAITING after RQ494 | P0/P1 | restore exact sale-line snapshot-cost binding |
| RQ496 | WAITING after RQ494/RQ495 | P1 | signed share / recommendation / UI parity plus rank and negative-base truth |
| RQ497 | WAITING after RQ494 | P1 | explicit business-time/timestamp-basis contract for Daily shifts |

No current primary/READY pointer is changed by this audit. RQ494 also touches `AllEndpoints.cs`, which is currently owned by other READY/queued work; it must be sequenced, not claimed concurrently.

## Recommended execution order inside this family

1. RQ494 — population truth comes first.
2. RQ495 — financial/margin evidence at exact sale-line grain.
3. RQ496 — display/decision semantics after population and cost are stable.
4. RQ497 can follow RQ494 independently of RQ495/RQ496 once the Daily service path is free.

After these, reuse the existing RQ448 browser/render/export certification lane instead of creating another screenshot-proof prompt.
