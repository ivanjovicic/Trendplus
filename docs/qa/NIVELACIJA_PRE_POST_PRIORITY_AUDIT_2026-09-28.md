# Nivelacija Pre/Post + Pre-Nivelacija Prioriteti audit — 2026-09-28

Repo: `ivanjovicic/Trendplus`  
Surfaces:
- `/analytics/nivelacije-pre-post`
- `/analytics/pre-nivelacija-prioriteti`

## Evidence boundary

The browser/web helper available in this audit could not open the Vercel or Render URLs, so this note does **not** claim a new browser-render smoke. Same-day repository production evidence is reused only where it directly applies. The 2026-09-28 live Supplier/Products audit called the same `/api/analytics/vendor-sales-nivelacija` endpoint used by the direct Pre/Post screen and received `meta.success=false`, `vendor_sales_nivelacija_contract_missing`, with empty result collections. Q83 already owns the missing `change_percent_revenue_semantic` live schema contract. No equivalent same-day production failure evidence exists for the Pre-Nivelacija Priority endpoint; findings there are code/contract/test findings.

## What the two screens actually measure

| Surface | Decision grain / population | Time basis | Main value |
|---|---|---|---|
| Pre/Post nivelacija | price-event cohort, then article/vendor aggregation | 30 days before vs 30 days after each selected nivelacija event | observe what happened around a price event |
| Pre-Nivelacija Prioriteti | current in-stock `Artikli` candidates | rolling 180-day sales + 7-day/previous-7-day signal + 180-day markdown history | decide which current stock deserves attention before another price action |

These screens should **not** be forced to show identical revenue totals. They should, however, share the same certified retail receipt population, signed-return semantics, store/grain truth, cost/evidence rules and provenance vocabulary.

## Confirmed findings

### F1 — Pre-Nivelacija does not use the certified retail receipt population

`Api/Endpoints/PreNivelacijaPriorityEndpoints.cs` reads `ProdajaStavke` + `ProdajaZaglavlja` directly for the rolling sales windows, but does not use `SalesReceiptPopulationPolicy` and does not exclude trimmed/case-insensitive `DUG` / `KOREKCIJA`. RQ456 made that exclusion canonical for certified retail turnover while retaining signed retail returns.

Impact: the same correction/debt receipt can be excluded on Daily/Supplier/Shoe Type/Color and still alter Pre-Nivelacija velocity, WoW, recency, score and recommendation.

Owner: **RQ489**.

### F2 — any signed return currently hard-blocks a Pre-Nivelacija recommendation

`ResolveSalesEvidence` returns incomplete `signed_adjustment` whenever `NegativeUnits180 < 0`, even when signed net sales remain positive. The page then says the recommendation is blocked until the signed balance is confirmed.

That is stricter than the canonical retail population contract: legitimate signed customer returns remain part of net sales. Presence of one return is evidence/provenance, not by itself proof that the sales signal is unusable.

Related defect: `LastSale` is currently `MAX(DatumProdaje)` over every signed line. A return can therefore reset `daysSinceLastSale` to zero even though it is not a positive sale.

Owner: **RQ489**.

### F3 — Pre/Post also bypasses the certified DUG/KOREKCIJA policy

`Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql` builds `sales_daily` directly from sales lines/headers and the scoped raw path in `BuildVendorSalesNivelacijaScopedSourceSql()` does the same. Neither path applies the canonical receipt-number exclusion.

Impact: Pre/Post can disagree with Supplier/Daily/Shoe Type/Color for reasons unrelated to the actual price event.

Owner: **RQ490**, sequenced behind the active AllEndpoints owner and the Q83 SQL owner.

### F4 — Pre/Post “coverage” is sales activity density, not data coverage

The SQL defines `coverage_pre30/post30` from the count of distinct days that had sales divided by 30. The frontend treats those fields as “post-window pokrivenost”, and concentration quality uses thresholds such as 20% and 60%.

A fully observed 30-day window in which an article sells on only two days is therefore displayed as roughly 6.7% “coverage”. That is sparse demand/sample evidence, **not** missing data.

This is especially dangerous for footwear/SKU-size data, where intermittent demand is normal.

Owner: **RQ491**.

### F5 — Pre-Nivelacija is operationally store-sensitive but has no declared store grain

`Artikli` has `IDObjekat` and its `Kolicina` is the stock used by the candidate. The Pre-Nivelacija DTO drops the store identity, the endpoint exposes no store filter, and the sales query groups only by `IdArtikal` without reconciling receipt store with the inventory row's store.

Impact:
- same PLU in more than one store can be operationally ambiguous;
- local stock can be combined with sales from another receipt store if source data contains cross-store use of the article id;
- an action queue cannot say where the action should occur.

For an actionable priority surface, the safe grain is **SKU/article + store**. All-store summaries may aggregate those rows, but must not silently erase store identity.

Owner: **RQ492**.

### F6 — the priority score is request-cohort-relative but looks like a stable 0–100 business score

`PercentileNormalize` is not a percentile. It divides by the maximum stock/velocity in the current candidate universe. Adding one unrelated extreme SKU can therefore change other rows' stock-pressure/velocity components and their score, even when those rows' own data did not change.

Because `minScore` is then used as a filter, the same numeric threshold does not necessarily mean the same business risk across scopes/datasets.

Owner: **RQ493**.

### F7 — scenario outputs are deterministic heuristics, not calibrated expected outcomes

`PreNivelacijaScoringService` currently hard-codes:
- highlight boost from 15% to 45% based on score;
- markdown discount from 8% to 35%;
- demand multiplier `1 + discount * 1.8`;
- Bayesian smoothing with a fixed prior.

The UI does say “PROCENA” and “nije garantovani prihod”, which is good, but still presents exact RSD values under labels such as “Procena povećanja prihoda”. There is no proof here that those multipliers are calibrated against realised Pre/Post outcomes.

Near-term truth: expose the heuristic basis/version and do not let an uncalibrated scenario delta masquerade as empirical uplift. Empirical calibration/backtesting remains a Recommendation Learning concern.

Owner: **RQ493**, with long-term causal/outcome calibration handed to RL12.

### F8 — “Kandidati” tooltip overstates actionability

The page says candidates “imaju zalihu i prodajni signal dovoljan za intervenciju”, while the backend intentionally retains candidates with `no_sales_in_window`, missing cost or other blocked evidence states. RQ432 correctly separated score-band population from recommendation eligibility; this tooltip regressed that distinction.

Owner: **RQ493** (same score/population semantics).

### F9 — Pre/Post causal wording is stronger than the implemented DiD proof

`vw_nivelacija_did` selects one control article from the same supplier/category, ranked by closeness of pre-period revenue and quantity, then calculates the difference in before/after changes. The implementation does not by itself prove parallel pre-trends, treatment/control overlap, absence of contemporaneous interventions or statistical uncertainty.

The current tooltip says “Procena uzročnog efekta nivelacije”. Until stronger causal validation exists, the defensible wording is “DiD procena / kontrolisana razlika” or equivalent, with method/limitations visible. Do not present it as established causal impact.

Existing owner: **RQ140 acceptance addendum**; long-term causal validation: **RL12**. No duplicate RQ is registered.

### F10 — production Pre/Post schema failure is already owned

The handler checks for `vw_vendor_sales_nivelacija.change_percent_revenue_semantic` before it selects the scoped raw-fact path. Therefore a missing view contract can block even a request that would otherwise use `BuildVendorSalesNivelacijaScopedSourceSql()`.

This audit does not invent a bypass. Q83 must prove whether the view gate is required for the scoped path; a scoped fallback is acceptable only if it independently satisfies exactly the same revenue/nullability contract and provenance tests.

Existing owner: **Q83**; Supplier assortment manifestation remains **RQ475**.

## Findings deliberately not re-queued

- KPI population definitions and coverage counts already fixed by RQ432.
- Pre-Nivelacija facet universe already fixed by RQ433.
- WoW signed percentage display/labels already fixed by RQ434.
- dead/frontend-derived Pre/Post KPIs already removed by RQ436.
- general pre/post comparability/fail-closed semantics remain RQ140.
- live missing view/schema/nullability remains Q83; supplier assortment readiness remains RQ475.
- the 15/25 unknown-supplier policy in RQ484 remains Supplier-only and is not copied onto these screens.

## Recommended product shape

Keep both screens:
- **Pre-Nivelacija Prioriteti** = forward operational queue: where to inspect/act next, by SKU + store, with explicit evidence and heuristic-vs-measured provenance.
- **Pre/Post nivelacija** = retrospective event analysis: what happened around completed price events, with comparable cohort/sample-strength and causal limitations.

Do not merge them into one page. Link them: a priority row may open historical Pre/Post evidence for the same SKU/store when such price-event history exists, and a Pre/Post detail may link back to the current priority state. The two screens answer different questions but should share population/provenance contracts.

## Queue routing

- **RQ489 READY** — Pre-Nivelacija certified retail population + signed-return/recency semantics.
- **RQ490 WAITING** — Pre/Post certified receipt population; waits for conflicting AllEndpoints/Q83 ownership to clear.
- **RQ491 WAITING after RQ490** — split sales-activity/sample strength from actual data coverage.
- **RQ492 WAITING after RQ489** — make SKU+store the actionable Pre-Nivelacija grain.
- **RQ493 WAITING after RQ489/RQ492** — score stability, heuristic-scenario truth and KPI wording.
- **Q83 amended**, no duplicate schema prompt.
- **RQ140 acceptance clarified**, no duplicate causal prompt.
