# Vercel Deploy Triage

Datum: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Task: triage failing Vercel deployment before more analytics work

## Summary

- Vercel failure is confirmed on the repo, but the user-provided short SHA `7fb6e04` could not be resolved from GitHub or `git ls-remote`.
- The same `Vercel: failure` status is present on:
  - `783adbc3858ccaf5ea9edc9f3cdd34b90aaf9f1e` (`origin/main`)
  - `ece794148576a39c691fa9198e7741cf59c302d1` (docs-only branch commit)
- Required local frontend checks pass from `Klijent/clientapp`:
  - `npm run check:analytics-guardrails`
  - `npm run build`
- Because the failure reproduces on a docs-only commit and the app builds locally, the strongest current classification is:
  - `Vercel project root/output config`

## What was verified

### GitHub / Vercel status

- GitHub combined status for `783adbc3858ccaf5ea9edc9f3cdd34b90aaf9f1e` returns:
  - `context: Vercel`
  - `state: failure`
  - target URL under Vercel project `ivans-projects-8c8927b4/trendplus`
- GitHub combined status for `ece794148576a39c691fa9198e7741cf59c302d1` also returns the same single failing `Vercel` status.
- The Vercel deployment page is publicly reachable, but detailed deployment JSON/log data is not public. The HTML includes repeated dashboard fetch failures with:
  - `The request is missing an authentication token`

### Local build posture

- Real frontend app lives under `Klijent/clientapp`.
- The real Vercel config also lives there:
  - `Klijent/clientapp/vercel.json`
- The actual frontend package there has the correct build script:
  - `npm run build` -> `tsc -b && vite build`
- The nested app also contains production env examples and build-time Vite config.

### Repo root mismatch risk

- Repo root has a different `package.json` with only:
  - `glob`
- Repo root has no `vercel.json`.
- If Vercel project root is still the repo root instead of `Klijent/clientapp`, Vercel will not use:
  - `Klijent/clientapp/package.json`
  - `Klijent/clientapp/vercel.json`
  - `Klijent/clientapp/.env.production`
- That would explain why Vercel fails even when frontend code itself still builds locally.

## Classification

Current best classification: `Vercel project root/output config`

Confidence: medium-high

Why this is the best fit:

1. Local frontend build passes.
2. Analytics guardrails pass.
3. Failure also happens on a docs-only branch commit, so it is not tied to a recent analytics code path.
4. The repository has a nested Vite app, while repo root looks incompatible with a normal Vite deploy.

## Not currently supported by evidence

- `TypeScript/build issue`
  - Local build passes.
- `guardrail`
  - Guardrails pass locally.
- `missing env variable`
  - Possible in theory, but less likely than root misconfiguration because the same failure pattern also affects docs-only commits.
- `npm install/dependency issue`
  - No local signal of dependency breakage.
- `external/transient`
  - Less likely because failure is reproducible across multiple commits.

## Recommended Vercel fix steps

Open the Vercel project `trendplus` and verify these settings:

1. `Settings -> General -> Root Directory`
   - Set to `Klijent/clientapp`
2. `Framework Preset`
   - `Vite`
3. `Install Command`
   - `npm ci`
4. `Build Command`
   - `npm run build`
5. `Output Directory`
   - `dist`
6. `Node.js Version`
   - use a current supported version compatible with local CI, ideally `20.x`
7. `Environment Variables`
   - verify values expected by the nested frontend app, based on `Klijent/clientapp/.env.production.example`
   - especially:
     - `VITE_API_BASE_URL`
     - `VITE_API_FALLBACK_URL`
     - `VITE_API_RENDER_BASE_URL`
     - `VITE_API_FLY_BASE_URL`
     - optional scraper/export flags if used in production
8. Trigger a new deployment after saving settings.

## If Vercel root is already correct

If the project is already rooted at `Klijent/clientapp`, the next most likely checks are:

1. Confirm Vercel is using `npm ci`, not a stale custom install command.
2. Confirm Node version is not pinned to an older runtime.
3. Open the authenticated build log and inspect:
   - install phase
   - build command phase
   - output directory validation
4. Compare configured env vars against `Klijent/clientapp/.env.production.example`.

## Conclusion

- No frontend code fix was applied in this pass.
- Available evidence points to Vercel project configuration, not analytics feature code.
- The highest-value next action is to correct or confirm the Vercel project root and deploy commands under the `trendplus` project.
