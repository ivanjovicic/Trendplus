# Evidence — UX/UI audit review and corrections 2026-10-04

Task: review the UX/UI audit originally delivered in `61992efb2432e53d935a38a2f7a756539393a5fe`, correct unsupported/over-broad claims, reconcile it with the same-day responsive audit, and register any concrete missing owner.

Delivery target: `main`
Review base before this evidence commit: `99001e16e92ac2bf1514ad762a2fc5a20d58d257`
Runtime code changed: no
Deployment triggered intentionally: no

## Original audit verdict

The audit is materially useful and its overall **4/10** UX score remains a reasonable critical score. The majority of findings have concrete live, live-API or source-level evidence. The review did not invalidate the major freshness, hierarchy, Product Decision, theme/contrast, state-taxonomy or responsive findings.

## Corrections made

1. **UX-002 / operational controls**
   - Original wording overclaimed that every user could one-click shut down API/workers/Redis.
   - Current code/security evidence distinguishes the operations:
     - worker enable/disable and Redis toggle are backend writes protected by `AdminAccessControl` / `X-Admin-Key`;
     - `ApiPingFlag` toggles the SPA's local periodic ping in the current browser and does not stop the API service.
   - The P1 UX/action-safety finding remains valid: these controls are globally placed, semantically conflated and backend writes lack a confirmation at the UI surface.
   - `P-UI-48` now preserves server authorization, moves backend writes to admin/observability with confirmation, and treats API ping as a clearly labelled browser-local preference.

2. **Trust-header ownership**
   - UX-003 and UX-004 incorrectly named P-UI-50 as the shared owner.
   - Shared evidence disclosure, copy, height/fold budget and one-H1 behavior belong to `P-UI-43` after `RQ569`.
   - `P-UI-50` remains bounded to Product Decision information hierarchy/KPI/disclosure work.

3. **Missing cross-cutting chart accessibility owner**
   - Repo-wide inspection found no shared Recharts accessibility contract: no Recharts `accessibilityLayer` use and no common accessible-name + text-summary/table-alternative pattern.
   - Data Quality's custom SVG is an isolated positive example (`role="img"` + `aria-label`).
   - Added code-derived **UX-051** and **P-UI-53** for chart accessibility without frontend business-metric invention.
   - P-UI-53 waits for P-UI-47 plus chart-heavy P-UI-31/35/36 completion or explicit deferral; P-UI-38 consumes it as the final regression gate.

4. **Design-system corrections**
   - Touch sizing is capability-based (coarse pointer / hybrid touch), not phone-width-only.
   - Chart accessibility now explicitly requires an accessible name plus a concise summary or discoverable equivalent table sourced from the same displayed data.
   - Chromium emulation is not treated as iPhone/iPad Safari certification. Real-device certification requires a manual iPhone Safari + iPad Safari pass; CI remains deterministic and separate.

5. **Evidence/routing synchronization**
   - Original audit run log now records exact main SHA `61992efb2432e53d935a38a2f7a756539393a5fe`, ancestor verification and `Evidence state: synchronized`.
   - Historical P-UI-45 READY routing in that run log is explicitly superseded.
   - Current P-UI READY set: `P-UI-39` primary plus `P-UI-40`, `P-UI-41`, `P-UI-47`, `P-UI-49`.
   - Current RQ primary remains `RQ569`; additional READY lanes remain `RQ553`, `RQ574`, `RQ578`, `RQ580`, `RQ581`, `RQ586`, subject to fresh claim-time collision checks.

## Audit count after review

- Total: **51**
- P0: **3**
- P1: **14**
- P2: **22**
- P3: **12**
- Live-confirmed: **31**
- UX-051: code-derived

## Files reviewed/corrected

- `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`
- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`
- `.ai/runs/2026-10-04-ux-ui-audit-evidence.md`
- `.ai/runs/2026-10-04-responsive-audit-review-corrections-evidence.md`
- `MASTER_ROADMAP.md`
- `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`
- worker/Redis/API-ping frontend controls and the runtime authorization boundary documentation

## Validation scope

This review changed documentation/queue/governance only. No frontend/backend runtime source was changed, so no runtime build/test suite was run in this connector-only pass. The original audit recorded the three governance validators and `git diff --check` as passing at registration time. The post-review consistency check directly fetched the canonical files from current `main` and confirmed the corrected routing/ownership/content rather than relying on GitHub code-search indexing, which lagged behind the commits during the review.

## Next

Global priority remains `RQ569`. For independent P-UI work, `P-UI-47` is the highest-value P1 UX lane; `P-UI-49` is a clean shared-state lane. `RQ586` remains a small repository-local correctness win.
