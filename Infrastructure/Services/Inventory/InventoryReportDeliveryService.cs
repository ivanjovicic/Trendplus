using System.Globalization;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Documents.Interfaces;
using Application.Documents.Models;
using Application.Inventory.Models;
using Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Inventory;

public sealed class InventoryReportDeliveryService
{
    private static readonly CultureInfo SerbianCulture = CultureInfo.GetCultureInfo("sr-RS");

    private readonly ITrendplusDbContext _db;
    private readonly IAnalyticsDbContext _analyticsDb;
    private readonly IDocumentService _documentService;
    private readonly IDocumentDownloadTokenService _downloadTokenService;
    private readonly IEmailService _emailService;
    private readonly SmtpOptions _smtpOptions;
    private readonly DocumentExportOptions _documentOptions;
    private readonly ILogger<InventoryReportDeliveryService> _logger;

    public InventoryReportDeliveryService(
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
        IDocumentService documentService,
        IDocumentDownloadTokenService downloadTokenService,
        IEmailService emailService,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<DocumentExportOptions> documentOptions,
        ILogger<InventoryReportDeliveryService> logger)
    {
        _db = db;
        _analyticsDb = analyticsDb;
        _documentService = documentService;
        _downloadTokenService = downloadTokenService;
        _emailService = emailService;
        _smtpOptions = smtpOptions.Value;
        _documentOptions = documentOptions.Value;
        _logger = logger;
    }

    public async Task<InventoryReportScheduleRunResult> RunAsync(
        InventoryReportScheduleDefinition schedule,
        string initiatedByUserId,
        string initiatedByUserName,
        bool manualTrigger,
        CancellationToken ct = default)
    {
        var executedAtUtc = DateTime.UtcNow;
        try
        {
            var rows = await BuildRowsAsync(schedule, ct);
            var request = BuildDocumentRequest(schedule, rows);
            var context = new DocumentExecutionContext
            {
                UserId = initiatedByUserId,
                UserName = initiatedByUserName,
                Roles = ["Admin", "AnalyticsExport"],
                CorrelationId = $"inventory-schedule-{schedule.Id}-{executedAtUtc:yyyyMMddHHmmss}"
            };

            var generated = await _documentService.GenerateAsync(request, context, ct);
            var status = await WaitForCompletionAsync(generated.DocumentId, context, ct);
            if (status is null || !string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return new InventoryReportScheduleRunResult(
                    false,
                    status?.Status ?? generated.Status,
                    "Dokument nije zavrsen u okviru predvidjenog vremena.",
                    generated.DocumentId,
                    executedAtUtc);
            }

            if (_emailService.IsEnabled)
            {
                await TrySendEmailAsync(schedule, status.DocumentId, status.FileName, context, rows.Count, manualTrigger, ct);
                return new InventoryReportScheduleRunResult(
                    true,
                    "emailed",
                    $"Izvestaj je poslat na: {schedule.RecipientsCsv}",
                    status.DocumentId,
                    executedAtUtc);
            }

            return new InventoryReportScheduleRunResult(
                true,
                "generated",
                "SMTP nije ukljucen; dokument je generisan bez slanja mejla.",
                status.DocumentId,
                executedAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory report schedule delivery failed for schedule {ScheduleId}", schedule.Id);
            return new InventoryReportScheduleRunResult(
                false,
                "failed",
                ex.Message,
                null,
                executedAtUtc);
        }
    }

    private async Task TrySendEmailAsync(
        InventoryReportScheduleDefinition schedule,
        Guid documentId,
        string? fileName,
        DocumentExecutionContext context,
        int rowCount,
        bool manualTrigger,
        CancellationToken ct)
    {
        var recipients = ParseRecipients(schedule.RecipientsCsv);
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Schedule does not contain valid recipients.");
        }

        await using var streamResult = await _documentService.OpenDownloadAsync(documentId, context, null, ct);
        if (streamResult is null)
        {
            throw new InvalidOperationException("Generated document could not be opened for email delivery.");
        }

        byte[] content;
        await using (var memory = new MemoryStream())
        {
            await streamResult.Stream.CopyToAsync(memory, ct);
            content = memory.ToArray();
        }

        var attachments = new List<EmailAttachment>();
        if (content.Length <= _smtpOptions.AttachmentSizeLimitMb * 1024 * 1024)
        {
            attachments.Add(new EmailAttachment
            {
                FileName = fileName ?? $"inventory-report-{documentId}.bin",
                ContentType = streamResult.MimeType,
                Content = content
            });
        }

        var downloadToken = _downloadTokenService.Create(
            documentId,
            DateTime.UtcNow.AddMinutes(_documentOptions.SignedUrlTtlMinutes));

        var email = new EmailMessage
        {
            To = recipients,
            Subject = string.IsNullOrWhiteSpace(schedule.Subject)
                ? $"Bilans stanja | {schedule.Name}"
                : schedule.Subject!,
            HtmlBody = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
                  <h2 style="margin-bottom:8px;">Bilans stanja je spreman</h2>
                  <p style="margin:0 0 12px 0;">Raspored: <strong>{System.Net.WebUtility.HtmlEncode(schedule.Name)}</strong></p>
                  <p style="margin:0 0 12px 0;">Format: <strong>{schedule.Format.ToUpperInvariant()}</strong> | Redova: <strong>{rowCount.ToString("N0", SerbianCulture)}</strong> | Pokretanje: <strong>{(manualTrigger ? "rucno" : "automatski")}</strong></p>
                  <p style="margin:0 0 12px 0;">Dokument ID: <strong>{documentId}</strong></p>
                  <p style="margin:0;">Ako attachment nije prilozen zbog velicine, dokument je dostupan preko API-ja uz token: <code>{downloadToken}</code>.</p>
                </div>
                """
        };
        email.Attachments.AddRange(attachments);

        await _emailService.SendAsync(email, ct);
    }

    private async Task<DocumentStatusResult?> WaitForCompletionAsync(
        Guid documentId,
        DocumentExecutionContext context,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 18; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var status = await _documentService.GetStatusAsync(documentId, context, ct);
            if (status is null)
            {
                return null;
            }

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        return await _documentService.GetStatusAsync(documentId, context, ct);
    }

    private async Task<List<InventoryExportRow>> BuildRowsAsync(InventoryReportScheduleDefinition schedule, CancellationToken ct)
    {
        var baseItems = await _db.Artikli
            .AsNoTracking()
            .Where(a =>
                (!schedule.StoreId.HasValue || a.IDObjekat == schedule.StoreId.Value)
                && (!schedule.SupplierId.HasValue || a.IDDobavljac == schedule.SupplierId.Value)
                && (string.IsNullOrWhiteSpace(schedule.Search)
                    || (a.Naziv ?? string.Empty).Contains(schedule.Search)
                    || (a.PLU ?? string.Empty).Contains(schedule.Search)))
            .Select(a => new
            {
                a.Id,
                a.PLU,
                a.Naziv,
                Quantity = a.Kolicina ?? 0,
                Minimum = a.MinimalnaKolicina ?? 0,
                UnitCost = a.NabavnaCena ?? 0m,
                StoreId = a.IDObjekat,
                SupplierId = a.IDDobavljac
            })
            .ToListAsync(ct);

        var storeIds = baseItems.Where(x => x.StoreId.HasValue).Select(x => x.StoreId!.Value).Distinct().ToList();
        var supplierIds = baseItems.Where(x => x.SupplierId.HasValue).Select(x => x.SupplierId!.Value).Distinct().ToList();

        var storeNames = storeIds.Count == 0
            ? new Dictionary<int, string>()
            : await _analyticsDb.StoresDim
                .AsNoTracking()
                .Where(x => storeIds.Contains(x.StoreId))
                .GroupBy(x => x.StoreId)
                .Select(x => new { StoreId = x.Key, Name = x.Select(y => y.StoreName).FirstOrDefault() ?? $"Objekat {x.Key}" })
                .ToDictionaryAsync(x => x.StoreId, x => x.Name, ct);

        var supplierNames = supplierIds.Count == 0
            ? new Dictionary<int, string>()
            : await _analyticsDb.SuppliersDim
                .AsNoTracking()
                .Where(x => supplierIds.Contains(x.SupplierId))
                .GroupBy(x => x.SupplierId)
                .Select(x => new { SupplierId = x.Key, Name = x.Select(y => y.Naziv).FirstOrDefault() ?? $"Dobavljac {x.Key}" })
                .ToDictionaryAsync(x => x.SupplierId, x => x.Name, ct);

        var rows = baseItems
            .Select(item => new InventoryExportRow(
                item.PLU,
                item.Naziv,
                item.Quantity,
                item.Minimum,
                Math.Max(item.Minimum - item.Quantity, 0),
                item.UnitCost,
                item.UnitCost * item.Quantity,
                item.StoreId.HasValue ? storeNames.GetValueOrDefault(item.StoreId.Value, $"Objekat {item.StoreId.Value}") : "Sve lokacije",
                item.SupplierId.HasValue ? supplierNames.GetValueOrDefault(item.SupplierId.Value, $"Dobavljac {item.SupplierId.Value}") : "Nerasporedjen"))
            .ToList();

        return ApplySorting(rows, schedule.SortBy);
    }

    private static List<InventoryExportRow> ApplySorting(List<InventoryExportRow> rows, string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "naziv" => rows.OrderBy(x => x.Naziv).ToList(),
            "vrednost" => rows.OrderByDescending(x => x.EstimatedValue).ToList(),
            _ => rows.OrderByDescending(x => x.Quantity).ThenBy(x => x.Naziv).ToList()
        };
    }

    private static DocumentGenerationRequest BuildDocumentRequest(
        InventoryReportScheduleDefinition schedule,
        List<InventoryExportRow> rows)
    {
        return new DocumentGenerationRequest
        {
            Format = schedule.Format,
            Orientation = schedule.Orientation,
            IncludeFiltersAndMetadata = schedule.IncludeFiltersAndMetadata,
            ForceAsync = rows.Count > 5000,
            Locale = "sr-RS",
            TemplateName = "inventory-balance",
            DocumentType = "inventory-balance",
            Table = new DocumentTablePayload
            {
                TableKey = "inventory-balance",
                TableTitle = $"Bilans stanja | {schedule.Name}",
                Columns =
                {
                    new() { Key = "plu", Header = "PLU", DataType = "text" },
                    new() { Key = "naziv", Header = "Naziv", DataType = "text" },
                    new() { Key = "qty", Header = "Kolicina", DataType = "number" },
                    new() { Key = "min", Header = "Minimum", DataType = "number" },
                    new() { Key = "gap", Header = "Gap", DataType = "number" },
                    new() { Key = "unitCost", Header = "Nabavna cena", DataType = "currency" },
                    new() { Key = "value", Header = "Vrednost", DataType = "currency" },
                    new() { Key = "store", Header = "Prodavnica", DataType = "text" },
                    new() { Key = "supplier", Header = "Dobavljac", DataType = "text" }
                },
                Rows = rows
                    .Select(row => new List<string?>
                    {
                        row.PLU,
                        row.Naziv,
                        row.Quantity.ToString(SerbianCulture),
                        row.Minimum.ToString(SerbianCulture),
                        row.ReorderGap.ToString(SerbianCulture),
                        row.UnitCost.ToString("F2", SerbianCulture),
                        row.EstimatedValue.ToString("F2", SerbianCulture),
                        row.StoreName,
                        row.SupplierName
                    })
                    .ToList(),
                Filters =
                {
                    new() { Key = "store", Label = "Prodavnica", Value = schedule.StoreId?.ToString(SerbianCulture) ?? "Sve" },
                    new() { Key = "supplier", Label = "Dobavljac", Value = schedule.SupplierId?.ToString(SerbianCulture) ?? "Svi" },
                    new() { Key = "search", Label = "Pretraga", Value = schedule.Search ?? "Nema" }
                },
                Metadata =
                {
                    new() { Key = "generatedAt", Label = "Generisano", Value = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm", SerbianCulture) },
                    new() { Key = "schedule", Label = "Raspored", Value = schedule.Name },
                    new() { Key = "recipients", Label = "Primaoci", Value = schedule.RecipientsCsv }
                }
            }
        };
    }

    private static List<string> ParseRecipients(string recipientsCsv)
    {
        return recipientsCsv
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => x.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record InventoryExportRow(
        string? PLU,
        string Naziv,
        int Quantity,
        int Minimum,
        int ReorderGap,
        decimal UnitCost,
        decimal EstimatedValue,
        string StoreName,
        string SupplierName
    );
}
