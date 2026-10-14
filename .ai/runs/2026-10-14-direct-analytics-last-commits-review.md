# Direct review — recent commits and analytics truth, 2026-10-14

Task: direct user-requested review/fix (no RQ task claimed).
Base main: `aafc7e2fefb15167331e98471ba98982314fd04a`; no open PRs at intake. Commit group reviewed: RQ657 supplier load race (#156), RQ659 navigation, RQ660 Product Decision null counts, RQ661 Home hard-coded KPI removal and related agent evidence, RQ648 PR #147 unblock.

## Verified source defects and scoped patches

1. RQ659 menu correctly renamed /analytics/daily-sales to "Dnevna prodaja", but DailySalesStatsPage's trust header and tests still showed "Prodaja po smenama". The page combines sales, suppliers, daily totals and conditional shift statistics. Aligned the page heading, subtitle, KPI aria-label and focused tests.
2. Daily Sales export metadata called smenska evidencija "Potpuna" whenever incompleteShiftCount was zero — including **zero rows**, missing shift-assignment provenance, or no-time fallback. A new pure common helper returns "Nije dostupno" for empty/untrusted, "Nepotpuna (N dan/a)" for verified gaps, "Potpuna" only for an observed measured period with all days complete. Added positive, partial, no-time and truly empty counterexamples.
3. Heuristic Insights used the optimistic "Stabilan pregled"/good tone merely because no threshold matched, not because sales stability was verified. Changed fallback to a neutral informational statement explicitly denying proof of stability or recommendation. The local quality toggle's clean label now states no *registered* warnings rather than certifying all data.
4. Home quick link claimed shifts as if they were always observed, and inventory turnover as if confirmed historical stock existed. Reworded to source-supported days/stores/suppliers and latest/available inventory instead.

## Validation / boundaries

- Source review: current components, real data response typings and premium/numeric spec fixtures, RQ659–RQ661 and RQ657 run logs; no backend/DB/deploy changes, no supplier/new marketplace development.
- Regression assertions added/updated in DailySalesStatsPage.numericState and DailySalesStatsPage/premium specs.
- GitHub Actions Frontend analytics tests/guardrails/build is the executable CI gate; a docs commit and diff review alone does not prove runtime correctness. No local npm/node/dotnet execution through the GitHub API connector was available.
- RQ648 remains READY for the separate remote-branch proof; don't automatically claim, merge or delete other branches. STAB16 provider freshness, RQ601 cost documents, RQ610 observed stock and RQ602 markdown outcome maturity remain external/source blocked.
- Additional potential future work: DailySales KPI text "RSD po komadu" relies on displayed rather than source-grain denominator, and stale/partial status can be under-emphasized outside the trust header. Review business meaning with independent source oracle before any mathematical rewrite. No fabricated bug fix.
