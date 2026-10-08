# 2026-10-08 — Post-close review of 016ea46f..dde6e017 (RQ593/RQ594/RQ595/RQ596/PERF18 + audit docs)

Base: `upstream/main` `dde6e017`. Box-only review; RQ555 (IN_PROGRESS, other owner) and P-UI-43 paths not changed beyond test alignment of P-UI-43's already-DONE compact trust header.

## Findings and fixes

| Commit(s) | Finding | Action |
|---|---|---|
| range-wide | Analytics Quality Gates red since 2026-10-07: page specs still expected the pre-compact trust header labels and Decision Board without router/`getStores` | `test(analytics)` alignment, specs query trust testids |
| 7f985492 RQ594 | Removing the movement gate turned every sold-out article with min 0 and no sales into a critical dopuna (floods top-24 workflow) | Dopuna requires configured minimum > 0 **or** observed sales; stockout with minimum keeps dopuna (censored demand) with explicit reason |
| 7f985492 RQ594 | Signal window shown as exclusive end in local TZ (one day late) | Inclusive UTC calendar dates; source-horizon copy in natural Serbian |
| 7f985492 RQ594 | English/jargon labels ("Markdown predlog", "Clearance lista"), slow-stock reason text | Serbian copy; receipt-age caveat for rasprodaja |
| c2442acc/850c0714 RQ596 | Coverage counted zero-stock rows and negative stock subtracted value; top value SKU classified B, single SKU C | Coverage over stocked rows only; Pareto keeps top item in A |
| c1106d93/86e91e64 RQ593 | Required coverage fields would reject whole Product Decision payload from an older Render API (manual deploy skew); loading showed fake "0 poznatih" | Fields optional, "Pokrivenost nije dostupna", no KPI coverage line while loading |
| 3d2797d2 RQ595 | Prompt Do 3 (3–5 negotiation facts) missing; stock as-of fell back to `generatedAtUtc` (stale stock looked current); English labels; `formatCalendarDate` one day early in Europe/Belgrade | Facts block (unknown omitted, no order advice), honest as-of, Serbian copy, UTC calendar formatting, TZ-proof tests |
| 42baa329/315f3e58 PERF18 | Bundle/route-lazy chart guard not run by any workflow | Added to Analytics Quality Gates after build |
| 1c2833fc/d6039395 docs | Audit §22 still called production freshness the RQ585 start gate (contradicts canonical queue/MASTER_ROADMAP) | Corrected; added dated note that RQ593–596 do not change outcome-proof scores |

## Reported only

- PERF19 Decision Board HTTP composition test (added before range, `14690775`) fails in CI "Analytics Tests & Data Integrity" but passes locally in isolation; environment/order dependent — not fixed here.
- Executive Decision Board reuse spec asserts raw English code text; Pilot Readiness reason line contains English action words; transfer reason still says "SKU".
