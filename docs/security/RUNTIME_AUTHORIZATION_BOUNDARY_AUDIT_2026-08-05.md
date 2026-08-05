# Runtime Authorization Boundary Audit — 2026-08-05

Repo: `ivanjovicic/Trendplus`  
Task: `STAB03`  
Inspected at (UTC): `2026-08-05T10:35:00Z`  
Result: **Phase 1 boundary selected = (b) explicit internal API-key admin mode**

This audit proves current production authentication capability. It does **not** invent an identity provider and does **not** implement broad endpoint protection.

Related historical docs:

- `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md` (2026-06-14 inventory; still useful)
- `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
- Follow-up code task: `STAB04` (admin operational reads)

## 1. Runtime identity sources

| Environment | How identity is established today | Can create authenticated `Admin` principal? |
|---|---|---|
| Production API (`Program.cs`) | No `AddAuthentication` / `UseAuthentication` / JWT / cookie / OIDC registration found | **No** |
| Local / Development | Same host pipeline as production code path | **No** |
| Integration tests | Prefer `X-Admin-Key` against configured `Admin:ApiKey` | Key path only; no production-like ClaimsPrincipal login |
| Document “user” context | Defaults + optional spoofable headers (`X-User-Id`, `X-User-Name`, `X-User-Roles`) | **Not** ASP.NET authentication |

Evidence:

- `Api/Program.cs` calls `app.UseAuthorization()` (~line 1177) but has **no** matching authentication registration.
- Repo-wide `Api` search for `AddAuthentication` / `UseAuthentication` / `JwtBearer` / `OpenIdConnect` returned **no matches**.
- Therefore the `AdminAccessControl` branch `context.User.Identity.IsAuthenticated == true` is effectively **dead in production**. Test principals do not prove production login.

## 2. `AdminAccessControl` contract (current)

File: `Api/Endpoints/AdminAccessControl.cs`

| Input | Decision | HTTP mapping used by callers |
|---|---|---|
| Authenticated + role `Admin` | `Authorized` | allow |
| Authenticated + non-Admin | `Forbidden` | `403` |
| No auth + missing/blank `X-Admin-Key` | `MissingCredential` | `401` |
| No auth + configured key missing | `Forbidden` (fail closed) | `403` |
| No auth + wrong key | `Forbidden` | `403` |
| No auth + matching key (`StringComparison.Ordinal`) | `Authorized` | allow |

Config resolution order:

1. `Admin:ApiKey`
2. env `ADMIN_API_KEY`

Notes:

- Only role name recognized: `Admin`.
- Key compare is ordinary equality (not fixed-time).
- `Api/appsettings.Production.json` has **no** `Admin` section; production must supply env/override or writes fail closed with `403`.
- `CachedAnalyticsEndpoints.cs` contains a **duplicate** private admin-key check for cache invalidate (~line 2171). Drift risk vs shared helper.

## 3. Endpoint-family matrix

Legend:

- **Current** = what code enforces today
- **Phase 1 target** = accepted temporary pilot boundary under option (b)
- **Later** = after external identity decision (STAB later / RBAC)

### 3.1 Public health / version

| Surface | Current | Phase 1 target | API-key | Tests |
|---|---|---|---|---|
| `GET /health`, `/ready` | Anonymous (`.AllowAnonymous()`) | Public (keep) | n/a | probe smoke only |
| `GET /health/dependencies` | Anonymous | Public but keep payload redacted/minimal (STAB05) | n/a | none auth |
| `GET /api/runtime/version` | Anonymous | Public (keep) | n/a | live smoke |

Anonymous access is **intentional** for ops probes.

### 3.2 Read-only analytics

| Surface | Current | Phase 1 target | Later | Tests |
|---|---|---|---|---|
| `/api/analytics/*` GET dashboard, sales, DQ, decision-board, refresh-status, cached reads, inventory reads | None | Remain open for internal pilot | Viewer/Analyst | none auth |

Anonymous analytics reads are **de facto intentional** for the current SPA pilot. Not a STAB03 blocker; do not invent Viewer RBAC here.

### 3.3 Analytics actions

| Surface | Current | Phase 1 target | Tests |
|---|---|---|---|
| `GET /api/analytics/actions`, counts, outcomes summary, by id | None | Open read (pilot) | none for GET auth |
| `POST /api/analytics/actions/status` (source probe) | None | Treat as read-like; later Manager if needed | none |
| `POST /` upsert, `PATCH .../status`, `PATCH .../outcome` | `AdminAccessControl` | Keep Admin-key gate | `AnalyticsActionsEndpointsTests` (auth cases exist; suite currently has unrelated failures on this branch) |

### 3.4 Reports / export / documents

| Surface | Current | Phase 1 target | Risk | Tests |
|---|---|---|---|---|
| `/api/analytics/reports/*` | None | Open read for pilot OR Admin-key if report contains customer-sensitive ops detail | P1 | none auth |
| Inventory export / print / schedule run | None at endpoint; document context accessor | Do **not** treat header roles as auth | P0 spoof | none auth |
| `/api/documents/*`, `/api/exports*` | Document role/ownership + signed download; defaults `anonymous` + role `AnalyticsExport`; accepts `X-User-*` headers | Phase 1: document that header roles are **not** authentication; follow-up task must stop trusting unauthenticated headers for generate | P0 | none AdminAccess |

Evidence: `Infrastructure/Services/Documents/DocumentSecurityServices.cs` (`DocumentUserContextAccessor`).

### 3.5 Access import / cleanup

| Surface | Current | Phase 1 target | Tests |
|---|---|---|---|
| GET runtime-status, batches/jobs, logs | None | **Admin-key** (sensitive ops reads) → STAB04-adjacent / import follow-up | limited |
| `POST cleanup/preview`, `GET cleanup/archive`, archive export | None | **Admin-key** | none |
| cancel, enqueue, delete, cleanup execute, restore, `/run`, jobs start | `AdminAccessControl` | Keep | `AccessImportAdminAuthorizationTests`, `AccessImportRunEndpointTests` |

### 3.6 Workers / admin / config / logs / redis

| Surface | Current | Phase 1 target | Tests |
|---|---|---|---|
| `GET /api/workers/health`, `/control`, `/configuration` | None | Admin-key for config/control detail; health may stay public if redacted | none |
| Worker start/stop/restart/schedule + control enable/disable | `AdminAccessControl` | Keep | `WorkerConfigurationEndpointsTests` |
| `GET /api/admin/pending-batches`, `/health-check`, `/audit-log`, `/workers/list`, `/workers/{name}` | **None** (Swagger may claim 401) | **Admin-key** — **STAB04** | none |
| Admin requeue / stale recovery / demo-verification / worker writes | `AdminAccessControl` | Keep | demo-verification, repair, backend-routing suites |
| `GET /api/logs`, `/api/logs/{id}`, `/errors` | None | Admin-key (P0 info exposure) | none |
| `DELETE /api/logs/clear` | `AdminAccessControl` | Keep | none dedicated |
| `GET /api/redis/status` | None | Admin-key | none |
| `POST /api/redis/toggle` | `AdminAccessControl` | Keep | none dedicated |
| backend-routing / repair / optimize-sync writes | `AdminAccessControl` | Keep | `AdminBackendRoutingEndpointsTests`, `AdminRepairAuthorizationTests` |

### 3.7 Cache clear / manual refresh

| Surface | Current | Phase 1 target | Tests |
|---|---|---|---|
| `GET .../cache/status`, `GET /api/analytics/refresh-status` | None | Open status OK | none |
| `POST .../cache/invalidate` | Admin-key (local duplicate helper) | Keep; consolidate to shared helper | `AnalyticsCacheInvalidateAuthorizationTests` |
| `POST /api/analytics/optimize`, admin sync/init/optimize | `AdminAccessControl` | Keep | partial |
| Snapshot admin mutations | `AdminAccessControl` + feature flag | Keep | none dedicated |

## 4. Secret handling expectations

| Topic | Current | Required |
|---|---|---|
| Key storage | config/env | env in production; never commit real values |
| Missing key | fail closed (`403`) | keep |
| Wrong key | `403` | keep |
| Missing header | `401` | keep |
| Logging | not audited exhaustively here | never log `X-Admin-Key` or configured secret; never return key in ProblemDetails |
| Comparison | `Ordinal` equality | acceptable for Phase 1; consider fixed-time compare later |
| Rotation | undocumented | document rotation as ops runbook follow-up |

## 5. Phase 1 decision

### Chosen pattern: **(b) explicit internal API-key admin mode while external auth remains disabled**

Reasons:

1. Matches what production can actually enforce today.
2. Matches existing write-path tests and handlers.
3. Option **(a)** (principal + policies) is not available without inventing an IdP / auth registration — out of STAB03 scope.
4. Option **(c)** `BLOCKED` is unnecessary: the owner can accept (b) for an internal pilot without external identity.

### Phase 1 locked contract

1. Sensitive **writes / destructive / admin ops** require `X-Admin-Key` validated by `AdminAccessControl` (or one shared helper).
2. Missing credential → `401`; wrong/missing configured key → `403`.
3. Health/version remain anonymous.
4. Read-only analytics may stay open for the internal pilot until an IdP decision.
5. Document/export header roles are **not** authentication and must not be treated as such in new work.
6. Do not add JWT/OIDC/cookie auth in STAB04–STAB05 unless a later owner task explicitly selects an identity source.

## 6. Split follow-ups (one family per task)

| Order | Task | Family | Smallest change |
|---|---|---|---|
| 1 | **STAB04** | Admin operational reads | Gate `GET /api/admin/pending-batches`, `/health-check`, `/audit-log`, `/workers/list`, `/workers/{name}` with `AdminAccessControl` + tests |
| 2 | Import read/cleanup preview follow-up | Access-import sensitive reads | Gate cleanup preview/archive export + batch list if still public |
| 3 | Logs read follow-up | `/api/logs*` | Gate reads; keep clear already gated |
| 4 | Document header trust follow-up | Documents/exports | Stop trusting unauthenticated `X-User-*` for generate privilege |
| 5 | Helper consolidation | Cache invalidate | Remove duplicate key check; call shared `AdminAccessControl` |
| 6 | External identity (later) | Auth pipeline | Only after owner picks IdP; then map Analyst/Manager/Admin |

## 7. Checks run for this audit

| Check | Result |
|---|---|
| Code inspection `Program.cs` / `AdminAccessControl.cs` / endpoint families | Done |
| Config presence of `Admin` in Production appsettings | Absent (env required) |
| Document accessor header defaults | Confirmed spoofable defaults |
| Targeted auth-related `dotnet test` filter | **Fail** `17` / pass `46` on current branch — includes auth and non-auth assertion failures (e.g. actions JSON shape / missing key in some fixtures). Not treated as production IdP proof. |
| Runtime code changes | **None** (audit-only) |

## 8. Acceptance mapping

| Acceptance item | Status |
|---|---|
| Current production authentication capability proven, not assumed | **Proven: no auth pipeline; Admin-key only** |
| Every sensitive family has current + target boundary | **Yes (matrix above)** |
| Next code task small enough for one family | **STAB04 admin operational reads** |
| No external provider / broad RBAC invented | **Yes** |
