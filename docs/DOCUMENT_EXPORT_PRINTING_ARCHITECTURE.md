# Trendplus Document Export And Print MVP

## What is implemented

- Generic export pipeline for any analytics table by sending normalized columns, rows, filters, and metadata to `/api/documents/generate`
- Sync generation for small datasets and queued generation for large datasets
- Formats: `csv`, `xlsx`, `pdf`, `html`
- Print-friendly HTML endpoint: `GET /api/documents/{id}/print`
- Batch export queue: `POST /api/documents/batch`
- Job status endpoint: `GET /api/exports/{jobId}/status`
- Versioned templates persisted in `DocumentTemplates`
- Immutable audit log in `DocumentAudits`
- Worker-driven async generation via `DocumentGenerationWorker`
- Signed download URLs plus ownership-based access checks

## Core backend flow

1. UI sends the current analytics table snapshot with columns, rows, filters, and metadata.
2. `DocumentService` validates access and chooses sync vs async using `Documents:SyncRowLimit` with a default of `10000`.
3. A `Documents` row is created immediately so the request is auditable and status-addressable.
4. Sync jobs render and store the file inline.
5. Async jobs are marked `queued` and claimed by `DocumentGenerationWorker` using `FOR UPDATE SKIP LOCKED`.
6. Rendered files are written to local storage under `out/documents/{yyyy}/{MM}/`.
7. Completion and failure events are written to `DocumentAudits` and also published to the outbox for later notifications.

## API contract

### `POST /api/documents/generate`

- Supports `csv`, `xlsx`, `pdf`
- Supports `portrait` and `landscape`
- Supports preview via `POST /api/documents/print-preview`
- Returns `200 OK` for sync or `202 Accepted` for queued jobs

### `GET /api/documents/{id}`

- Streams the generated file
- Accepts optional `token` query string for signed URL downloads

### `POST /api/documents/batch`

- Always queues all requested exports
- Returns a `batchId` and item-level status URLs

### `GET /api/exports/{jobId}/status`

- Returns current lifecycle state, file metadata, and fresh signed download URL when complete

## Storage and security model

- Default storage implementation is local filesystem and intentionally hidden behind `IDocumentStorage`
- Signed URL generation is handled by `IDocumentDownloadTokenService`
- Access control is handled by `IDocumentAccessControlService`
- Current user context resolves from claims first, then `X-User-*` headers for environments where auth is not fully wired yet

## Template model

- Templates are versioned by `(Name, Version)`
- Default seeded templates:
  - `analytics-table-default`
  - `executive-summary-default`
  - `receipt-default`
  - `label-default`
- Template content is sanitized with a deny-list guard to block inline script execution

## Scaling notes

- The queue store uses `FOR UPDATE SKIP LOCKED` so multiple worker replicas can safely claim jobs
- CSV generation is streamed directly to disk
- XLSX generation writes worksheet XML directly into a zip archive
- PDF generation is currently a built-in lightweight table renderer so the platform can ship without external binary dependencies
- For Phase B, swap `PdfDocumentRenderer` with PuppeteerSharp or QuestPDF behind the same `IDocumentRenderer` contract
