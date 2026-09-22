# Supplier Decisions audit evidence

- Date: 2026-09-22
- Task ID: SUPPLIER-DECISIONS-AUDIT
- Queue: direct-user-request
- Delivery target: `main`
- Owner: Supplier Decision Hub / Supplier Analytics

## Interpreted outcome and owner

Detailed audit and bounded repair of the Supplier Decision Hub/Scorecard screen, report path, backend contract touchpoints and existing reliability prompts. Frontend trust/explainability copy and a confirmed recommendation-code display bug were repaired. Backend schema/period findings were recorded as executable queue prompts because they require a separate backend contract change.

## What was done

- Confirmed source of truth: `SupplierDecisionHubPage`, `SupplierConsolidatedPage`, `SupplierDecisionReport*`, `SupplierExplainabilitySnapshot`, `supplierDecisionHubApi`, `SupplierDecisionHubEndpoints` and supplier decision SQL projections.
- Fixed recommendation presentation so an allowed `ASSORTMENT_REDUCE` signal renders “Smanjiti nabavku” instead of being treated as “Povećati saradnju”.
- Localized active Supplier Decision Hub, trust header, report and action/empty/degraded copy; preserved internal API/reason identifiers.
- Added/updated focused frontend regression expectations for recommendation-code mapping, Serbian copy and trust/fallback messaging.
- Added queue prompts `RQ368`-`RQ372`; promoted only `RQ368` to `READY` and kept `RQ369`-`RQ372` `WAITING` under the one-READY rule.
- Synchronized `MASTER_ROADMAP.md` with the new current READY truth.

## Confirmed findings not implemented in this bounded repair

- Cache capability checks validate the all-time cache but can select 90d/180d views without equivalent existence/column checks; the all-time projection and endpoint SQL have a `post_signal_coverage` compatibility risk.
- Requested short/custom periods may use wider effective materialized datasets while user-facing KPI/detail/report surfaces can look like the requested period; provenance must remain explicit and recommendation-gated.
- The canonical screen currently loads summary/ranking while the richer details endpoint/client/drawer is not the active full-detail path.
- Backend-supported category/gender/season/revenue/confidence/OOS filters are not all exposed by the canonical consolidated filter bar.
- Remaining report/backend user-facing terminology requires a dedicated, encoding-safe cleanup prompt.

## Files changed

- Supplier frontend components/pages/tests under `Klijent/clientapp/src/components/analytics`, `Klijent/clientapp/src/components/supplierDecisionHub` and `Klijent/clientapp/src/pages`.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- This run log.

## Validation run

- `node scripts/check-prompt-queues.mjs` — PASS; 511 tasks.
- `npm run check:analytics-guardrails` — PASS: encoding, analytics guardrails and TypeScript build.
- Focused frontend tests — PASS: 8 files, 57 tests.
- `npm run build` — PASS; Vite production build completed. Existing chunk-size warning remains.
- `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests|FullyQualifiedName~SupplierNegotiationPackReportTests" --no-restore` — FAILED: 21 passed, 1 failed. Failure is the existing `BuildSupplierDecisionReportResponse_PreservesTrustFreshnessAndStableFilters` assertion at `Api.Tests/SupplierDecisionHubContractTests.cs:345` (`report.Meta.IsPartial` expected true); no backend source file was changed in this task.
- `git diff --check` — PASS.

## Validation not run

- Full frontend suite, full backend suite, live browser/API/database proof and CI — not run; outside the narrow audit proof and no live credentials/database were required.

## Documentation impact

- Queue and roadmap now identify `RQ368` as the current READY prompt and `RQ369`-`RQ372` as WAITING supplier decision follow-ups.
- No queue prompt was claimed or put IN_PROGRESS; this was direct user work plus planning intake.

## Delivery evidence

- Branch at audit start: `main`.
- Main commit SHA and `origin/main` verification: pending final commit/push.
- Untracked `.codex-remote-attachments/` was pre-existing and intentionally untouched.

## Residual risks and next step

- Backend cache schema/provenance compatibility remains unresolved until `RQ368` is claimed and implemented.
- The focused backend contract failure remains open and should be triaged with the RQ368 backend work rather than hidden by changing the test expectation.
- Next owner: execute `RQ368`, then promote one dependent prompt only after its acceptance proof.
