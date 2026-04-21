# Deploy backend to Fly

Short checklist and steps to move backend deployment from Render to Fly.

1) Add GitHub secret

- Create an API token on https://fly.io/account/personal-access-tokens
- In the GitHub repo, add secret `FLY_API_TOKEN` with the token value

2) Confirm `fly.toml` and `Dockerfile`

- Ensure `fly.toml` app name (`app = "trendplus"`) matches the target Fly app.
- Ensure `Dockerfile` builds the backend image from the repo root.

3) Automatic deploy

- The repository contains a GitHub Actions workflow `.github/workflows/fly-deploy.yml` that runs on push to `main` and calls `flyctl deploy --remote-only` using `FLY_API_TOKEN`.

4) Manual deploy (optional)

Run locally if you prefer to test before enabling the secret/CI:

```powershell
flyctl auth login
flyctl deploy --config fly.toml --app trendplus --remote-only
```

5) Disable Render backend

- In the Render dashboard, stop or disable the backend service to avoid double-deploys.

6) Verify

- After GitHub Actions runs, check Fly dashboard and logs for successful startup and the expected `DeferredStartupTasksHostedService` warmup logs.

7) Frontend configuration

- If your frontend is built and deployed separately (e.g., Render, Vercel, Netlify), set the frontend build environment variable `VITE_API_BASE_URL` to your Fly app URL, for example `https://trendplus.fly.dev`.
- If the frontend is served from the backend (static files in `Api/wwwroot`), ensure the production build step uses `VITE_API_BASE_URL` set to the Fly URL before building the static bundle.
- Alternatively, keep the frontend's current public domain and point its DNS (CNAME/ALIAS) at Fly so no frontend changes are needed.

8) Cleanup on Render

- After Fly is verified and receiving traffic, disable or delete the backend service in Render to avoid duplicate services and unexpected costs.

