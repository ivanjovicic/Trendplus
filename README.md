# Trendplus — Inventory & Sales Management (Developer + Ops Guide)

Trendplus is a production-grade backend and analytics pipeline for retail data: a .NET 8 Web API that ingests legacy point-of-sale databases (Access .mdb/.accdb), normalizes transactional and master data, stores it in PostgreSQL and maintains a synchronized Analytics database for reporting.

This README aims to be a single source of truth for developers and operators: quickstart, architecture, Access-import specifics, deployment notes, troubleshooting and developer workflows.

—

## Key Highlights

- Language & Frameworks: C#, .NET 8, ASP.NET Core Web API
- Persistence: Entity Framework Core (Npgsql) + PostgreSQL; `pgvector` for vector features
- Import: Robust Access importer using `unixODBC` + `libmdbodbc` (mdbtools) with a CLI fallback (mdb-export/mdb-tables)
- Patterns: CQRS + MediatR, background HostedServices (Workers), streaming I/O (IAsyncEnumerable)
- Resilience & Observability: Polly retries/circuit-breakers, Serilog (console/file), Swagger, health & perf endpoints
- Messaging & Cache: RabbitMQ broker integration, Redis caching (optional hybrid cache)
- ML Integration: ONNX runtime and optional Python embedding/model services via HTTP
- Container-first: Dockerfile with mdbtools preinstalled for Linux containers

—

## Quickstart (local dev)

Prereqs:
- .NET 8 SDK
- Docker (optional, recommended for parity)
- Node 18+ for frontend dev

Run backend locally (dev environment):

```powershell
cd Trendplus2
dotnet restore
dotnet build
dotnet run --project Api/Api.csproj
```

Frontend (dev server):

```bash
cd Klijent/clientapp
npm install
npm run dev
```

Run migrations (from repo root):

```powershell
dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj --context TrendplusDbContext
dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project Api/Api.csproj --context AnalyticsDbContext
```

—

## Configuration (important sections)

- `appsettings.json` / `appsettings.Development.json` — main configuration values. Key sections:
  - `ConnectionStrings:DefaultConnection` — Trendplus DB
  - `ConnectionStrings:AnalyticsConnection` — Analytics DB
  - `AccessImportOptions` — importer tuning (batch size, CLI timeouts, AutoInsertMissingParents)
  - `Caching:UseRedis` — toggle Redis
  - `Workers:Enabled` & `Workers:AllowRuntimeToggle` — control background workers

Environment variables commonly used in CI / Docker:
- `ASPNETCORE_ENVIRONMENT` — `Development` / `Production`
- `PORT` — server port
- `DOCUMENT_SIGNING_KEY` — required in production for document export

—

## Access Import: design and operational notes

Problem space:
- Many customers keep POS exports in legacy Access files. On Linux containers, the ODBC driver is brittle and can return inconsistent metadata.

Approach:
- Primary read mode: ODBC via `libmdbodbc` (unixODBC). When the provider returns unexpected schema or fails, the importer falls back to `mdbtools` CLI (`mdb-tables`, `mdb-export`).
- For open/locked ACCDB files we create a timestamped snapshot copy to ensure a consistent read.
- The importer pre-scans parent IDs (e.g. `prodaja_zaglavlje`) and either validates or optionally auto-inserts missing parents (`AutoInsertMissingParents` flag) to avoid FK violations during `prodaja_stavke` import.
- Large tables are streamed using `IAsyncEnumerable<AccessDataRow>` to limit memory and throughput spikes.

Operational tips:
- Docker image includes `mdbtools` and config for `odbcinst.ini` — keep image aligned with host distro.
- If imports fail with ODBC errors, inspect logs for fallback activation (logger warns) and run `mdb-tools` locally to validate the file.

—

## Architecture overview

- `Api/` — Web API, DI, `AccessImportService`, endpoints
- `Application/` — business logic, MediatR handlers, validators
- `Infrastructure/` — EF DbContexts, repositories, migrations, seeders
- `Domain/` — domain entities and value objects
- `Workers/` — background hosted services: import processor, analytics aggregator, outbox processor, training workers
- `Klijent/clientapp/` — React + TypeScript frontend

—

## Docker & Production

Build and run the container (example):

```bash
docker build -t trendplus:latest .
docker run -e ASPNETCORE_ENVIRONMENT=Production -e PORT=8080 -p 8080:8080 trendplus:latest
```

Notes:
- Dockerfile includes `mdbtools` and `unixodbc` so Access import works inside container.
- Ensure PostgreSQL connection strings point to production DB and that DB migrations have run before enabling workers.

—

## Developer workflows & tests

- Build: `dotnet build Api/Api.csproj`
- Tests: `dotnet test Api.Tests/Api.Tests.csproj`
- Run only importer preview (quick local check): call the API endpoint `POST /api/access-import/preview` with a sample ACCDB path or use the admin UI.

—

## Troubleshooting & Diagnostics

- If imports return "Could not find DSN nor DBQ": check `odbcinst.ini`, `libmdbodbc` presence and allow CLI fallback.
- For FK 23503 errors on `prodaja_stavke`: enable `AccessImportOptions:AutoInsertMissingParents` temporarily or inspect source file rows for parent ids.
- To debug heavy DB IO during import: monitor PostgreSQL `pg_stat_activity` and lower `_options.DbSaveBatchSize` to flush more frequently.

Logs & monitoring:
- Serilog writes console and file sinks. Check API logs for `AccessReadMode` and `cli-process` messages.
- Health & perf endpoints: `/health`, `/api/performance`, and `/api/logs`.

—

## Contribution & code style

- Follow existing repository conventions (folder layout already in use).
- Use `dotnet format` and run unit tests before opening PRs. Keep changes small and focused; add unit tests for core logic (import heuristics, schema parsing).

—

## Contact / Maintainers

- Primary maintainer: Ivan Jovičić — email: ivanjovicic1986@gmail.com

—

If you want, I can also:
- Add a short `CONTRIBUTING.md` and `SECURITY.md`.
- Create a markdown snippet explaining how to reproduce import failures locally with `mdbtools`.
- Commit this README update and push to `main`.
