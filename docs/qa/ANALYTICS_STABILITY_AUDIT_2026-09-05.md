# Analytics Stability Audit

Date: 2026-09-05  
Repository: `ivanjovicic/Trendplus`  
Queue: `direct-user-request`  
Owner program: Analytics Reliability (`RQ`)

## Verdict

The current main branch contains substantial reliability work, but the requested analytics surface is not yet fully proven. This audit found three concrete frontend defects or proof gaps that are bounded enough to queue independently. It does not claim live database, migration, refresh-worker, deployed-browser, or complete cross-route parity proof; those remain owned by `STAB16`, `RQ141`, `RQ145` and `RQ146`.

The queue is intentionally refilled with exactly one runnable prompt:

- `RQ154` is `READY`.
- `RQ155` and `RQ156` are valid follow-ups and remain `WAITING` until the queue advances.
- Forecast, Shopify, vendor comparison and other excluded work is not promoted.

## Review basis

Read before this audit:

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/ANALYTICS_ROUTE_LINEAGE_MATRIX_2026-09-05.md`
- `docs/qa/ANALYTICS_SUSPICIOUS_RESULT_AUDIT_2026-09-05.md`

Git history reviewed for the affected files includes:

- `570a31e8 fix(analytics): harden pre-post nivelacija comparability`
- `41790622 fix(analytics): fail closed on missing trust metadata`
- `29a5943a fix(analytics): harden trust metadata and decision surfaces`
- `69511be0 Harden analytics indicators against incomplete evidence`
- `3d408866 fix(analytics): add daily sales trust meta`
- `e77af0ff fix(analytics): repair scoped dashboard fallback trust`

The history shows that these are residual local presentation/contract-test gaps, not a reason to reopen already delivered RQ151/RQ152 or to duplicate the broad RQ141/RQ145/RQ146 runtime work.

## Confirmed findings

| ID | Surface | Evidence | User risk | Queue |
|---|---|---|---|---|
| F1 | Daily Sales trend, shift-mix, weekday and supplier charts | `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:327-332` returns `0` for an empty rolling window and sums every value without a finite/known-value check. Tooltip formatters at `:1651`, `:1690`, `:1738` and `:1791` use `Number(value ?? 0)`. Summary/quality values also use `?? 0` at `:347-349`, `:865-921`. | Missing, partial, non-finite or missing-denominator evidence can be shown as measured zero; MA7 and anomaly output can be contaminated. A real zero must remain distinguishable. | `RQ154` |
| F2 | `/analytics` top gainers/losers | `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:868-878` uses `(row.trendPct ?? 0)` for filtering and sorting. A missing trend is silently excluded from both lists instead of being visible as unavailable. | Users can mistake an incomplete trend population for a complete ranking and miss an unknown signal. | `RQ155` |
| F3 | Supplier, Color and Shoe Type pre/post presentation | `SupplierSalesStatsPage.tsx:363,428`, `ColorSalesStatsPage.tsx:204` and `ShoeTypeSalesStatsPage.tsx:293` use `(prePostNivelacijaRevenueCoveragePct ?? 0) <= 0`. Existing tests cover some `null` payloads, but do not prove that unknown coverage is not treated as measured zero/no coverage or that the same state is preserved in detail/export/chart paths. | Unknown coverage can enter a branch that describes a zero-coverage case, while the UI lacks an explicit unavailable distinction. This can overstate the certainty of pre/post interpretation. | `RQ156`; broad causal/runtime proof remains `RQ140`/`RQ145` |

## Existing proof and remaining gaps

| Requirement | Current evidence | Truth after this audit |
|---|---|---|
| Empty success is not server error | Existing Daily Sales, Color/Shoe and Data Quality tests cover selected empty states | Partly proven; RQ154 must add Daily Sales empty/chart state regression |
| Null versus genuine zero | RQ139, RQ144 and RQ152 cover selected backend/derived paths | Not complete on Daily Sales chart/summary and Dashboard trend ranking |
| NaN/Infinity rejection | RQ152 covers derived builders; the affected page formatters are not covered by counterexample tests | Open in RQ154/RQ155/RQ156 scopes |
| Stale/unknown/partial/fallback visibility | Selected trust-header tests exist | Broad route/runtime proof remains unproven and must not be inferred from these local tests |
| Period and scope correctness | Static route lineage matrix exists; selected RQ137 paths are repaired | Live endpoint/refresh/schema proof remains RQ141/RQ146/STAB16 |
| Backend owns decisions and recommendation status | Selected contract work exists; Executive Board local ranking fallbacks remain under RQ143 | Do not duplicate or solve ranking ownership inside RQ154-RQ156 |
| Table/chart/detail/export/report parity | Selected tests exist, but no complete fixture parity across all requested routes | RQ145 remains the owner; RQ154-RQ156 must not claim global parity |
| Chart size 0/-1, theme and browser console | Static code and mocked chart tests are insufficient | Runtime smoke remains unproven under STAB16/RQ145 |

## Prompt promotion decision

`RQ154` is the smallest same-owner repair with a concrete reproducer in the existing Daily Sales page and nearest tests. It does not require a schema, migration, live refresh worker or product decision. `RQ155` is a separate Dashboard ranking-visibility repair and `RQ156` is a presentation-only pre/post coverage state repair; both are queued behind RQ154 to preserve the one-READY queue invariant.

No prompt in this audit promotes forecast calibration, Shopify, vendor comparison, production access, destructive schema work or recommendation-formula changes.

## Non-proof boundaries

- Static findings do not prove that the deployed API returns the same payload shape.
- Existing frontend tests do not prove applied migrations, SQL/view existence, cache invalidation, refresh success or browser console cleanliness.
- A future prompt must not mark an item `DONE` without failing-first regression tests and the validation selected by `docs/ai/VALIDATION_SELECTOR.md`.
- If a contract decision is unclear, preserve unavailable state and document the ambiguity instead of converting it to zero.

