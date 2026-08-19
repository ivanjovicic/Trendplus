Task ID: post-commit-react-csharp-review-3
Queue: none (direct user request)
Date: 2026-08-13
Agent/tool: Cursor
Model: unknown-not-exposed
Delivery target: none
Main commit SHA: uncommitted
Main verification: not run

## What was done
- Reviewed the inventory handler hotfix in `e3933c0` (`fix(inventory): harden signal counts and queue follow-ups`).
- Confirmed the handler reads `total_matching_count` while the reader is still on a row.
- Hardened `InventorySnapshotContractTests` with an EOF-strict reader double so a revert to post-EOF `GetInt64` fails the test.
- Added an empty-forecast case that proves matching count stays `0` without post-EOF access.

## Files changed
- Api.Tests/InventorySnapshotContractTests.cs

## Validation run
- dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~InventorySnapshotContractTests -> pass (5/5)
- git diff --check -> not run on this file only after the test pass; CRLF warnings exist elsewhere in the merge worktree

## Validation not run
- npm run test / check:analytics-guardrails / npm run build -> not run - no React change in this pass
- full dotnet test -> not run - focused inventory snapshot filter matched the change
- queue/planning validators -> not run - did not edit live queue files in this pass

## What was missed
- Earlier Decision Board / Access Import fixes were already committed in `089275e` before this inventory lock-in pass.
- Did not claim or close `RQ99`; this is a same-owner lock-in of the already-landed handler hotfix.

## Risks
- The inventory snapshot test uses a custom reader double, so future handler changes should keep the EOF boundary behavior in mind.

## Next
- Commit the inventory test lock-in and push the branch.
