# Dependency and Build Warning Cleanup Evidence

Date: 2026-09-01
Queue: direct-user-request
Status: implemented locally; deployment not performed

## Scope

- Remove the duplicate `Testcontainers.MsSql` reference from `Api.Tests`.
- Resolve the 24 npm audit findings across both frontend projects without leaving a known vulnerability.
- Keep the analytics test suite and production builds working after the dependency upgrades.
- Fix the Product Decision Center empty-state path so it never renders KPI zeroes for insufficient data.

## Changes

- Removed the duplicate package reference from `Api.Tests/Api.Tests.csproj`.
- Updated the main client dependency lockfile through safe audit updates and upgraded `msw` 1.x to 2.x, `puppeteer` 24.x to 25.x and `vitest` 1.x to 4.x.
- Added `Klijent/clientapp/src/mocks/mswCompat.ts` so existing legacy-style test handlers run on MSW 2 without production API changes.
- Removed duplicate `meta` keys from the supplier test fixture.
- Gated the duplicate Product Decision Center KPI section with the existing trust/empty-state guard.

## Validation

- `npm audit --audit-level=low --json` in `Klijent/clientapp`: 0 vulnerabilities.
- `npm audit --audit-level=low --json` in `Trendplus.POS.Ui`: 0 vulnerabilities.
- Main client typecheck: passed.
- Main client Vitest: 101 files, 451 tests passed.
- Product Decision Center focused test: 3 tests passed.
- POS UI production build: passed.
- `dotnet build .\\Trendplus2.sln --no-restore`: 0 warnings, 0 errors.
- Focused .NET archive policy test: 5 passed.

## Residual warnings

- Vite reports one informational bundle-size warning because the `recharts` chunk is approximately 548 kB. This is a performance/code-splitting follow-up, not a security finding; the warning was not hidden by increasing the warning threshold.
- Repository-wide ESLint still has an older unrelated backlog of errors/warnings; it is not part of the .NET solution build or npm audit result.
