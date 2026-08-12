# PERF08 Evidence

- Date: 2026-08-12
- Prompt: PERF08 — Capture backend and frontend cold-start evidence (PERF-8)
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF08-raw.json`
- Harness: `tmp/perf08_measure.ps1`, `Klijent/clientapp/scripts/perf08_frontend_render.mjs`

## Method

Cold/warm state is explicit and paths are measured separately:

| Path | Process state | Cache state | Marker |
|---|---|---|---|
| Backend cold | fresh `dotnet run` per sample | cold API + cold bootstrap cache | health ready → first bootstrap HTTP 200 |
| Backend warm | single sustained API process | outer cache warm after first bootstrap | second bootstrap ≪ first |
| Frontend cold | fresh Vite dev server (`5174`, proxy to API) per sample | cold shell + warm API | Puppeteer useful render after bootstrap 200 |

Controls: `AnalyticsPrewarm__Enabled=false`, `Workers__Enabled=false`, Development env, period `2026-02-13` → `2026-08-12`.

## Backend cold-start (5 samples)

| Metric | p50 | p95 |
|---|---:|---:|
| Health ready | 8,310 ms | — |
| First bootstrap request | ~7,600 ms | — |
| **First useful analytics** (health + bootstrap) | **15,992 ms** | **18,937 ms** |

All samples: HTTP `200`. Response meta fields were not parsed by the harness (`success`/`isPartial` remain null in raw JSON).

## Backend warm marker (1 pair)

- First bootstrap after cold start: **7,358 ms**
- Second bootstrap (same process): **75 ms**
- Confirms cold-start cost is dominated by first outer-cache miss, not steady-state latency.

## Frontend cold-start (3 samples, warm API)

Mode: **vite-dev-proxy** (`http://127.0.0.1:5174` → proxied `/api`).

| Metric | p50 | p95 |
|---|---:|---:|
| Vite dev ready | 3,240 ms | — |
| Bootstrap 200 observed | 2/3 samples | — |
| **Useful render** (successful samples) | **8,538 ms** | — |

Successful samples (1–2): bootstrap `200`, useful render **8.5–5.0 s** after navigation.

Sample 3 outlier: bootstrap never returned `200` within 120 s (likely transient API/process contention after rapid restarts); recorded as timeout, not masked.

Vite **preview** on `4173` was abandoned: direct API URL caused CORS and 120 s render timeouts with `bootstrapStatus: null`.

## Interpretation

1. **Backend cold-start is the primary bottleneck** on M-tier: ~16 s p50 to first useful analytics vs ~5–8.5 s frontend useful render when bootstrap succeeds.
2. **Warm path is fast** (~75 ms second bootstrap) — optimization must target cold outer-cache miss and process startup, not steady-state.
3. **Frontend shell startup** (~3 s Vite dev ready) is material but secondary to backend bootstrap wait inside the browser.
4. PERF07 section profiling (~7.2 s bootstrap wall on a single cold sample) aligns with backend first-bootstrap timings here; health/startup adds ~8 s before first request completes.

## Gaps / residual risk

- Harness does not yet assert bootstrap `meta.success` / `isPartial` (PascalCase/camelCase parse TODO).
- Frontend sample 3 instability under rapid cold restarts — consider longer API settle or retry policy in future packs.
- Evidence uses **dev** Vite + proxy, not production preview/build artifact; prod cold-start may differ.
- No optimization, cache TTL, or index semantics were changed in this prompt.

## Files

- `tmp/perf08_measure.ps1`
- `Klijent/clientapp/scripts/perf08_frontend_render.mjs`
- `.ai/runs/2026-08-12-PERF08-raw.json`
