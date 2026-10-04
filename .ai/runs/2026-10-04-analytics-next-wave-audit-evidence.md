Task ID: analytics-next-wave-audit-2026-10-04
Queue: direct-user-request (registers RQ587 and RQ588 in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; repairs RQ479 and RQ586)
Date: 2026-10-04
Agent/tool: Grok Bot (executor, box worktree `/workspace/nw`)
Delivery target: main
Working branch / PR: local branch `next-wave-2026-10-04` from `origin/main` `f1437ed84c5c22922f4b70b2174ef05d2d0fa970`; the owner pushes from his PC (the box has no GitHub credentials)
Main commit SHA: `8ae06a508ae8f98163015c869a816e2a3b7a11d3`
Main verification: synchronized; fresh GitHub history after review confirms `8ae06a508ae8f98163015c869a816e2a3b7a11d3` is an ancestor of current `main`.
Evidence state: synchronized

## What was done

- Fresh start on `origin/main` `f1437ed8`. Open PRs: 0 (GitHub API). Remote branches: 28, all owned by DONE prompts. Local locks: none. The original run recorded P-UI-45 as READY; this was superseded by the same-day collision review. Canonical P-UI READY after reconciliation is P-UI-39 primary plus P-UI-40/41/47/49; P-UI-45 waits behind P-UI-40 + P-UI-48. STAB16 BLOCKED. QDB has no READY.
- Verified the section-level status of RQ142 (OBSOLETE), RQ147/RQ148/RQ149 (DONE), RQ150 (OBSOLETE) and RL12 (WAITING); section and table agree.
- Falsified 10 claims (F1–F10). Confirmed F1, F2, F7, F9 and F10. Narrowed F2 (low impact) and F4 (the 42P16 fix is in the runtime). Mostly refuted F6: the "Failed to fetch" errors coincided with the 21:38 CEST redeploy. Refuted the P1 priority of F8 (RQ586 changes no current number).
- New finding: live `/ready=true` while startup-owned analytics objects are missing. Post-review correction: this strongly constrains the startup/schema-convergence problem but does not uniquely prove which setting/path is wrong. AutoMigrate-off, non-strict/FailFast behavior, alternate effective config/connection/runtime path, and post-readiness schema drift remain distinguishable until provider/admin evidence is captured.
- New finding: the Analytics Actions ledger holds only 4 smoke fixtures and 0 real actions. Post-review correction: that blocks Actions-based adoption/outcome learning, but it does not block RQ557 (Nivelacija price-event outcome ledger) or RQ585 (current-signal weekly digest), which use different sources and dependencies.
- Built the original coverage map (32 findings) before registering RQ587 (P1 READY) and RQ588 (P3 READY). Post-review found one additional measured execution-owner gap rather than a new analytics-semantic gap: PERF19 (P1 WAITING after RQ573) for Decision Board composition profiling if the endpoint remains above its existing budget.
- Repaired RQ479: added the missing `Ready after` and paths, narrowed it to a repository-local read guard, and promoted it WAITING -> READY (P1 -> P2). Repaired RQ586: P1 -> P3.
- Original addenda: RQ545, RQ565, RQ573, RQ578, STAB16, P-UI-49. Post-review further tightened RQ545/RQ557/RQ558/RQ573/RQ578/RQ587/RQ588/STAB16, corrected RQ585's dependency narrative and registered PERF19. Added a supersession note to the PROD-AN planning backlog.
- Wrote `docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md` (Serbian, 27 sections).

## Live probes (read-only GET, no auth, 2026-10-04 22:50–22:56 CEST)

| Probe | Result |
|---|---|
| `/api/runtime/version` | 200; commit `02f9915887f99115348bd241590da581dafee45d`; build `2026-10-04T19:38:31Z`; `processType=web`; `provider=render` |
| `/ready` | 200; `ready=true`; `reason=ready`; `startedAtUtc=19:38:36Z`; `readyAtUtc=19:39:00Z` |
| `/health`, `/health/dependencies` | 200 healthy; both databases ok (67 ms / 29 ms) |
| `/api/analytics/refresh-status` | `workersEnabled=false`, `processType=web`, 6 jobs without status, in-memory cache |
| `/api/analytics/cached/validation/freshness` | `critical`; `lastImport=2026-08-12T10:30:04Z`; `freshnessHours=1282.34` |
| `vendor-sales-nivelacija` 2026-07-07..08-06 | `vendor_sales_nivelacija_contract_missing` (`change_percent_revenue_semantic`) |
| `reports/supplier-decision` same period | `MISSING_OBJECT` (90-day dataset) |
| `decision-board` same period | 200 in 17.3 s; `BOARD_PARTIAL`; `critical` |
| `decision-pulse` same period | 200 in 1.9 s; 0 items, 24 suppressed; `PULSE_PARTIAL` ("Product Decision izvor nije dostupan") |
| `actions` | `totalCount=4`, all smoke fixtures; `dataQualityStatus=good` |
| `operations-integrity` | `unverified` |
| `data-quality/health` same period | 100 / excellent; `windowTo=2026-10-04T23:59:59Z` despite `toDate=2026-08-06` |
| `daily-sales` 2026-07-07..08-05 | `shiftTimeZone=UTC`; `shiftTimestampBasis=legacy_access_wall_clock` (307/307); `no_time_fallback` |
| CORS | `Access-Control-Allow-Origin: https://trendplus.vercel.app` on every analytics response |

Raw responses are box-local in `/workspace/nwlive/` (not committed).

## Files changed

- `docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md` (new)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` (header, RQ479, RQ573, RQ578, RQ586, new RQ587/RQ588; mixed line endings preserved, changed and new lines LF)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md` (RQ545)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md` (RQ565)
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (STAB16)
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` (P-UI-49)
- `docs/ai/ANALYTICS_PRODUCTION_VALUE_PROMPT_BACKLOG_2026-08-19.md` (supersession note)
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-04-analytics-next-wave-audit-evidence.md` (this file)

## Validation run

- `node scripts/check-agent-instructions.mjs --self-test` and without the flag: pass
- `node scripts/check-prompt-queues.mjs --self-test` and without the flag: pass
- `node scripts/check-planning-architecture.mjs --self-test` and without the flag: pass
- `git diff --check`: pass
- `git merge-base --is-ancestor 26e09e46 02f99158`: true (the 42P16 fix is deployed)

## Validation not run

- .NET and frontend tests were not run in the original audit because it was docs/queue-only. The post-review also changes only docs/queue/governance; no new runtime test result is claimed.
- Browser rendering: not repeated. UX evidence is reused from `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`.
- Database, provider logs, Render dashboard, admin key: no access.

## Documentation impact

- New QA audit report; queue routing, prompt repairs and addenda; roadmap note.

## What was missed

- Effective Render configuration and exact startup/schema-drift cause remain UNPROVEN-RUNTIME. The retained ready+missing-object evidence is a constraint, not a unique diagnosis. Separate worker existence is also UNPROVEN; process-local web status only proves there is no matching durable success evidence visible to the API.
- Negative-ID repair was not re-probed live (owned by RQ566).

## Risks

- If the owner later changes the effective runtime policy to strict FailFast, the next restart can legitimately remain not-ready until the schema is repaired. Capture current effective config/logs before changing it.
- RQ587 touches readiness handlers in `Api/Program.cs`, a hotspot file.
- Codex works concurrently; the queue was re-fetched before the commit.

## Post-close routing recovery

- Current RQ primary remains RQ569; additional READY lanes remain RQ553, RQ574, RQ578, RQ580, RQ581, RQ586 (P3), RQ587, RQ588 (P3), RQ479 (P2), subject to fresh claim-time collision checks. Canonical P-UI READY is P-UI-39 primary + P-UI-40/P-UI-41/P-UI-47/P-UI-49; P-UI-45 is WAITING. PERF19 is WAITING after RQ573. RQ557 waits for RQ553 shared Pre/Post ownership rather than real Actions data; RQ585 keeps only its certified-signal/freshness prerequisites.

## Next

- Owner/provider wave A: run the approved Access import; capture before changing the effective Render env (`Database__AutoMigrate`, `DatabaseInitialization__FailFast`, `StartupTasks__RunDatabaseInitialization`), worker-service state and startup log/admin diagnostic. Change strictness only after the current state is evidenced.
- Overall: claim RQ569. Highest code value per cost: RQ587. Quick hygiene: RQ479.


## Post-review corrections

A fresh code/queue review after the original `8ae06a50` delivery made the following corrections without changing runtime code:

1. `/ready=true` plus missing analytics objects does not uniquely prove AutoMigrate/FailFast is disabled; RQ587/RQ545/STAB16 now distinguish startup skip, non-strict completion, alternate effective config/connection and post-readiness drift.
2. `refresh-status` first consumes durable `AnalyticsRefreshRuns`; `workersEnabled=false` on the web process does not prove a separate worker service is absent.
3. Data Quality health currently does not declare `fromDate/toDate`; RQ578 now owns a real explicit historical-period contract instead of merely a label correction.
4. Decision Board's 17.3 s observation has no contributor timing. RQ573 measures PDC+Board before/after; PERF19 profiles/optimizes only if the Board still breaches the existing p95 budget.
5. RQ479 uses one shared positive-only fixture predicate across list/count/outcome/Board and does not add an anonymous fixture bypass.
6. RQ557 is a Nivelacija price-event descriptive ledger, not an Analytics Actions learning prompt; it is narrowed to observed outcomes with fail-closed stock/cost evidence.
7. RQ585 does not depend on real Actions outcomes; RQ558/causal runtime work remains later behind measured sample/coverage gates.
8. RQ588 uses EF discovery semantics plus an explicit reviewed legacy allowlist rather than blindly requiring raw attributes/deleting files.
9. Supplier/Shoe Type historical attribution is already materially protected by RQ411 sale-time snapshots/provenance; remaining poorly populated master dimensions do not justify a new SCD runtime project now.
