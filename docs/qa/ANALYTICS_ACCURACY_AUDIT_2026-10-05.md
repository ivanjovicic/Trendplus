# Trendplus Analytics accuracy audit (ultra-deep) — 2026-10-05

Datum: 2026-10-05 (Europe/Belgrade).
Osnova: `origin/main` **`4ed7012dfa2ed768f835797db40079e7a8a842bc`** (`chore(db): guard EF migration discovery`).
Način: adversarial code+test review on box worktree `/workspace/rh`; **fix local deterministic gaps**; no Cursor cloud agents; no Trendplus2 WT edits.
Dokaz: `.ai/runs/2026-10-05-analytics-accuracy-audit-evidence.md`.

Napomena o opsegu: punih 58 sekcija iz parent brifa nije bilo dostupno u executor storage-u; audit je urađen po Ivanovom sažetom spisku domena + §55/§56 obaveznim isporukama, uz red-team null→0 prolaz.

Oznake: **code** = iz koda/testova na bazi; **fix** = ispravljeno u ovom delivery-ju; **residual** = svesno ostavljeno; **UNPROVEN-RUNTIME** = treba live/provider.

---

## §55 — Definition of Done (this delivery)

| # | Criterion | Status |
|---|---|---|
| 1 | Fresh `origin/main` recorded before edits | PASS (`4ed7012`) |
| 2 | AGENTS / queue protocol consulted; no stolen READY claim | PASS (RQ588 left IN_PROGRESS) |
| 3 | Accuracy domains from brief reviewed adversarially | PASS (see §56 matrix) |
| 4 | Safe local deterministic bugs fixed (not report-only) | PASS (2 product fixes) |
| 5 | Focused regression tests added/green | PASS (4/4) |
| 6 | Backend remains authoritative; unknown↛0 in changed paths | PASS |
| 7 | Docs/evidence durable under `.ai/runs/` (+ qa audit) | PASS |
| 8 | Logical commit(s) + PC bundle FF push to main | pending at evidence write |
| 9 | Trendplus2 working tree untouched; no cloud agents | PASS |
| 10 | Residuals explicitly classified (no silent green) | PASS |

---

## §56 — Final report

### 56.1 Base and routing
- Start SHA: `4ed7012` (includes post-`c9c4167` work: RQ557 markdown outcome ledger, RQ586 DailySales timezone root bind, RQ588 migration discovery guard + docs).
- Queue: Current READY **RQ588 IN_PROGRESS** — audit did not claim it.
- Program: direct-user-request accuracy audit.

### 56.2 Surface inventory (analytics)
Tier-1 reviewed at contract/code level: Daily Sales, Dashboard bootstrap, Product Decision, Supplier/ShoeType/Color ops, Inventory valuation/aging, Pre/Post nivelacija + outcome ledger, Pre-Nivelacija, Data Quality health/integrity, Decision Board/Actions, Supplier Hub, Insight Studio (quarantined), Pilot Intake/Readiness, Executive board, freshness/refresh, startup readiness.

### 56.3 Domain matrix

| # | Domain | Finding | Disposition |
|---|---|---|---|
| 1 | Inventory of surfaces | Map above; quarantined Insight Studio still experimental | noted |
| 2 | Period | Half-open Daily (RQ584) + horizon (RQ569/570/571) present; exclusive end wired | OK (code) |
| 3 | Filter | Store/supplier/dataScope in cache keys for major routes | OK (code) |
| 4 | Revenue | Ops screens share certified window pattern from prior oracles | OK / reuse |
| 5 | Margin | Margin policy distinguishes coverage; V2 margin-alerts prices nullable after prior harden | OK |
| 6 | Null / unknown→0 | **BUG**: PDC alternative MARKDOWN/DO_NOT_ORDER treated null margin/trend as 0 → poor_margin / declining bonuses. **BUG**: QuickInsights `BestDayRevenue ?? 0` when `BestDay` null | **fix** |
| 7 | Inventory stock | Valuation/aging from receipts (RQ576); DTO still emits 0 cost with `costMissing` | residual (flagged contract) |
| 8 | Nivelacija | Pre/Post evidence flags + mature-window zeroing; RQ557 outcome ledger uses emptyWindowIsZero only for post | OK (code) |
| 9 | Aggregation | PDC summary money totals still `+= estimate ?? 0` | **residual** |
| 10 | Unknown buckets | CategoricalDimensionCoveragePolicy + FE chart gate | OK |
| 11 | Previous-only / trend | `ComputeTrendPct` returns null when previous missing/≤0 | OK |
| 12 | Returns | Daily othersCount signed remainder documents returns | OK |
| 13 | Shares | SupplierSharePolicy fail-closed on non-finite | OK |
| 14 | SQL | No new raw SQL defect found this pass; nivelacija/outcome use scoped facts | OK |
| 15 | EF | RQ588 discovery guard landed on tip; not re-owned | out-of-scope claim |
| 16 | Decimal | Mix of `Math.Round` default vs `AwayFromZero` remains | residual (low) |
| 17 | FE derived | Intelligence derived aggregates only for experimental Insight; Dashboard charts use BE series + coverage gate | OK |
| 18 | DTO | QuickInsights BestDayRevenue widened to `decimal?` | **fix** |
| 19 | Cache | Keys include period/store/supplier/scope for sampled endpoints | OK |
| 20 | Freshness | Global banner + SLA (RQ583); horizon separate (RQ569) | OK |
| 21 | DQ | Health/integrity contracts from RQ578/579; live excellence-on-stale was prior finding | UNPROVEN-RUNTIME live |
| 22 | Test oracle quality | New nullability tests assert unavailable ≠ zero | **fix** |
| 23 | Cross-screen oracle | Not re-executed live; prior Jul window certification reused as reference only | UNPROVEN-RUNTIME this pass |
| 24 | Boundary kits | Half-open / horizon policies covered by existing tests; not expanded | residual coverage |
| 25 | Metamorphic | No new metamorphic suite this pass | residual |
| 26 | Golden | Did not regenerate goldens; distrust unchanged goldens without oracle | noted |
| 27 | Export | No export≠screen defect isolated this pass | OK / limited scan |
| 28 | Pagination | Supplier open-action paging (RQ48) present; no new page-sum vs total bug proven | OK |
| 29 | Startup | `/ready` vs schema still UNPROVEN-RUNTIME (RQ587) | residual |
| 30 | Docs / RQ | Evidence + this audit; no status theft; prefer fix over new RQ | PASS |
| 31 | Red-team null→0 | Targeted scan of PDC alternatives + QuickInsights + known coalesce sites | PASS (2 fixes) |

### 56.4 Fixes delivered
1. **Product Decision alternative scoring/reason codes** — null `MarginPct`/`TrendPct` no longer receive poor-margin or declining-trend bonuses (`CachedAnalyticsEndpoints`).
2. **QuickInsights** — `BestDayRevenue` is `null` when there is no best day (BE + FE type); UI already falls back to "Nije dostupno".

### 56.5 Tests
- `ProductDecisionAlternativeScoreNullabilityTests` (3)
- `QuickInsightsNullBestDayContractTests` (1)
- Result: **4/4 passed**

### 56.6 Residuals / follow-ups (prefer fix, not RQ unless PO)
1. PDC summary `LostSalesEstimate`/`SlowStockCapital` coverage-aware totals (stop `?? 0` into certified KPI).
2. Inventory list DTO 0-cost with `costMissing` — confirm every FE path hides value.
3. Live cross-screen oracle re-probe after next import.
4. Decimal MidpointRounding consistency pass (low priority).

### 56.7 Red-team conclusion
Green tests and DONE prompts were not trusted blindly. Two user-visible trust lies (null→0 in alternative ranking signals; best-day revenue 0 without a best day) were falsified and fixed. Broader live oracle and PDC money-summary coverage remain open.

### 56.8 Delivery
- Commits: see git log on delivery branch.
- Push: PC bundle FF via DESKTOP-V877DAD; Trendplus2 WT not used for apply.
