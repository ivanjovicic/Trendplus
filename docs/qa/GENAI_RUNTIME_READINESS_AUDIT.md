# GenAI Runtime Readiness Audit

Date: 2026-08-05
Repo: `ivanjovicic/Trendplus`
Scope: GAI01

## Verdict

Overall readiness for a real-data GenAI pilot is **BLOCKED**.

The repository does have an image embedding prototype and a .NET adapter, but the default runtime path is the mock service, the Python FastAPI service is not wired into deployment, and the Python service itself has no service auth, permissive CORS, unbounded reads, and raw exception leakage.

## Evidence summary

### PASS

| Area | Status | Evidence | Summary |
| --- | --- | --- | --- |
| Runtime selector is explicit | PASS | `Api/Program.cs`, `Api/appsettings.json` | The API chooses between `MockEmbeddingService` and `PythonEmbeddingService` via `EmbeddingService:UseMock`. |
| .NET adapter exists | PASS | `Infrastructure/Services/EmbeddingService.cs` | `PythonEmbeddingService` is implemented with `HttpClient` and a 30s timeout. |
| Separate image/vector tables exist | PASS | `Infrastructure/DbContexts/TrendplusDbContext.cs`, `Infrastructure/DbContexts/OpenProductTrainingDbContext.cs` | Product image vectors and feature vectors are already modeled in Postgres. |

### WARN

| Area | Status | Evidence | Summary |
| --- | --- | --- | --- |
| Mock is the default active path | WARN | `Api/appsettings.json`, `Api/Program.cs` | `EmbeddingService:UseMock` defaults to `true`, so the mock path is selected unless an operator overrides config. |
| Python service is manual/local only | WARN | `EmbeddingService/README.md`, `EmbeddingService/app.py`, `render.yaml`, `Dockerfile`, `docker-compose.yml`, `docker-compose.production.yml` | The FastAPI service can be run manually, but the main deploy files do not wire it as a production service. |
| Text/vector boundary is ambiguous | WARN | `Infrastructure/DbContexts/OpenProductTrainingDbContext.cs` | The schema has both image and text feature-vector tables, but there is no approved text-RAG runtime yet. |
| README overstates automation | WARN | `EmbeddingService/README.md`, `Api/Program.cs` | The README says the backend will automatically call the service, but runtime selection still depends on config and there is no production deployment wiring. |

### BLOCKED

| Area | Status | Evidence | Summary |
| --- | --- | --- | --- |
| Mock embeddings are random | BLOCKED | `Infrastructure/Services/EmbeddingService.cs` | `MockEmbeddingService` returns random vectors. That is not safe for production similarity. |
| Python service has no auth | BLOCKED | `EmbeddingService/app.py`, `Infrastructure/Services/EmbeddingService.cs` | The FastAPI service exposes `/embed`, `/embed-batch`, and `/similarity` without service-to-service authentication; the .NET client sends requests without credentials. |
| Python service has permissive CORS | BLOCKED | `EmbeddingService/app.py` | CORS allows local browser origins with credentials enabled and wildcard methods/headers. |
| Input is not bounded enough | BLOCKED | `EmbeddingService/app.py` | The service reads uploads into memory and does not define an application-level byte limit, batch limit, or explicit concurrency guard. |
| Raw exception text can leak | BLOCKED | `EmbeddingService/app.py` | Several handlers return `str(e)` to clients. |
| Production deploy path is missing | BLOCKED | `render.yaml`, `Dockerfile`, `docker-compose.yml`, `docker-compose.production.yml` | The repo deploys the .NET API, Redis, RabbitMQ, and Postgres, but not the Python embedding service. |

## Environment classification

| Component | Local/dev | Test/CI | Production | Notes |
| --- | --- | --- | --- | --- |
| `MockEmbeddingService` | Active by default | Active by default unless a test overrides config | Active by default from repo config | `UseMock=true` in `Api/appsettings.json`, and `Api/Program.cs` uses mock when the flag is true. |
| `PythonEmbeddingService` | Dormant/optional | Dormant/unreachable in normal repo tests | Dormant/unreachable | It activates only when `EmbeddingService:UseMock=false` and a reachable `EmbeddingService:BaseUrl` is provided. No deploy file wires it. |
| FastAPI `EmbeddingService/app.py` | Manually runnable | Not wired | Not wired | `python app.py` works locally, but there is no service declaration in the production deploy files. |

## Data boundary assessment

The current code base is **image-embedding prototype only**, not a production text copilot.

- `EmbeddingService/app.py` is image-only and returns image embeddings.
- `Infrastructure/Services/EmbeddingService.cs` is a read-only image similarity adapter.
- `Infrastructure/DbContexts/TrendplusDbContext.cs` maps `ProductImages` for image embeddings.
- `Infrastructure/DbContexts/OpenProductTrainingDbContext.cs` also maps feature-vector tables that are text-ready in shape, but there is no approved text retrieval pipeline, provider policy, or AI gateway yet.

Conclusion: the storage layer is partially prepared for vector work, but the runtime boundary is still prototype-grade and image-centric.

## Security and operational gaps

1. No service-to-service authentication between the .NET API and the Python embedding service.
2. No explicit production fail-closed validation for `EmbeddingService:UseMock`.
3. No deployment manifest for the Python embedding service.
4. No explicit request size or batch-count limits in the Python service.
5. No error redaction for client-facing exception text.
6. No evidence of CI coverage for the Python service path in this repo.

## Recommended next task

**GAI02 - Quarantine or harden the existing image embedding path**

Why this is the smallest next step:

- it is the smallest concrete hardening task after the readiness audit;
- it can fail closed in production;
- it can add bounds, auth, and tests without expanding into text RAG or agent work.

## Evidence files

- `Api/Program.cs`
- `Api/appsettings.json`
- `Api/appsettings.Development.json`
- `Api/appsettings.Production.json`
- `EmbeddingService/app.py`
- `EmbeddingService/README.md`
- `Infrastructure/Services/EmbeddingService.cs`
- `Infrastructure/DbContexts/TrendplusDbContext.cs`
- `Infrastructure/DbContexts/OpenProductTrainingDbContext.cs`
- `render.yaml`
- `Dockerfile`
- `docker-compose.yml`
- `docker-compose.production.yml`
