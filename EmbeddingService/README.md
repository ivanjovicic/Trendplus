# Trendplus Image Embedding Service

FastAPI service for generating image embeddings with CLIP or SigLIP.

This service is intended for local or private-network use only. It is not wired as a public production dependency by default.

## Runtime behavior

- The service does not start unless `EMBEDDING_SERVICE_ENABLED=true` is set.
- `MODEL_TYPE` must be `clip` or `siglip`.
- Uploads are size-limited, batch requests are count-limited, and invalid images return safe generic errors.
- Long-running inference is wrapped with a timeout.
- The .NET backend must explicitly set `EmbeddingService:UseMock=false` and a private `EmbeddingService:BaseUrl` before it will call this service.
- Production startup is fail-closed for mock embeddings: `UseMock=true` is rejected.
- When production has `UseMock=false` but no private `BaseUrl` (or `Enabled=false`), the API starts with a disabled/quarantined embedding adapter that rejects similarity calls instead of returning random vectors.

## Start locally

```powershell
$env:EMBEDDING_SERVICE_ENABLED = "true"
$env:MODEL_TYPE = "siglip"
$env:HOST = "127.0.0.1"
$env:PORT = "8000"
python app.py
```

Optional tuning:

- `MAX_UPLOAD_BYTES` default `10485760`
- `MAX_BATCH_FILES` default `8`
- `EMBEDDING_TIMEOUT_SECONDS` default `30`

## Endpoints

### `GET /`
Health check with runtime metadata.

### `GET /health`
Detailed health status with limits and model info.

### `POST /embed`
Accepts one image file and returns an embedding vector.

### `POST /embed-batch`
Accepts multiple image files and returns per-file results.

### `POST /similarity`
Accepts two image files and returns cosine similarity.

## Model selection

`MODEL_TYPE` controls which checkpoint is loaded:

- `siglip` loads `google/siglip-base-patch16-256`
- `clip` loads `openai/clip-vit-base-patch32`

## .NET integration

If you want the API to call this service, configure:

```json
{
  "EmbeddingService": {
    "UseMock": false,
    "BaseUrl": "http://127.0.0.1:8000",
    "Timeout": 30
  }
}
```

Notes:

- `UseMock=true` is allowed only outside production.
- Production rejects loopback/public embedding URLs and only allows private-network service URLs when the Python path is enabled.
- Production default is quarantine: `UseMock=false` and `Enabled=false` (see `Api/appsettings.Production.json`).
- The repository does not include service-to-service auth for this path, so keep the service on trusted networking only.

## Validation and testing

Quick syntax check:

```powershell
python -m py_compile app.py
```

If you run the service locally, verify the health endpoint after startup:

```powershell
curl http://127.0.0.1:8000/health
```
