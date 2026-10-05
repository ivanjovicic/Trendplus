# Analytics business documentation & prompt audit — 2026-10-05

Base: `origin/main` `7265745a`  
Evidence: `.ai/runs/2026-10-05-analytics-business-docs-audit-evidence.md`  
Canonical glossary: `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`

## What was wrong / stale

1. **No single business glossary** for Promet/Prihod, nabavni trošak, DUG/KOREKCIJA, source horizon, Nije dostupno vs 0, inventory age, insufficient signal — definitions were scattered across metric registry, accuracy contracts, owner decisions and UI audits.
2. **Prihod vs Promet drift** risk: `ANALYTICS_STANDARDS` listed only Prihod; Operations UI/PoP often says Promet; registry aliases both to `revenue` without an owner mass-rename.
3. **Test Hardening addendum** still said “current execution is RQ96” though RQ96/RQ106 are DONE — fake live pointer.
4. **OPERATIONS_UNDOCUMENTED_FINDINGS_2026-09-25** still showed RQ442 READY / DUG as open-era findings without a historical banner (live contracts are DONE).
5. **Duplicate typo path** `docs/Analytics/KPI_METHODLOGY_AUDIT.md` vs `KPI_METHODOLOGY_AUDIT.md`.
6. Presentation audit DoD still said “PC FF needed” after main already received those tips.
7. Open PRs: none (fresh `list_pull_requests`).

## What was improved

| Change | Path |
|---|---|
| Canonical business glossary | `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md` |
| Standards + Start Here links | `docs/ai/ANALYTICS_STANDARDS.md`, `docs/ai/AGENT_START_HERE.md` |
| Stale RQ96 executor pointer cleared | `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` |
| Historical routing banner | `docs/qa/OPERATIONS_UNDOCUMENTED_FINDINGS_2026-09-25.md` |
| Typo KPI audit → stub | `docs/Analytics/KPI_METHODLOGY_AUDIT.md` |
| Presentation/UX audit sync + glossary pointer | `docs/qa/ANALYTICS_UI_PRESENTATION_AUDIT_2026-10-05.md`, `docs/qa/ANALYTICS_UI_UX_DESIGN_AUDIT_2026-10-05.md` |
| Roadmap owner note | `MASTER_ROADMAP.md` |
| P-UI-52 progress note (stays READY) | `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` |

No product formulas, queues statuses (except clearing false executor text), or Insight Studio rename (RQ582) were changed.

## Prompts closed / merged / added

| Action | ID | Result |
|---|---|---|
| Closed | — | none (no false READY that was fully implemented) |
| Merged | — | none |
| Added | — | **no new READY prompt**; glossary is documentation |
| Annotated | P-UI-52 | stays READY; N/A sweep partially satisfied by prior UI audits; nav IA remains |

## Fresh queue scan (canonical)

Scanned active RQ queue + addenda, UI premium queue, SQL, Stabilization, Platform, Multitenancy, Test Hardening, NEXT historical.

| Lane | Status |
|---|---|
| **RQ primary** | **RQ588 IN_PROGRESS** (P3 EF migration discovery; owned elsewhere — not claimed) |
| RQ READY | none besides IN_PROGRESS pointer |
| **UI READY** | **P-UI-39** (primary) + P-UI-40, 41, 47, 49, **52** |
| UI WAITING | P-UI-31/35/36/38/42–46/48/50/51/53 (deps) |
| SQL | Current READY none (Q83 DONE); Q69 PARTIAL |
| STAB | STAB16 **BLOCKED** (provider/deploy access) |
| Notable WAITING | RQ585 (prod freshness), RQ454/565/566 (deploy), RQ558/559 (owner/sample), RQ451/452/448 |
| PARTIAL | RQ137/139/140, RQ545, RQ530, Q69 |

Validators: `check-prompt-queues` (709 tasks OK before edits; re-run after).

## Residuals (PO / external)

1. **Owner decision:** unify display label **Prihod** vs **Promet** across all Analytics UI (same metric today).
2. **RQ588** completion by owning agent.
3. **STAB16 / RQ565 / RQ566 / RQ585** need provider/production freshness access.
4. **P-UI-39+** responsive/theme/nav implementation (code), not docs.
5. Insight Studio experimental quarantine (**RQ582**) — do not invent rename.
6. ~~Insight Studio `N/D` helper residual~~ — closed in recertify 2026-10-05 (`insightStudioTrustPresentation` → `ANALYTICS_UNAVAILABLE_LABEL`).

## Definition of done

- [x] Fresh main audited
- [x] Canonical glossary published and linked
- [x] Stale prompt/doc pointers repaired safely
- [x] Queue statuses rescanned (no false “no READY”)
- [x] Validators + `git diff --check`
- [x] Push / HEAD == origin/main (landed as `e44eb1b7`; later tip may advance)


## Recertify 2026-10-05 (quality review)

Falsified against `origin/main` tip after docs audit:
- Trust collapse, sticky table pilot, reset/DQ Serbian labels, glossary links: still true.
- **Incomplete prior presentation claim:** `insightStudioTrustPresentation.ts` still returned `N/D` (RQ591-era) despite pass-2 saying N/D cleared — fixed in this recertify.
- **Missed adjacent surface:** `IntelligenceSnapshotPanel` (Insight Studio) still used `n/a` + English card titles — fixed.
- Evidence SHA fields that said "pending" after FF: updated in evidence run file.
- RQ588 remains IN_PROGRESS elsewhere (not claimed). Queue lock file may be absent — residual for owning agent, not this task.
