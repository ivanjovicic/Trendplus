# Production edge contract (STAB05)

Date: 2026-08-05  
Scope: public health/diagnostics, HSTS behind reverse proxy, Swagger exposure, CORS origins

## Public surfaces (anonymous, minimal)

| Route | Contract |
|---|---|
| `GET /health` | Liveness: `status`, `provider`, `ready`, `timestampUtc`. No dependency error text. |
| `GET /ready` | Readiness: safe status/retry metadata (`status`, `ready`, `db.ok`, `db.latencyMs`, `reason`, retry headers). No connection strings or exception messages. |
| `GET /api/runtime/version` | Build identity: service, environment, commit SHA, build time, process type, provider. |
| `GET /health/dependencies` | Dependency probe: `ok` / latency / **stable error codes only** (`missing_connection_string`, `timeout`, `request_aborted`, `unavailable`). Full exceptions stay in server logs with `CorrelationId`. |

## Middleware / config

| Concern | Rule |
|---|---|
| HSTS | Enabled when environment is **not** Development (`ProductionEdgePolicy.ShouldUseHsts`). Relies on `UseForwardedHeaders` + `X-Forwarded-Proto`. ASP.NET still excludes `localhost`/`127.0.0.1` by default (safe for local HTTP). No `UseHttpsRedirection` (avoids proxy redirect loops). |
| Swagger | Default: Development only. Override with `Swagger:Enabled`. Production appsettings sets `false`. |
| CORS | Single source: `Cors:AllowedOrigins`. Shared by `AllowFrontend` policy and health/ready CORS helper. Production **must** configure at least one origin (startup fails otherwise). |
| Forwarded headers | `X-Forwarded-For` + `X-Forwarded-Proto`; known proxies/networks cleared for platform proxies (Render). |

## Configuration keys

```json
{
  "Cors": { "AllowedOrigins": [ "https://trendplus.vercel.app" ] },
  "Swagger": { "Enabled": false }
}
```

## Out of scope (follow-ups)

- Admin-only detailed dependency diagnostics
- Identity-provider authentication
- Changing `/health` / `/ready` live-smoke field names
