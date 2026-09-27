# 2026-09-27 — Today commit review and fixes (evidence)

- Queue: direct-user-request ("Analiziraj nekoliko danasnjih komitova detaljno i popravi/unapredi sta nije potpuno ili tacno")
- Owner: frontend analytics (`Klijent/clientapp`), sort URL helper + Operations pages
- Base: `origin/main` f0228582, isolated worktree `Trendplus2-review`, branch `review-today-fixes`

## Commits reviewed

| Commit | Prompt | Verdict |
|---|---|---|
| 4138fd9a | RQ326 Pre-Nivelacija sort in URL | Functionally correct, but duplicated its own `parseSortField/parseSortDir` instead of the shared `analyticsTableSortUrl` helper the prompt pointed at (a second owner of the same URL contract). Fixed. |
| dc20d775 | RQ327 Daily Sales sort in URL | Accepted any `supplier:<n>` from the URL, even beyond `topN` (a sort on a non-rendered column). An invalid `sort` with a valid `dir` kept that `dir` on the fallback column (e.g. `date asc`). Also duplicated parsing. Fixed. |
| 9526ac93 | RQ328 Pre/Post expansion on refetch | Reconciliation kept positional `row:<index>` keys (used when `vendorId` is missing or duplicated). After a refetch these can point at a different vendor, which is a wrong-vendor detail risk. No page-level proof that Apply keeps the expansion. Fixed plus tests. |
| 9bbbc9b5 | RQ324 SKU size curve error | OK (own delivery, error + retry covered). |
| ac732e4c | RQ330 Pre/Post focus row context | OK. |
| ea4ac7cc | RQ329 dead shoe type truncation hint | OK. |
| d2c47908 | RQ309 nav icons | OK. |
| 6b590226 | RQ311 guardrail assignment regex | OK. |
| cde5a587 | RQ310 operations spec routes | OK. |

## Changes

- `src/utils/analyticsTableSortUrl.ts`: `defaultDir` may be a per-field function. `writeAnalyticsTableSort` takes optional `defaults` and omits the default field and direction. Backward compatible for existing callers (Color, Shoe Type).
- `src/pages/PreNivelacijaPriorityPage.tsx`: uses the shared helper. Local parsers removed. URL semantics unchanged (default field omitted, field-specific default dir omitted).
- `src/pages/DailySalesStatsPage.tsx`: uses the shared helper. `supplier:<n>` is valid only for `n < topN`. An invalid `sort` resets to `date desc`, so the orphan `dir` is dropped. The canonical effect then cleans the URL.
- `src/pages/ProdajaPrePostNivelacijePage.tsx`: `reconcilePrePostExpandedVendorKey` keeps only stable `id:` keys.
- Tests:
  - New `src/utils/__tests__/analyticsTableSortUrl.spec.ts`.
  - Daily spec: `topN=5&sort=supplier:7&dir=asc` becomes a clean URL.
  - Expansion unit test for `row:` keys.
  - Pre/Post page spec: Apply refetch keeps the expanded vendor detail.
- `scripts/known-guardrail-baseline.json`:
  - Line shifts: Daily 879→875, Pre-Nivelacija 706→697.
  - Pre-Nivelacija `decisionScore_assign` reason sharpened to name the backend `recommendationAllowed` gate and the covering spec.

## Validation

- `npx vitest run` on the helper, Daily (spec + premium), Pre/Post (page + expansion), Pre-Nivelacija and Color specs: 7 files, 153 tests passed.
  - The first run failed on the new Daily test (`sort=date&dir=asc` remained). This was a product defect and has been fixed.
- `npm run check:analytics-guardrails`: OK (encoding, self-test, baseline-only, `tsc -b`).
- `npm run build`: OK (the chunk-size warning was already there before this change).

## Not done / risks

- The guardrail reports 4 stale baseline entries as `BASELINE REMOVED`: ExecutiveDecisionBoardPage:436, ProductDecisionCenterPage:1800, SupplierExplainabilitySnapshot:146/151. These are pre-existing and unrelated to this diff. They were left in place because another workspace has uncommitted edits to the same baseline file. Next: prune them in a dedicated baseline-hygiene change.
- The queue summary table vs section status mismatches (RQ128 and others) remain out of scope.

## Delivery

- Implementation SHA `2c9e3252`, pushed directly to `main` (rebased over c3df38ca; the upstream delta was docs/queue only). `git merge-base --is-ancestor 2c9e3252 origin/main` returned 0.
- The guardrail still passes on the rebased tree.
