# BCI03 evidence log

Prompt: BCI03 - Repair or explicitly isolate unavailable JavaScript SDK pins from whole-solution builds
Date: 2026-08-06
Repo: Trendplus2

Selected model: explicit backend solution filter (`Trendplus2.Backend.slnf`) + available IDE SDK pins

Changed files:
- `Trendplus2.Backend.slnf`
- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`
- `scripts/check-javascript-sdk-pins.mjs`
- `docs/ci/SOLUTION_AND_FRONTEND_BUILD_CONTRACT.md`
- `docs/ci/ANALYTICS_CI_GATES.md`
- `.github/workflows/analytics-quality-gates.yml`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `.ai/task-locks/BCI03-cursor.lock.md`

Validation:
- `node scripts/check-javascript-sdk-pins.mjs` - pass
- negative unavailable pin (`1.0.3864779`) - fail as expected
- `dotnet restore Trendplus2.Backend.slnf` - pass
- `dotnet build Trendplus2.Backend.slnf --no-restore --configuration Release` - pass
- `dotnet restore Trendplus2.sln --force` - pass
- `cd Trendplus.POS.Ui && npm run build` - pass
- `git diff --check` - pass

Checks not run:
- Visual Studio IDE open of mixed solution
- full primary React rebuild in this pass

Next:
- none in backend CI repair queue
