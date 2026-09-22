Task ID: Q83
Queue: docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md
Date: 2026-09-22
Agent/tool: Cursor Cloud Agent
Delivery target: main
Working branch / PR: cursor/prepost-revenue-contract-52eb / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Verified the reported correlation against `Api/Endpoints/AllEndpoints.cs`.
- Confirmed the exact response is emitted when the live `vw_vendor_sales_nivelacija` relation does not expose `change_percent_revenue_semantic`.
- Confirmed repository SQL `014_CreateVendorSalesNivelacijaViews.sql` already declares the revenue-baseline columns and the endpoint fails closed instead of substituting quantity change.
- Added the production reproduction to Q83 and linked RQ387 to Q83 as the SQL/view prerequisite owner.

## Files changed
- docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- .ai/runs/2026-09-22-q83-prepost-revenue-contract-evidence.md

## Validation run
- `git diff --check` -> pending before delivery
- `node scripts/check-prompt-queues.mjs` -> pending before delivery
- Static source/view inspection -> pass: endpoint guard, SQL column declaration and existing schema assertions agree

## Validation not run
- Live PostgreSQL relation/migration verification -> not run; live database access is not available in this workspace.
- Backend test/build -> not run; the .NET SDK is unavailable in this environment.

## Documentation impact
- Q83 remains `PARTIAL` because the repository contract exists but live view/migration application is unverified.
- RQ387 now explicitly preserves Q83 ownership and safe error behavior.

## What was missed
- No runtime database repair was performed; applying the view/migration requires authorized live database access.

## Risks
- The deployed analytics database may continue returning this error until `014`/the dependent view chain is applied and verified.

## Next
- Apply/recreate the analytics view chain on the live database, verify the required column through `information_schema`, then rerun the Pre/Post request with the recorded correlation path.
