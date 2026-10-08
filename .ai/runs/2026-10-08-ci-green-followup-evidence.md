# 2026-10-08 — CI red on d1dc0a07 and open review items (follow-up)

Base: `origin/main` `d1dc0a07`. Box-only. RQ555/P-UI-43 paths not touched.

## CI root causes

| Workflow / job | Failure | Root cause | Fix |
|---|---|---|---|
| Analytics Tests & Data Integrity › Complete backend suite | PERF19 `DecisionBoardHttp_ProfilesColdWarmAndPreservesBusinessPayload` — no `Cache MISS` log | `Program.cs` reads `AnalyticsCache:Provider` (and loopback policy) from `builder.Configuration` before `WebApplicationFactory.ConfigureAppConfiguration` sources apply. Locally (Development) `appsettings.Development.json` resolves `memory`; in CI (`ASPNETCORE_ENVIRONMENT=Testing`) the non-development default resolved Redis/Hybrid, whose logs go to Serilog, not the capturing provider. Reproduced locally with `ASPNETCORE_ENVIRONMENT=Testing`. | Factory passes its settings with `UseSetting` too. Not a perf gate (no timing thresholds) — stays in the correctness suite. |
| Analytics Quality Gates › Frontend analytics tests | `ProdajaPrePostNivelacijePage` "keeps special-character vendor names collision-safe" — "Otvori puni detalj" missing | Expanded-row reconciliation for positional `row:N` keys ran in a passive effect; a click landing before that effect flushed was wiped. | Reconcile during render (state-adjust pattern). |

## Previously open items

- InventoryPage queue-status flake: two-way URL sync — a stale state→URL write (router transition) committed after typing and the URL→state effect cleared the search. Own URL writes are now recognised as echoes; spec waits for the request carrying the search and asserts the input survives. 0/24 failures under heavy parallel load (other specs timed out at 5 s, this one did not).
- English in UI: reason-code chips (Decision Board, inventory snapshot) → shared Serbian labels; Pilot Readiness product line/sources/refresh hints; "worker panel" → "status osvežavanja"; inventory reasons "SKU" → "artikal".
- Product Decision: KPI counts showed `0` while loading → `—`; labels/caption use `--font-size-label` / `--font-size-md` instead of 11px.
