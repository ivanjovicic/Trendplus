# Backend CI mixed-solution Windows evidence (BCI06)

Date: 2026-08-13
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI06`
Agent: Cursor
HEAD: `ed0d752b3893acbf20d2ef2fc41a2c7ded181dc0` (backend-equivalent to green GHA `f1f5a17`)

## Environment

- OS: Windows 10.0.22000 win-x64
- .NET SDK: 10.0.201 (MSBuild 18.3)
- Visual Studio Community 2026 `18.2.11415.280`
  - path: `C:\Program Files\Microsoft Visual Studio\18\Community`
  - `vswhere -requires Microsoft.VisualStudio.Workload.NetWeb`: present
  - `vswhere -requires Microsoft.VisualStudio.Workload.Node`: not present
  - `vswhere -requires Microsoft.VisualStudio.ComponentGroup.WebToolsExtensions.JavaScript`: not present
  - observed JavaScript project-system packages: `Microsoft.VisualStudio.JavaScript.ProjectSystem`, `Microsoft.VisualStudio.Component.JavaScript.TypeScript`, `Microsoft.VisualStudio.Package.NodeJs`
  - VS still ships bundled `Microsoft.VisualStudio.JavaScript.SDK_1.0.3864779`; restore/build used NuGet `1.0.3982316`

## Commands and results

| Command | Result |
|---|---|
| `node scripts/check-javascript-sdk-pins.mjs` | pass |
| `dotnet restore Trendplus2.Backend.slnf` | pass |
| `dotnet build Trendplus2.Backend.slnf --no-restore --configuration Release` | pass (0 errors) |
| `dotnet restore Trendplus2.sln --force` | pass; both `.esproj` evaluated, no unavailable-SDK error |
| `dotnet build Klijent/Klijent.esproj --configuration Release` | pass (JS SDK `1.0.3982316`; invoked `tsc -b && vite build`; 0 errors) |
| `dotnet build Trendplus.POS.Ui/Trendplus.POS.Ui.esproj --configuration Release` | pass (0 errors) |
| `cd Trendplus.POS.Ui && npm run build` | pass |
| VS MSBuild `/t:Restore` on `Klijent.esproj` | hung after "no packages to restore"; killed after ~4 minutes; not used as proof |

A later `cd Klijent/clientapp && npm run build` failed with missing `reasonCodes` on `BoardCard`. That tree was dirty from a parallel DEX14 agent (`DEX14-codex.lock.md` plus Decision Board files). It is not HEAD proof and was not repaired in this BCI06 session. The React production script already passed through the `.esproj` build on a clean HEAD.

## Decision

- Mixed-solution Windows support is **observed**, not inferred only from NuGet pin availability.
- Canonical Linux/agent backend path remains `Trendplus2.Backend.slnf`.
- Required vswhere IDs from the 2026-08-11 note are corrected: `Workload.Node` and `ComponentGroup.WebToolsExtensions.JavaScript` are not installed here and are not required for the observed `.esproj` builds.
- Interactive `devenv` GUI open was not used; `dotnet build` of both `.esproj` wrappers is the documented JavaScript SDK project-system proof.
