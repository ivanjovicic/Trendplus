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
