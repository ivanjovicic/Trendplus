using System.Globalization;
using System.Text.Json;
using Application.Documents.Interfaces;
using Application.Documents.Models;
using Infrastructure.DbContexts;
using Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Trendplus2.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var documentOptions = app.Services.GetRequiredService<IOptions<DocumentExportOptions>>().Value;
        var group = app.MapGroup("/api/documents")
            .WithTags("Documents");

        group.MapPost("/generate", async (
            HttpContext httpContext,
            IConfiguration configuration,
            DocumentGenerateRequestDto dto,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var request = dto.ToRequest();
            var result = await documentService.GenerateAsync(request, context, ct);
            var response = new DocumentOperationResponseDto
            {
                DocumentId = result.DocumentId,
                BatchId = result.BatchId,
                Status = result.Status,
                IsAsync = result.IsAsync,
                FileName = result.FileName,
                MimeType = result.MimeType,
                SizeBytes = result.SizeBytes,
                CreatedAtUtc = result.CreatedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                ExpiresAtUtc = result.ExpiresAtUtc,
                StatusUrl = $"/api/exports/{result.DocumentId}/status",
                DownloadUrl = result.CompletedAtUtc.HasValue
                    ? $"/api/documents/{result.DocumentId}?token={tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
                    : null,
                PrintUrl = $"/api/documents/{result.DocumentId}/print?token={tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
            };

            return result.IsAsync
                ? Results.Accepted(response.StatusUrl, response)
                : Results.Ok(response);
        })
        .RequireRateLimiting("writes");

        group.MapPost("/batch", async (
            HttpContext httpContext,
            IConfiguration configuration,
            DocumentBatchRequestDto dto,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var request = new DocumentBatchRequest
            {
                Items = dto.Items.Select(item => item.ToRequest()).ToList()
            };

            var result = await documentService.EnqueueBatchAsync(request, context, ct);
            return Results.Accepted($"/api/documents/batches/{result.BatchId}", new
            {
                result.BatchId,
                items = result.Items.Select(item => new
                {
                    item.DocumentId,
                    item.Status,
                    statusUrl = $"/api/exports/{item.DocumentId}/status"
                })
            });
        })
        .RequireRateLimiting("writes");

        group.MapGet("/{id:guid}", async (
            Guid id,
            string? token,
            HttpContext httpContext,
            IConfiguration configuration,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            DocumentExecutionContext context;
            if (!string.IsNullOrWhiteSpace(token) && tokenService.TryValidate(id, token))
            {
                context = userContextAccessor.GetCurrent();
            }
            else if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out context,
                    out var rejected))
            {
                return rejected!;
            }

            var streamResult = await documentService.OpenDownloadAsync(id, context, token, ct);
            if (streamResult is null)
            {
                return Results.NotFound();
            }

            return Results.Stream(
                streamResult.Stream,
                streamResult.MimeType,
                streamResult.FileName,
                enableRangeProcessing: true);
        })
        .RequireRateLimiting("fixed");

        group.MapGet("/{id:guid}/print", async (
            Guid id,
            string? token,
            HttpContext httpContext,
            IConfiguration configuration,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            DocumentExecutionContext context;
            if (!string.IsNullOrWhiteSpace(token) && tokenService.TryValidate(id, token))
            {
                context = userContextAccessor.GetCurrent();
            }
            else if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out context,
                    out var rejected))
            {
                return rejected!;
            }

            var html = await documentService.GetPrintHtmlAsync(id, context, token, ct);
            return html is null
                ? Results.NotFound()
                : Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireRateLimiting("fixed");

        group.MapPost("/print-preview", async (
            HttpContext httpContext,
            IConfiguration configuration,
            DocumentGenerateRequestDto dto,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            dto.Format = "html";
            dto.Preview = true;
            dto.ForceAsync = false;

            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var result = await documentService.GenerateAsync(dto.ToRequest(), context, ct);
            return Results.Ok(new
            {
                result.DocumentId,
                printUrl = $"/api/documents/{result.DocumentId}/print?token={tokenService.Create(result.DocumentId, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}",
                status = result.Status
            });
        })
        .RequireRateLimiting("writes");

        app.MapGet("/api/exports/{jobId:guid}/status", async (
            Guid jobId,
            HttpContext httpContext,
            IConfiguration configuration,
            IDocumentService documentService,
            IDocumentUserContextAccessor userContextAccessor,
            IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var status = await documentService.GetStatusAsync(jobId, context, ct);
            if (status is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new DocumentStatusResponseDto
            {
                DocumentId = status.DocumentId,
                BatchId = status.BatchId,
                Status = status.Status,
                Format = status.Format,
                TableKey = status.TableKey,
                TableTitle = status.TableTitle,
                IsAsync = status.IsAsync,
                RowCount = status.RowCount,
                FileName = status.FileName,
                MimeType = status.MimeType,
                SizeBytes = status.SizeBytes,
                ErrorMessage = status.ErrorMessage,
                CreatedAtUtc = status.CreatedAtUtc,
                StartedAtUtc = status.StartedAtUtc,
                CompletedAtUtc = status.CompletedAtUtc,
                ExpiresAtUtc = status.ExpiresAtUtc,
                DownloadUrl = status.Status == "completed"
                    ? $"/api/documents/{status.DocumentId}?token={tokenService.Create(status.DocumentId, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
                    : null,
                PrintUrl = $"/api/documents/{status.DocumentId}/print?token={tokenService.Create(status.DocumentId, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
            });
        })
        .WithTags("Documents")
        .RequireRateLimiting("fixed");

        app.MapGet("/api/exports", async (
            int? take,
            HttpContext httpContext,
            IConfiguration configuration,
            [FromServices] TrendplusDbContext db,
            [FromServices] IDocumentUserContextAccessor userContextAccessor,
            [FromServices] IDocumentAccessControlService accessControlService,
            [FromServices] IDocumentDownloadTokenService tokenService,
            CancellationToken ct) =>
        {
            if (!AdminAccessControl.TryAuthorizeDocumentPrivilege(
                    httpContext,
                    configuration,
                    userContextAccessor,
                    out var context,
                    out var rejected))
            {
                return rejected!;
            }

            var requestedTake = Math.Clamp(take ?? 50, 1, 200);
            var query = db.Documents.AsNoTracking();

            if (!accessControlService.CanBypassOwnership(context))
            {
                query = query.Where(x => x.RequestedByUserId == context.UserId);
            }

            var documents = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(requestedTake)
                .Select(x => new DocumentStatusResponseDto
                {
                    DocumentId = x.Id,
                    BatchId = x.BatchId,
                    Status = x.Status,
                    Format = x.Format,
                    TableKey = x.TableKey,
                    TableTitle = x.TableTitle,
                    IsAsync = x.IsAsync,
                    RowCount = x.RowCount,
                    FileName = x.FileName,
                    MimeType = x.MimeType,
                    SizeBytes = x.SizeBytes,
                    ErrorMessage = x.ErrorMessage,
                    CreatedAtUtc = x.CreatedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    DownloadUrl = x.Status == "completed"
                        ? $"/api/documents/{x.Id}?token={tokenService.Create(x.Id, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
                        : null,
                    PrintUrl = $"/api/documents/{x.Id}/print?token={tokenService.Create(x.Id, DateTime.UtcNow.AddMinutes(documentOptions.SignedUrlTtlMinutes))}"
                })
                .ToListAsync(ct);

            return Results.Ok(documents);
        })
        .WithTags("Documents")
        .RequireRateLimiting("fixed");
    }
}

public sealed class DocumentGenerateRequestDto
{
    public string Format { get; set; } = "csv";
    public string Orientation { get; set; } = "landscape";
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public bool Preview { get; set; }
    public bool ForceAsync { get; set; }
    public string? Locale { get; set; }
    public string? TemplateName { get; set; }
    public int? TemplateVersion { get; set; }
    public string? DocumentType { get; set; }
    public string TableKey { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public List<DocumentColumnDto> Columns { get; set; } = new();
    public List<JsonElement> Rows { get; set; } = new();
    public List<DocumentNamedValueDto> Filters { get; set; } = new();
    public List<DocumentNamedValueDto> Metadata { get; set; } = new();

    public DocumentGenerationRequest ToRequest()
    {
        var columns = Columns.Select(column => new DocumentColumnDefinition
        {
            Key = column.Key,
            Header = column.Header,
            DataType = column.DataType,
            FormatHint = column.FormatHint
        }).ToList();

        return new DocumentGenerationRequest
        {
            Format = Format.ToLowerInvariant(),
            Orientation = Orientation.ToLowerInvariant(),
            IncludeFiltersAndMetadata = IncludeFiltersAndMetadata,
            Preview = Preview,
            ForceAsync = ForceAsync,
            Locale = Locale,
            TemplateName = TemplateName ?? "analytics-table-default",
            TemplateVersion = TemplateVersion,
            DocumentType = DocumentType ?? "analytics-table-report",
            Table = new DocumentTablePayload
            {
                TableKey = TableKey,
                TableTitle = string.IsNullOrWhiteSpace(TableTitle) ? TableKey : TableTitle,
                Columns = columns,
                Filters = Filters.Select(item => item.ToModel()).ToList(),
                Metadata = Metadata.Select(item => item.ToModel()).ToList(),
                Rows = Rows.Select(row => ConvertRow(row, columns)).ToList()
            }
        };
    }

    private static List<string?> ConvertRow(JsonElement row, IReadOnlyList<DocumentColumnDefinition> columns)
    {
        if (row.ValueKind == JsonValueKind.Array)
        {
            return row.EnumerateArray().Select(Stringify).ToList();
        }

        if (row.ValueKind != JsonValueKind.Object)
        {
            return columns.Select(_ => Stringify(row)).ToList();
        }

        return columns.Select(column =>
        {
            return row.TryGetProperty(column.Key, out var value)
                ? Stringify(value)
                : string.Empty;
        }).ToList();
    }

    private static string? Stringify(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            _ => element.ToString()
        };
    }
}

public sealed class DocumentBatchRequestDto
{
    public List<DocumentGenerateRequestDto> Items { get; set; } = new();
}

public sealed class DocumentColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? DataType { get; set; }
    public string? FormatHint { get; set; }
}

public sealed class DocumentNamedValueDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }

    public DocumentNamedValue ToModel()
    {
        return new DocumentNamedValue
        {
            Key = Key,
            Label = string.IsNullOrWhiteSpace(Label) ? Key : Label,
            Value = Value
        };
    }
}

public sealed class DocumentOperationResponseDto
{
    public Guid DocumentId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? StatusUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? PrintUrl { get; set; }
}

public sealed class DocumentStatusResponseDto
{
    public Guid DocumentId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TableKey { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public int RowCount { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? DownloadUrl { get; set; }
    public string? PrintUrl { get; set; }
}
