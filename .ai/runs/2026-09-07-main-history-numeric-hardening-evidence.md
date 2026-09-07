Task ID: main-history-numeric-hardening
Queue: direct-user-request
Date: 2026-09-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/main-history-numeric-hardening-20260907 / local merge, no PR
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

- Reviewed the historical pre-existing boundary not covered by the immediately preceding review: commits `da18187c` (RQ139), `196266cb` (RQ144), `4b47affc` (RQ152), and the related RQ151 action-warning mapping.
- Confirmed that `TrendScoringService.ComputeRecommendedOrderQty` treated negative velocity and invalid inventory/horizon inputs as valid output, while `GroupFinalScore` could accept missing/inconsistent group evidence and propagate non-finite results.
- Updated the same owner-scope to fail closed for missing, inconsistent, negative or overflowing numeric evidence, while preserving legitimate zero values.
- Added regression coverage for invalid inputs, missing group evidence, evidence/count mismatch, valid zero, finite positive score and horizon overflow.

## Files changed

- `Application/Analytics/Services/TrendScoringService.cs`
- `Api.Tests/TrendScoringServiceTests.cs`
- `.ai/runs/2026-09-07-main-history-numeric-hardening-evidence.md`

## Validation run

- `git diff --check` -> pass; Git reported only existing LF/CRLF normalization notices.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TrendScoringServiceTests"` -> first run failed because the newly added valid-zero test used insufficient stock; the test expectation was corrected.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TrendScoringServiceTests"` -> pass, 9/9.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~TrendScoringServiceTests|FullyQualifiedName~PreNivelacijaScoringServiceTests|FullyQualifiedName~AnalyticsDataQualityConsistencyTests|FullyQualifiedName~AnalyticsDataQualityHealthServiceTests"` -> pass, 38/38.
- `dotnet build Api/Api.csproj --no-restore --configuration Release --nologo` -> pass, 0 warnings, 0 errors.

## Validation not run

- Full repository test suite -> not run; the changed owner-scope was covered by focused tests and the API Release build.
- Live database refresh, browser proof, and remote CI result -> not run; they require external/runtime state and were not necessary to prove this local numeric guard.
- Frontend analytics guardrail -> not run; no frontend files were changed.

## Documentation impact

- Added this durable run log as required for direct file-changing work.
- No queue/router or owner documentation changed. RQ139 remains `PARTIAL`; RQ144 and RQ152 remain previously recorded as `DONE`.

## What was missed

- The broader RQ139 parity inventory remains outside this bounded correction, including the legacy `999` days-since-last-sale sentinel in the PreNivelacija surface and live/browser parity proof.
- Waiting or obsolete prompts were not promoted or changed because no confirmed same-owner defect required that scope.

## Risks

- Existing repository-wide analyzer warnings and unrelated legacy numeric fallbacks remain outside this patch.
- Full integration/runtime behavior against live data remains unverified locally.

## Next

- None for this bounded correction. A follow-up owner should complete the remaining RQ139 legacy-surface parity work with its API/UI contract and live proof.
