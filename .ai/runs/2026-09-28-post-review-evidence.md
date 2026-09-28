# 2026-09-28 post-review of today's agent deliveries

- Reviewed current `main` after RQ461, RQ462, RQ466, RQ486, RQ484 and RQ483 delivery.
- Reviewed today's run logs, current queue/roadmap state, open PRs and remote branches.
- Open PRs: none.
- Remote branches: `main` plus `backup/mixed-local-changes-20260312-1845`; the backup branch is 2460 commits behind and one commit ahead of current main, so it is intentionally not merged as a safe change.
- Confirmed Planning Governance run `36443764487` for RQ483 queue/evidence close completed successfully.
- Evidence defect found: the RQ483 run log still says the governance runs were in progress even though the later run is green. Historical text is left intact; this review records the final observed state instead of rewriting the original run chronology.
- Functional defect found: RQ466 added backend `refresh=true` cache bypass for the durable Pilot Intake report, while the RQ462 page's "Ponovo generiši report" action only repeated the cached request.
- Repair: `getPilotIntakeDurableReport` now accepts `refresh`; the page sends `refresh=true` after an explicit regenerate/retry action; focused page regression coverage asserts first-load `refresh=false` and user regeneration `refresh=true`.
- RQ467 remains READY and is intentionally not bundled into this review fix because it changes readiness/population business semantics (default-period anchor, DUG/KOREKCIJA policy, cost fallback and blocked-count definition) rather than repairing a local regression.
- No production database mutation or schema change was made.
- Validation available in this connector-only review: static source/diff inspection and the added focused regression specification. Runtime test execution was not available in this chat execution environment.

## CI follow-up

- Analytics Quality Gates run `36448814105` exposed two pre-existing brittle frontend tests while validating the post-review fix: `AnalyticsDetails.periodState.spec.tsx` depended on the wall-clock 30-day default (on 2026-09-28 its hard-coded 2026-08-30 value was not a state change), and `SupplierDecisionReport.spec.tsx` assumed a UTC+2 runner timezone.
- Both were repaired as test-contract fixes only: the period test now uses a guaranteed changed valid date before reversing it, and Supplier report timestamp assertions use the shared environment-aware formatter instead of hard-coded UTC+2 text.
- The Pilot Intake regenerate regression itself was not among the failures.

## RQ467 post-review

- A concurrent RQ467 delivery landed while this review was in progress and was reviewed before close-out.
- Found a provenance mismatch in the new default-period fallback: for `dataScope=existing/all`, the code reused the scope-filtered `includedHeaders` query but labelled the result `import_business_date_fallback` and told the UI it came from the imported dataset.
- Repaired the fallback to query the actual included `DataOrigin=access` retail population before emitting the import fallback code/message.
- Added `PilotIntakeDefaultPeriodAnchorTests` proving an existing-scope request with no scoped sales anchors to the imported business date rather than an unrelated existing-scope date.

## Backend CI review

- Full backend run `36449412559` exposed 28 failures. Comparing it with earlier runs `36442045065` and `36443560294` showed that most failures predated RQ467; the review did not rewrite broad integration behavior just to turn that historical baseline green.
- Three safe, directly evidenced repairs were made:
  - Data-source discovery endpoint parameters are now explicitly `[FromServices]`, fixing ASP.NET minimal-API binding that had inferred `NamedSourceDiscoveryService` as a request body even on GET routes.
  - The missing-cost top-offender contract test now asserts the RQ467 article master fallback `NabavnaCenaDin -> NabavnaCena` instead of the obsolete single-field predicate; the service comment was corrected to distinguish top-offender master-cost semantics from revenue-window sale-line override semantics.
  - Demo endpoint tests suppress only EF Core's process-wide `ManyServiceProvidersCreatedWarning` for their isolated InMemory test hosts, preventing unrelated test-count/order from turning that warning into a cascade of test failures.
- The remaining older backend integration failures are left as separate baseline work unless the newest CI proves one is caused by today's reviewed changes.
