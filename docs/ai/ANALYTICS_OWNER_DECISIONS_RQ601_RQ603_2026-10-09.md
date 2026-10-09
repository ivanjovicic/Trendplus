# Product decision record: RQ601 and RQ603 — 2026-10-09

Decision provenance: user requested two concrete decisions in conversation after confirmation of green CI on `d84c6d0d` and main evidence tip `24231e77`. These are assistant-proposed delegated product decisions; user may override. This record documents decisions, **not** source-price evidence and **not** implementation/CI evidence.

## RQ601 — 20-item purchase-cost verification

**Decision: YES to the 20-item source-of-truth sample, but never invent the 20 calculation costs.** Execute steps 1–3 of RQ601 immediately on existing historical data. A fresh production import is not required to diagnose historical cost scales.

- Source of truth: original inbound **kalkulacija / purchase receipt line** cost for the same article, per pair, with dated receipt, store/warehouse (if applicable), supplier and source document/row ID. Preserve original currency, any FX conversion, discount, tax/VAT basis and pack-to-pair normalization; compare like-for-like RSD-per-pair net purchase cost. Do not treat sale/retail prices or master `Artikli.NabavnaCena` as authoritative just because they are present.
- Agent should first make a reproducible, read-only selection of **20 distinct article IDs** from real historical inventory/import, prioritizing 8 high on-hand-value, 6 extreme master-vs-inbound/sale-cost ratio anomalies, 4 cross-supplier ordinary control records, and 2 missing/fallback-lineage cases **with verifiable purchase lines**. De-duplicate categories; include at least four suppliers and multiple stores if source permits; disclose impossible strata rather than fabricating examples. Include article ID, supplier ID, stock quantity, source cost fields, anomalies, rationale, and a column for verified original kalkulacija unit cost/source line.
- If Kalkulacije purchase lines are already accessible through authorized read-only import/source records, **agent may fill the 20 cells with traced original source lines**. If they are not, produce the exact 20-article worksheet for the product owner to fill from the source Access/export/calculation documents. Never substitute inferred values or public web prices.
- Show timestamp and scope for each basis. Prefer latest relevant inbound record at or before the chosen stock-valuation date for this **scale audit**; note that an accounting stock-cost valuation policy (FIFO/weighted-average/etc.) remains a separate approved contract and may not be silently invented from the latest receipt.
- RQ601's ±5% acceptance comparison is meaningful only on matched quantities, unit and valuation date with genuine source costs; report total and per-item error, coverage and unresolved data instead of forcing a PASS. Before sample confirmation, all capital figures must be flagged provisional/not-certified, not globally deleted.
- Only if the verified sample proves a defect may an agent introduce a minimal mapping/precedence fix with a failing independent regression test and before/after comparison. No production DB writes, schema change or blind mass backfill. RQ601 can finish its diagnostic/reconciliation phase without the original sample; a definitive 'master scale correct/defective' verdict cannot.

**Routing:** RQ601 remains READY for the repository-local work. Do not call its full acceptance DONE until the genuine 20-item source sample exists and has been evaluated.

## RQ603 — primary analytics navigation

**Decision: YES to simplifying the main Analytics menu; this explicitly supersedes the 2026-09-29 RQ507 choice to keep Color and Daily in primary navigation, but does NOT authorize deletion of any routes or functionality.** Treat this as a grouping/labels change only, not a new data-coverage or recommendation implementation.

The **eight primary Analytics entries** (order is intentional):
1. `/analytics` — Pregled poslovanja
2. `/analytics/inventory` — Analitika zaliha
3. `/analytics/supplier` — Prodaja po dobavljačima
4. `/analytics/shoe-type-sales-stats` — Prodaja po tipu obuće
5. `/analytics/pre-nivelacija-prioriteti` — Prioriteti nivelacije
6. `/analytics/nivelacije-pre-post` — Pre/Posle nivelacije
7. `/analytics/actions` — Akcije i preporuke
8. `/analytics/data-quality` — Kvalitet podataka

Move the other current Analytics entries into a collapsed **Dodatne analize** group, retaining deep links, permissions and their existing trust/quality states:
- `/analytics/pilot-readiness` — Pilot spremnost
- `/analytics/decision-board` — Izvršni pregled odluka
- `/analytics/products` — Odluke o proizvodima
- `/analytics/decision-pulse` — Puls odluka
- `/analytics/supplier?tab=scorecard` — Ocena dobavljača (distinct from the main supplier sales view)
- `/analytics/daily-sales` — Prodaja po smenama
- `/analytics/color-sales-stats` — Prodaja po boji artikla
- `/analytics/supplier/report` — Izveštaj dobavljača
- `/analytics/reports/pilot-intake` — Pilot izveštaj kvaliteta podataka

The screenshot/browser/menu render should not assert that the above 8 are fully live-certified. Existing trust, missing-data, permission and freshness indications must remain visible. Do not invent a live backend `dimensionCoverage` dependency to render the menu: static grouping is approved now, optional gating only when a verified existing contract makes it safe and explicitly useful. Analytics destinations outside the current NAV_GROUPS should retain direct URLs too.

**Implementation guardrails:** change only `navConfig.ts`, its relevant nav/grouping tests and concise product/navigation docs; no route removals/redirect changes, no page/backend analytics business-logic changes. Preserve mobile/tablet keyboard/a11y behavior and collapsed-group discoverability. Test exact eight primary destinations, distinct supplier labels, no broken legacy deep links, stable access controls, responsive and keyboard navigation.

**Routing:** RQ603's owner-choice blocker is resolved by this decision; it is eligible to be promoted from WAITING to READY in the canonical RQ queue after fresh main, lock/branch/PR/path collision checks. RQ602 remains independently READY. Do not bypass queue claim procedure.

## Operator follow-up
1. Read this decision and current canonical RQ601/RQ602/RQ603 sections.
2. Update canonical RQ queue status/Ready-after and the Master Roadmap routing summary in a collision-safe small commit (do not silently modify old DONE evidence).
3. Claim RQ601 (diagnostics) and/or RQ603 (navigation) with disjoint paths. Start RQ602 in parallel only after exact path ownership check.
4. Deliver source worksheet + reconciliation evidence for RQ601; never state the 20 reference purchase values have been provided until verified.
5. Keep STAB16 production proof separate; green CI alone is not a production-data certificate.
