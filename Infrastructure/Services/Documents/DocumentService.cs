using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Application.Common.Interfaces;
using Application.Documents.Interfaces;
using Application.Documents.Models;
using Domain.Model.Documents;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services.Documents.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Documents;

public sealed class DocumentService : IDocumentService
{
    private static readonly Meter Meter = new("Trendplus.Documents");
    private static readonly Histogram<double> ExportDurationMs = Meter.CreateHistogram<double>("trendplus.documents.export.duration.ms");
    private static readonly Counter<long> ExportStartedCounter = Meter.CreateCounter<long>("trendplus.documents.export.started");
    private static readonly Counter<long> ExportCompletedCounter = Meter.CreateCounter<long>("trendplus.documents.export.completed");
    private static readonly Counter<long> ExportFailedCounter = Meter.CreateCounter<long>("trendplus.documents.export.failed");

    private readonly TrendplusDbContext _db;
    private readonly IDocumentAccessControlService _accessControlService;
    private readonly IDocumentAuditService _auditService;
    private readonly IDocumentStorage _storage;
    private readonly IDocumentDownloadTokenService _downloadTokenService;
    private readonly IDocumentTemplateRenderer _templateRenderer;
    private readonly IOutboxService _outboxService;
    private readonly IReadOnlyDictionary<string, IDocumentRenderer> _renderers;
    private readonly DocumentExportOptions _options;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        TrendplusDbContext db,
        IDocumentAccessControlService accessControlService,
        IDocumentAuditService auditService,
        IDocumentStorage storage,
        IDocumentDownloadTokenService downloadTokenService,
        IDocumentTemplateRenderer templateRenderer,
        IOutboxService outboxService,
        IEnumerable<IDocumentRenderer> renderers,
        IOptions<DocumentExportOptions> options,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _accessControlService = accessControlService;
        _auditService = auditService;
        _storage = storage;
        _downloadTokenService = downloadTokenService;
        _templateRenderer = templateRenderer;
        _outboxService = outboxService;
        _renderers = renderers.ToDictionary(renderer => renderer.Format, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DocumentGenerateResult> GenerateAsync(
        DocumentGenerationRequest request,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default)
    {
        _accessControlService.EnsureCanGenerate(executionContext);
        var normalizedRequest = Normalize(request);
        var isAsync = normalizedRequest.ForceAsync || normalizedRequest.Table.Rows.Count > _options.SyncRowLimit;
        var document = BuildDocumentRecord(normalizedRequest, executionContext, null, isAsync);
        ExportStartedCounter.Add(1, new KeyValuePair<string, object?>("format", normalizedRequest.Format));
        _logger.LogInformation(
            "Document export requested for table {TableKey} format {Format} rows {RowCount} async {IsAsync}",
            document.TableKey,
            document.Format,
            document.RowCount,
            isAsync);

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);
        await _auditService.WriteAsync(document.Id, DocumentAuditActions.Requested, executionContext, new { document.TableKey, document.Format, document.RowCount }, ct);

        if (isAsync)
        {
            await _outboxService.PublishAsync("DocumentQueued", new { document.Id, document.TableKey, document.Format }, executionContext.CorrelationId, ct);
            await _db.SaveChangesAsync(ct);
            await _auditService.WriteAsync(document.Id, DocumentAuditActions.Queued, executionContext, new { document.Id }, ct);
            return MapGenerateResult(document);
        }

        await GenerateInternalAsync(document.Id, normalizedRequest, executionContext, ct);
        var completed = await _db.Documents.AsNoTracking().FirstAsync(item => item.Id == document.Id, ct);
        return MapGenerateResult(completed);
    }

    public async Task<DocumentBatchResult> EnqueueBatchAsync(
        DocumentBatchRequest request,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default)
    {
        _accessControlService.EnsureCanGenerate(executionContext);
        var batchId = Guid.NewGuid();
        var items = new List<DocumentGenerateResult>();

        foreach (var item in request.Items)
        {
            var normalized = Normalize(item);
            var document = BuildDocumentRecord(normalized, executionContext, batchId, isAsync: true);
            _db.Documents.Add(document);
            items.Add(MapGenerateResult(document));
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Document batch queued {BatchId} with {Count} items.", batchId, items.Count);
        foreach (var item in items)
        {
            await _auditService.WriteAsync(item.DocumentId, DocumentAuditActions.Queued, executionContext, new { batchId }, ct);
        }

        await _outboxService.PublishAsync("DocumentBatchQueued", new { batchId, count = items.Count }, executionContext.CorrelationId, ct);
        await _db.SaveChangesAsync(ct);

        return new DocumentBatchResult
        {
            BatchId = batchId,
            Items = items
        };
    }

    public async Task<DocumentStatusResult?> GetStatusAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == documentId, ct);
        if (document is null)
        {
            return null;
        }

        _accessControlService.EnsureCanAccess(document, executionContext);
        return MapStatus(document);
    }

    public async Task<DocumentStreamResult?> OpenDownloadAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        string? signedToken,
        CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == documentId, ct);
        if (document is null || document.Status != DocumentStatuses.Completed || string.IsNullOrWhiteSpace(document.StoragePath))
        {
            return null;
        }

        if (!_downloadTokenService.TryValidate(documentId, signedToken))
        {
            _accessControlService.EnsureCanAccess(document, executionContext);
        }

        var stream = await _storage.OpenReadAsync(document.StoragePath, ct);
        await _auditService.WriteAsync(document.Id, DocumentAuditActions.Downloaded, executionContext, new { document.FileName }, ct);
        return new DocumentStreamResult
        {
            FileName = document.FileName ?? "document",
            MimeType = document.MimeType ?? "application/octet-stream",
            SizeBytes = document.SizeBytes,
            Stream = stream
        };
    }

    public async Task<string?> GetPrintHtmlAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        string? signedToken,
        CancellationToken ct = default)
    {
        var document = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == documentId, ct);
        if (document is null)
        {
            return null;
        }

        if (!_downloadTokenService.TryValidate(documentId, signedToken))
        {
            _accessControlService.EnsureCanAccess(document, executionContext);
        }

        var request = JsonSerializer.Deserialize<DocumentGenerationRequest>(document.RequestJson, DocumentJson.Options);
        if (request is null)
        {
            return null;
        }

        var (_, html) = await _templateRenderer.RenderHtmlAsync(request, _db, ct);
        await _auditService.WriteAsync(document.Id, DocumentAuditActions.Printed, executionContext, new { document.TableKey }, ct);
        return html;
    }

    public async Task ProcessQueuedDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(item => item.Id == documentId, ct);
        if (document is null)
        {
            return;
        }

        var request = JsonSerializer.Deserialize<DocumentGenerationRequest>(document.RequestJson, DocumentJson.Options);
        if (request is null)
        {
            await MarkFailureAsync(document, "Queued document request payload could not be deserialized.", null, ct);
            return;
        }

        var executionContext = new DocumentExecutionContext
        {
            UserId = document.RequestedByUserId,
            UserName = document.RequestedByUserName,
            Roles = document.RequestedByRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };

        try
        {
            await GenerateInternalAsync(documentId, request, executionContext, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process queued document {DocumentId}", documentId);
            await MarkFailureAsync(document, ex.Message, executionContext, ct);
        }
    }

    private async Task GenerateInternalAsync(Guid documentId, DocumentGenerationRequest request, DocumentExecutionContext executionContext, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var document = await _db.Documents.FirstAsync(item => item.Id == documentId, ct);
        if (!_renderers.TryGetValue(request.Format, out var renderer))
        {
            throw new InvalidOperationException($"Unsupported renderer format '{request.Format}'.");
        }

        document.Status = DocumentStatuses.Processing;
        document.StartedAtUtc = DateTime.UtcNow;
        document.NextAttemptAtUtc = null;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Document export started {DocumentId} table {TableKey} format {Format}", document.Id, document.TableKey, document.Format);

        var (template, html) = await _templateRenderer.RenderHtmlAsync(request, _db, ct);
        var fileName = DocumentFileName.Build(request.Table.TableTitle, request.Format, DateTime.UtcNow);
        var stored = await _storage.SaveAsync(document.Id, fileName, async (stream, token) =>
        {
            await renderer.RenderAsync(stream, request, html, token);
        }, ct);

        document.TemplateId = template.Id;
        document.TemplateVersion = template.Version;
        document.TemplateName = template.Name;
        document.MimeType = renderer.MimeType;
        document.FileName = stored.FileName;
        document.StoragePath = stored.RelativePath;
        document.SizeBytes = stored.SizeBytes;
        document.Sha256 = await DocumentHashing.ComputeSha256Async(stored.FullPath, ct);
        document.FileUrl = $"/api/documents/{document.Id}?token={_downloadTokenService.Create(document.Id, DateTime.UtcNow.AddMinutes(_options.SignedUrlTtlMinutes))}";
        document.Status = DocumentStatuses.Completed;
        document.CompletedAtUtc = DateTime.UtcNow;
        document.ExpiresAtUtc = DateTime.UtcNow.AddHours(_options.FileTtlHours);
        document.ErrorMessage = null;
        await _outboxService.PublishAsync("DocumentGenerated", new
        {
            document.Id,
            document.FileName,
            document.Format,
            document.TableKey,
            document.RequestedByUserId
        }, executionContext.CorrelationId, ct);
        await _db.SaveChangesAsync(ct);
        await _auditService.WriteAsync(document.Id, DocumentAuditActions.Completed, executionContext, new { document.FileName, document.SizeBytes }, ct);
        stopwatch.Stop();
        ExportCompletedCounter.Add(1, new KeyValuePair<string, object?>("format", document.Format));
        ExportDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("format", document.Format));
        _logger.LogInformation(
            "Document export completed {DocumentId} file {FileName} sizeBytes {SizeBytes} durationMs {DurationMs}",
            document.Id,
            document.FileName,
            document.SizeBytes,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private async Task MarkFailureAsync(DocumentRecord document, string error, DocumentExecutionContext? executionContext, CancellationToken ct)
    {
        document.RetryCount++;
        document.ErrorMessage = error;
        if (document.RetryCount >= 3)
        {
            document.Status = DocumentStatuses.Poisoned;
            document.NextAttemptAtUtc = null;
        }
        else
        {
            document.Status = DocumentStatuses.Queued;
            document.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, document.RetryCount));
        }

        await _db.SaveChangesAsync(ct);
        ExportFailedCounter.Add(1, new KeyValuePair<string, object?>("format", document.Format));
        _logger.LogError(
            "Document export failed {DocumentId} retryCount {RetryCount} status {Status} error {Error}",
            document.Id,
            document.RetryCount,
            document.Status,
            error);
        await _auditService.WriteAsync(
            document.Id,
            DocumentAuditActions.Failed,
            executionContext ?? new DocumentExecutionContext { UserId = document.RequestedByUserId, UserName = document.RequestedByUserName },
            new { document.RetryCount, error, document.Status },
            ct);
    }

    private static DocumentGenerationRequest Normalize(DocumentGenerationRequest request)
    {
        if (!DocumentFormats.All.Contains(request.Format))
        {
            throw new InvalidOperationException($"Format '{request.Format}' is not supported.");
        }

        if (request.Table.Columns.Count == 0)
        {
            throw new InvalidOperationException("At least one column is required.");
        }

        request.DocumentType = string.IsNullOrWhiteSpace(request.DocumentType)
            ? DocumentTemplateTypes.AnalyticsTableReport
            : request.DocumentType;
        request.TemplateName = string.IsNullOrWhiteSpace(request.TemplateName)
            ? "analytics-table-default"
            : request.TemplateName;
        request.Orientation = string.Equals(request.Orientation, DocumentOrientations.Portrait, StringComparison.OrdinalIgnoreCase)
            ? DocumentOrientations.Portrait
            : DocumentOrientations.Landscape;

        foreach (var row in request.Table.Rows)
        {
            while (row.Count < request.Table.Columns.Count)
            {
                row.Add(string.Empty);
            }
        }

        return request;
    }

    private static DocumentRecord BuildDocumentRecord(
        DocumentGenerationRequest request,
        DocumentExecutionContext executionContext,
        Guid? batchId,
        bool isAsync)
    {
        return new DocumentRecord
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            TemplateName = request.TemplateName,
            TemplateVersion = request.TemplateVersion ?? 0,
            DocumentType = request.DocumentType,
            TableKey = request.Table.TableKey,
            TableTitle = request.Table.TableTitle,
            Format = request.Format,
            Orientation = request.Orientation,
            Status = isAsync ? DocumentStatuses.Queued : DocumentStatuses.Requested,
            RequestedByUserId = executionContext.UserId,
            RequestedByUserName = executionContext.UserName,
            RequestedByRoles = string.Join(",", executionContext.Roles),
            Locale = request.Locale,
            IncludeFiltersAndMetadata = request.IncludeFiltersAndMetadata,
            IsPreview = request.Preview,
            IsAsync = isAsync,
            RowCount = request.Table.Rows.Count,
            FiltersJson = JsonSerializer.Serialize(request.Table.Filters, DocumentJson.Options),
            MetadataJson = JsonSerializer.Serialize(request.Table.Metadata, DocumentJson.Options),
            RequestJson = JsonSerializer.Serialize(request, DocumentJson.Options),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = isAsync ? DateTime.UtcNow : null
        };
    }

    private static DocumentGenerateResult MapGenerateResult(DocumentRecord document)
    {
        return new DocumentGenerateResult
        {
            DocumentId = document.Id,
            BatchId = document.BatchId,
            Status = document.Status,
            IsAsync = document.IsAsync,
            FileName = document.FileName,
            MimeType = document.MimeType,
            SizeBytes = document.SizeBytes,
            CreatedAtUtc = document.CreatedAtUtc,
            CompletedAtUtc = document.CompletedAtUtc,
            ExpiresAtUtc = document.ExpiresAtUtc
        };
    }

    private static DocumentStatusResult MapStatus(DocumentRecord document)
    {
        return new DocumentStatusResult
        {
            DocumentId = document.Id,
            BatchId = document.BatchId,
            Status = document.Status,
            Format = document.Format,
            TableKey = document.TableKey,
            TableTitle = document.TableTitle,
            IsAsync = document.IsAsync,
            RowCount = document.RowCount,
            FileName = document.FileName,
            MimeType = document.MimeType,
            SizeBytes = document.SizeBytes,
            ErrorMessage = document.ErrorMessage,
            CreatedAtUtc = document.CreatedAtUtc,
            StartedAtUtc = document.StartedAtUtc,
            CompletedAtUtc = document.CompletedAtUtc,
            ExpiresAtUtc = document.ExpiresAtUtc
        };
    }
}
