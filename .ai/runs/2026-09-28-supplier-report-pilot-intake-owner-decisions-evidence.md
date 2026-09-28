# 2026-09-28 Owner decisions (Supplier report / Pilot intake) and PS de-dup (grok)

- Date: 2026-09-28 (Europe/Belgrade)
- Requested by: Ivan Jovičić (owner), follow-up to `.ai/runs/2026-09-28-supplier-report-pilot-intake-audit-evidence.md`.
- Scope: docs/queue only; no product code. Local commits via private index + CAS `update-ref`; nothing pushed.
- Base: `main` = `origin/main` = `5fe1f30b`. The commits after `c5a1937f` (`641a4a0c`, `991d05ff`, `cf586707`, `5fe1f30b`) are docs-only, so the code line references at `c5a1937f` still hold.

## Id collision check

- `RQ461`–`RQ468` are this agent's Supplier report / Pilot intake prompts (`657647ad`, `f79d5d92`, `78baa640`, all on `origin/main`).
- `RQ469`–`RQ476` were registered by the Products/Supplier live audit (`641a4a0c`); `RQ477`–`RQ482` by `cf586707`. There are no duplicate section ids; the validators pass.
- Residual: the F1–F8 table in `docs/qa/PRODUCTS_SUPPLIER_LIVE_AUDIT_2026-09-28.md` still names `RQ461`–`RQ468`. A correction addendum was appended to that file (F1→`RQ469` … F8→`RQ476`); the original lines are unchanged.
- No task locks exist for `RQ46x`/`RQ47x`/`RQ48x` (`.ai/task-locks`, checked 2026-09-28 ~11:40 CEST). Only `origin/main` exists remotely, with no feature branch.
- The max id before registration was `RQ482`; new ids start at `RQ483` (re-checked right before committing).

## Task A — owner decisions recorded

| Prompt | Decision recorded | Status after | Why not READY now |
|---|---|---|---|
| `RQ463` | Option A (fail closed): exact only if the range equals the rolling window on the refresh date; arbitrary period → requested dates shown, `usedFallback=true`, `requested_range_not_precomputed`, recommendation blocked but report rendered; sales exact via `RQ464`; historical recompute out of scope | WAITING, `Ready after: RQ461 DONE` | same backend file/metadata contract as READY `RQ461` (`PROMPT_QUEUE_PROTOCOL.md:108`: overlapping prompts stay WAITING) |
| `RQ464` | Main KPIs = Supplier overview parity (same population contract); „Rezultat oko sniženja“ as a separate block; DUG/KOREKCIJA excluded via `SalesReceiptPopulationPolicy`; missing cost not 0 (coverage shown); `povracaj_*`/`ReturnFact` = supplier returns, customer returns = signed `ProdajaStavke` lines, `DnevnikPromena` control only; sale-time attribution; receipt-store filter; one `SupplierDecisionScorePolicy` (short-term: port the MV model + parity test) | WAITING, `Ready after: RQ463 DONE` | same file and KPI/section contract as `RQ461`/`RQ463` |
| `RQ465` | Negotiation pack only with exactly one supplier; no „Finalni savet“/pack/negotiation action for `supplierId=null`; grow/risk only when the rule assigns them, no fallback | WAITING, `Ready after: RQ461 DONE`, not concurrently with `RQ463`/`RQ464` | same files as `RQ461` |
| `RQ467` | Readiness = data quality; insufficient signal not a gate (shown as „Pokrivenost poslovnim signalom“); blocked = distinct articles; DUG/KOREKCIJA excluded; returns not price errors; receipt vs article store with distinct labels; cost fallback `ps.NabavnaCena → a.NabavnaCenaDin → a.NabavnaCena` (> 0); default period anchored to `MAX(DatumProdaje)` for the scope, with a flagged import business-date fallback | WAITING, `Ready after: RQ466 DONE` | same builder function as READY `RQ466` |
| `RQ461` | addendum: leave room in the vocabulary for the follow-up blocks (no second contract change) | READY (unchanged) | — |
| `RQ466` | addendum: health window uses the resolved period, so the `RQ467` anchor flows through; predicates owned by `RQ467` | READY (unchanged) | — |
| `RQ462` | addendum: `RQ467` renders its new fields | READY (unchanged) | — |
| `RQ473` (other agent) | cross-reference only: the missing-cost decision may answer part of its margin-policy question; status unchanged | WAITING (unchanged) | owner confirmation |
| `RQ468` | unchanged (the new decision labels are already Serbian) | WAITING (unchanged) | — |

## Task B — PS de-dup (PS04, PS07, PS10, PS12–PS18)

Checked against `RQ441`–`RQ482`, DONE work and the current code at `c5a1937f`.

| PS | Mapping | Reason |
|---|---|---|
| `PS04` | new `RQ483` (WAITING after `RQ472` + threshold sign-off) | rule reachability and period-end measurement unowned; still present (`ProductDecisionReasoningHelper.cs:76`, `:93`; `CachedAnalyticsEndpoints.cs:6135-6137`, `:6209-6213`) |
| `PS07` | new `RQ484` (WAITING, owner decision) | gate policy for nivelacija evidence and the unknown bucket across Supplier/Shoe Type/Color (`AllEndpoints.cs:1784-1789`, `:2566-2571`, `:3235-3246`, `:1977-1984`); `RQ140` introduced the fail-closed gate deliberately |
| `PS10` | new `RQ485` (WAITING after `RQ469`–`RQ471`) | duplicate `h1`/toolbar still at `ProductDecisionCenterPage.tsx:1298`/`:1496`; URL state beyond `search`; impure sort `:1040-1049`; nulls `-9999` `:883-887` |
| `PS12` | new `RQ486` (READY) | trust-header staleness (`RQ444` residual), double validation, stores scope, duplicate store labels; no active prompt on these files |
| `PS13` | not registered: existing owners | sale-time attribution `RQ441`/`RQ445` DONE; Supplier report attribution decided in `RQ464`; the link silent-clear residual folded into `RQ486` |
| `PS14` | new `RQ487` (WAITING after `RQ474` diagnosis + baseline) | query cost unowned; cache-hit metadata only if a gap remains after `RQ141`/`RQ187` |
| `PS15` | merged into `RQ486` | same shell file; the overflow must be reproduced first |
| `PS16` | merged into `RQ485` + `RQ486` | local date defaults and Serbian display; server boundaries `RQ442` DONE |
| `PS17` | new `RQ488` (WAITING, residual) | the listed Product Decision ASCII labels and the Supplier shell ASCII are already fixed on current code (dropped as DONE); remaining: English engine summaries surfaced on Supplier overview (`AnalyticsDecisionRecommendationEngine.cs:247`, `:274-275` → `SupplierSalesStatsPage.tsx:639-640`), the export ratio/header, `analyticsApi.ts:844` |
| `PS18` | merged into `RQ486` | same file; the dead `displaySignalLabel` (1 occurrence = definition), impure `handleSort` `:1585-1594`, sort-dependent rank badges `:2195-2202` |

Existing mapping honoured: PS01→`RQ469`, PS02→`RQ472`, PS03→`RQ473`, PS05→`RQ470`, PS06→`RQ474`, PS08→`RQ475` (canonical), PS09→`RQ471`, PS11 (part)→`RQ476`. The mapping is also recorded as a dated addendum in `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md`.

## READY set after this run

`Current READY prompt: RQ461`; also READY: `RQ462`, `RQ466`, `RQ486` (distinct feature families and files).

## Open items for Ivan

- `RQ484` (PS07): gate policy decision.
- `RQ483` (PS04): threshold sign-off.
- Optional: promote only the backend part of `RQ464` (views, live SQL, score policy, parity tests) now, with `RQ461`'s renderer/rows as avoid-paths, if parallel lanes are wanted.
- `RQ473` owner to confirm whether the `RQ464` missing-cost decision closes its margin-policy question.

## Validation

See the commit output: `git diff --check` plus the 6 validators on every commit.
