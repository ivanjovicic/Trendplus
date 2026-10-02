Task ID: analytics-013-42p16
Queue: direct-user-request
Date: 2026-10-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending
Main verification: pending fresh `origin/main` verification after push.
Evidence state: pending

## What was done

- Diagnosed the likely `42P16` as `CREATE OR REPLACE VIEW prodaja_stavke` changing the seventh legacy output column from `data_origin` to `supplier_id_at_sale`; PostgreSQL requires existing view columns to retain their names and order.
- Moved `supplier_id_at_sale` after the existing `data_origin` column, preserving the legacy column ordinal and appending the new compatibility field.
- Extended the PostgreSQL supplier-schema readiness fixture with the pre-existing seven-column legacy view. The initializer now upgrades it idempotently and preserves `data_origin` at ordinal 7 while adding `supplier_id_at_sale` at ordinal 8.

## Files changed

- `Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql`
- `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`
- `.ai/runs/2026-10-02-analytics-013-42p16-evidence.md`

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~SupplierDecisionSchemaReadinessIntegrationTests.SupplierDecisionRepair_SeedsPostgres_RepeatsIdempotently_AndPreservesRefreshPrerequisites --logger "console;verbosity=minimal"` -> pass, 1 passed / 0 failed / 0 skipped, using disposable PostgreSQL/Testcontainers.
- `git diff --check` -> pass.

## Validation not run

- Full backend suite -> not run; the focused PostgreSQL initializer integration test exercises the legacy view upgrade and repeat initialization.
- Production database retry/write -> not run; no production access or mutation was used.

## Documentation impact

- No product documentation changed; the SQL comment documents the compatibility ordering requirement beside the view definition.

## What was missed

- The provided log excerpt omits PostgreSQL `MessageText`/`Position`, so the live database's exact failing statement was not available. The legacy-view collision in the deployed script is reproduced by the targeted fixture and matches the reported SQLSTATE.

## Risks

- If the deployed exception came from a different stale compatibility view in the same SQL file, the added `supplier_id_at_sale` position fix does not cover that separate shape; the current identified view replacement path is now exercised against its prior schema.

## Next

- None for this repository fix. If the deployed startup still reports `42P16`, capture PostgreSQL `MessageText` and `Position` from the full exception to identify any second stale view.
