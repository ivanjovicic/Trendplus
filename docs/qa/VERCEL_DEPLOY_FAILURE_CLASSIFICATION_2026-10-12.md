# Vercel production deploy failures — classification (RQ633)

Date: 2026-10-12  
Repo: `ivanjovicic/Trendplus`  
Agent evidence base: `docs/qa/AGENT_PROMPT_LOG_AUDIT_2026-10-12.md` (F6), git history, `scripts/vercel-should-build.sh`.

Vercel deployment log lines were **not** retrieved in this run (`npx vercel inspect … --logs` requires project credentials; the cloud agent environment has none). Classifications below use commit diffs and deployment metadata from the audit.

## Authoritative config

- **Canonical:** repository root `vercel.json` (install/build/output, headers, rewrites, `ignoreCommand`).
- **Secondary:** `Klijent/clientapp/vercel.json` duplicates headers/rewrites only for the historical pattern where the Vercel project root was `Klijent/clientapp`. It is not used when the Vercel project root is the repo root. Do not edit both for routing changes; change root `vercel.json` first.

## Deployments

| Commit | Deployment id | Touches `Klijent/clientapp` or root `vercel.json`? | Classification | Evidence |
| --- | --- | --- | --- | --- |
| `f35e5656` | `dpl_HTdAhWqwZDDBRu6QxrnVQxgocGGT` | No (queue docs + `MASTER_ROADMAP` only) | **Spurious production build** — docs-only push should not have triggered a frontend deploy before `ignoreCommand`. Failure is not explained by a frontend diff; treat as deploy hygiene / possible transient or concurrency until logs confirm. | `git show f35e5656 --stat`; audit F6 |
| `3a78e062` | `dpl_GWMNgzfNTGe5338RVEPQJH39C7m6` | No (backend migration bootstrap + tests + queue docs) | **Spurious production build** — same as above; no frontend surface change. Not a classified frontend build break from source diff. | `git show 3a78e062 --stat`; audit F6 |

## Mitigation delivered

Root `vercel.json` now sets:

```json
"ignoreCommand": "bash scripts/vercel-should-build.sh"
```

The script exits **0** (skip build) when `vercel.json` and `Klijent/clientapp` are unchanged between `VERCEL_GIT_PREVIOUS_SHA` and `VERCEL_GIT_COMMIT_SHA`; otherwise exit **1** (build).

Self-test: `bash scripts/vercel-should-build-selftest.sh`.

## Owner follow-up (optional)

Run for line-level log proof:

```bash
npx vercel inspect dpl_HTdAhWqwZDDBRu6QxrnVQxgocGGT --logs
npx vercel inspect dpl_GWMNgzfNTGe5338RVEPQJH39C7m6 --logs
```

If logs show an npm/build error unrelated to “no frontend change”, append the finding here without reopening the ignore-command fix.
