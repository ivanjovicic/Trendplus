# RQ672 evidence

- Audit report: docs/qa/ACCESS_IMPORT_AUDIT_2026-10-14.md (base 292b6957). No import run, no DB writes.
- Root cause: runningBatchId set only by the tab that started the run; no mount-time batch load; admin-only history with the key held in memory; batch request dedupe ignored the admin key.
- Fix: mount-time load and attach to running/pending batch (status, step, table, percent, server-based elapsed, cancel); 'Proveri status importa' prompt; 15-min stale-heartbeat warning; per-key in-flight dedupe; Serbian cancel label.
- Also unblocked main breakages from other commits: AnalyticsDashboard emptyVariant typing + guardrail baseline re-anchor (117f2503), MetricMethodologyPanel --text-contrast fallback (0b29d159).
- Local: test:analytics 163 files / 1233 tests; typecheck, build, guardrails, ui-program-gates, bundle budget pass.
- PR #161 CI on head 8f0989e0: backend 38629637640, frontend 38629637233, governance 38629637630 pass.
- Merge (--merge): d75f6c1b3fdba1ce33a66942bd1acbc7fd1528ae
