# Backend CI mixed-solution Windows evidence (BCI06)

Date: 2026-08-11
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI06`
Agent: Cursor

## Environment

- OS: Windows 10.0.22000 win-x64
- .NET SDK: 10.0.201 (MSBuild 18.3)
- Visual Studio Community 2026 `18.2.11415.280`
  - path: `C:\Program Files\Microsoft Visual Studio\18\Community`
  - workloads/components present: NetWeb, Node, WebToolsExtensions.JavaScript

## Commands and results

| Command | Result |
|---|---|
| `node scripts/check-javascript-sdk-pins.mjs` | pass |
| `dotnet restore Trendplus2.Backend.slnf` | pass |
| `dotnet build Trendplus2.Backend.slnf --no-restore --configuration Release` | pass (0 warnings / 0 errors) |
| `dotnet restore Trendplus2.sln --force` | pass |
| `dotnet build Klijent/Klijent.esproj` | pass (JS SDK targets; npm build invoked; 0 errors) |
| `dotnet build Trendplus.POS.Ui/Trendplus.POS.Ui.esproj` | pass (0 errors) |
| `cd Klijent/clientapp && npm run build` | pass |
| `cd Trendplus.POS.Ui && npm run build` | pass |

## Decision

- Mixed-solution Windows support is **observed**, not inferred only from NuGet pin availability.
- Canonical Linux/agent backend path remains `Trendplus2.Backend.slnf`.
- Required VS workloads are documented in `docs/ci/SOLUTION_AND_FRONTEND_BUILD_CONTRACT.md`.
- Interactive `devenv` GUI open was not used; `dotnet build` of both `.esproj` wrappers is the documented equivalent JavaScript SDK project-system proof.
