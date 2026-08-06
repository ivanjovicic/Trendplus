# Solution and frontend build contract

Date: 2026-08-06
Repo: `ivanjovicic/Trendplus`
Related: BCI01 / BCI03

## Supported model

Canonical non-IDE / Linux / agent restore-build path is the **backend solution filter**:

- `Trendplus2.Backend.slnf`

It includes only C# projects from `Trendplus2.sln` and excludes Visual Studio JavaScript wrappers (`.esproj`).

Frontend quality remains a separate npm/Vite gate. `Api/Api.csproj` keeps React auto-build disabled (`BuildReactApp` condition `false`).

## Canonical commands

### Backend (canonical)

```powershell
dotnet restore Trendplus2.Backend.slnf
dotnet build Trendplus2.Backend.slnf --no-restore --configuration Release
dotnet restore Api.Tests/Api.Tests.csproj
dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release
```

Backend CI (`.github/workflows/analytics-tests.yml`) continues to use `Api.Tests/Api.Tests.csproj` as the restore/build/test root so it never evaluates `.esproj` wrappers.

### Primary React frontend (`Klijent/clientapp`)

```powershell
cd Klijent/clientapp
npm ci
npm run check:analytics-guardrails
npm run test -- --run
npm run build
```

### POS frontend (`Trendplus.POS.Ui`)

```powershell
cd Trendplus.POS.Ui
npm ci
npm run build
```

### Optional mixed solution / IDE wrappers

`Trendplus2.sln` still contains:

- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`

These wrappers pin `Microsoft.VisualStudio.JavaScript.Sdk`. They are for Visual Studio SPA project integration, not for Linux CI or agent backend work.

```powershell
dotnet restore Trendplus2.sln
```

Only use this when intentionally validating IDE wrappers. Do not treat whole-solution restore success as a substitute for the npm frontend gates above.

## JavaScript SDK pin policy

1. Every `.esproj` pin must exist on nuget.org.
2. Regression check:

```powershell
node scripts/check-javascript-sdk-pins.mjs
```

3. Current pin: `Microsoft.VisualStudio.JavaScript.Sdk/1.0.3982316` (verified present on nuget.org flat container; replaces unavailable `1.0.3864779`).

## Unsupported / out of scope claims

- Passing `dotnet restore Trendplus2.sln` does not prove React or POS UI production readiness.
- Visual Studio IDE load of the mixed solution is best-effort and may require a local VS JavaScript workload even when NuGet can resolve the SDK package.
