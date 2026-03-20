# Document Export Stabilization Checklist

## Build And Runtime

- Verify `dotnet build Api/Api.csproj` passes.
- Verify `dotnet build Workers/Workers.csproj` passes.
- Verify `dotnet test Api.Tests/Api.Tests.csproj` passes.
- In production, set `DOCUMENT_SIGNING_KEY` or `Documents:SigningKey` before startup.
- Confirm generated files are written under the configured `Documents:StorageRoot`.

## Export Safety

- CSV exports should open in Excel with UTF-8 encoding.
- PDF export is currently a stabilized simple-table renderer.
- Do not use the current PDF path for complex branding, rich Unicode, or arbitrary HTML documents without upgrading to a dedicated PDF engine.
- Validate signed download links after deployment.

## Queue And Worker Safety

- Confirm `Documents` table exists before enabling the document worker.
- Verify queued jobs move through `queued -> processing -> completed/failed`.
- Check logs for repeated queue claim retries or `42P01` warnings.

## Migration Safety For `022_ApplyDobavljaciMapping.sql`

- Do not auto-run this script as part of unattended startup.
- Take a full database backup before execution.
- Run it in staging against a production-like snapshot first.
- Verify `Dobavljaci` row count changes are expected.
- Verify `Artikli.IDDobavljac` relationships after execution.
- Verify `DnevnikPromena.DobavljacId` updates are correct.
- Review placeholder vendor mappings before execution.
- Keep the script idempotent and archive execution logs.
