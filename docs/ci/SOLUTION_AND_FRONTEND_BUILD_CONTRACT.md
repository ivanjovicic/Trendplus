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
- Visual Studio GUI “open solution” clicks are optional when the JavaScript SDK project system is exercised by an equivalent VS/`dotnet` build of both `.esproj` wrappers without an unavailable-SDK error.

## Windows / Visual Studio verification (BCI06, 2026-08-11)

Observed on this Windows agent:

| Check | Result |
|---|---|
| OS | Windows 10.0.22000 (win-x64) |
| Visual Studio | Community 2026 (`18.2.11415.280`) at `C:\Program Files\Microsoft Visual Studio\18\Community` |
| Required workloads / components | `Microsoft.VisualStudio.Workload.NetWeb`, `Microsoft.VisualStudio.Workload.Node`, `Microsoft.VisualStudio.ComponentGroup.WebToolsExtensions.JavaScript` |
| `node scripts/check-javascript-sdk-pins.mjs` | pass (`1.0.3982316`) |
| `dotnet restore/build Trendplus2.Backend.slnf` | pass (canonical non-IDE / Linux path) |
| `dotnet restore Trendplus2.sln --force` | pass |
| `dotnet build Klijent/Klijent.esproj` | pass (JavaScript SDK project system; 0 errors) |
| `dotnet build Trendplus.POS.Ui/Trendplus.POS.Ui.esproj` | pass (JavaScript SDK project system; 0 errors) |
| `cd Klijent/clientapp && npm run build` | pass (independent React gate) |
| `cd Trendplus.POS.Ui && npm run build` | pass (independent POS gate) |

Support boundary (historical 2026-08-11 snapshot; 2026-08-13 corrected the vswhere IDs below):

- Mixed-solution IDE wrappers are supported on Windows when Visual Studio Community (or equivalent) can load the JavaScript SDK project system and the NuGet pin resolves.
- Linux / GitHub Actions backend CI must continue using `Trendplus2.Backend.slnf` or `Api.Tests/Api.Tests.csproj` and must not depend on `.esproj` availability.
- Interactive Visual Studio GUI load was not required for this proof; `dotnet build` of each `.esproj` is the documented equivalent that loads the JavaScript SDK targets.

## Windows / Visual Studio verification (BCI06, 2026-08-13)

Re-observed on Windows 10.0.22000 / VS Community 2026 `18.2.11415.280` against `ed0d752` (backend-equivalent to green GHA `f1f5a17`):

| Check | Result |
|---|---|
| `node scripts/check-javascript-sdk-pins.mjs` | pass (`1.0.3982316`) |
| `dotnet restore/build Trendplus2.Backend.slnf` | pass (0 errors; canonical non-IDE / Linux path) |
| `dotnet restore Trendplus2.sln --force` | pass; both `.esproj` projects were evaluated (`_GenerateProjectRestoreGraph`) with no unavailable-SDK error |
| `dotnet build Klijent/Klijent.esproj` | pass; loaded `Microsoft.VisualStudio.JavaScript.Sdk/1.0.3982316` and ran `tsc -b && vite build` (0 errors) |
| `dotnet build Trendplus.POS.Ui/Trendplus.POS.Ui.esproj` | pass (0 errors; invoked POS `npm run build`) |
| `cd Trendplus.POS.Ui && npm run build` | pass (independent POS gate) |
| `vswhere -requires Microsoft.VisualStudio.Workload.NetWeb` | pass |
| `vswhere -requires Microsoft.VisualStudio.Workload.Node` | no match on this install |
| `vswhere -requires Microsoft.VisualStudio.ComponentGroup.WebToolsExtensions.JavaScript` | no match on this install |
| VS MSBuild `/t:Restore` on `Klijent.esproj` | hung after "no packages to restore"; not used as closing proof |

Observed VS packages that actually provide the JavaScript project system on this machine:

- `Microsoft.VisualStudio.Workload.NetWeb`
- `Microsoft.VisualStudio.JavaScript.ProjectSystem`
- `Microsoft.VisualStudio.Component.JavaScript.TypeScript`
- `Microsoft.VisualStudio.Package.NodeJs`
- VS-bundled `Microsoft.VisualStudio.JavaScript.SDK_1.0.3864779` (historical unavailable pin; restore/build uses the NuGet pin `1.0.3982316` instead)

Current support boundary:

- Mixed-solution Windows support is claimed for VS Community 2026 with NetWeb plus the JavaScript project-system packages above, and only while the `.esproj` NuGet pin exists on nuget.org.
- `Workload.Node` and `ComponentGroup.WebToolsExtensions.JavaScript` are not required vswhere IDs on this verified install.
- Linux / GitHub Actions backend CI must continue using `Trendplus2.Backend.slnf` or `Api.Tests/Api.Tests.csproj`.
- Interactive Visual Studio GUI "open solution" was not used; `dotnet build` of both `.esproj` wrappers is the documented JavaScript SDK project-system proof.
