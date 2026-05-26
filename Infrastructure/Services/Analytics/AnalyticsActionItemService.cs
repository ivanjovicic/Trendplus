using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Infrastructure.Services.Analytics;

public sealed class AnalyticsActionItemService
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<AnalyticsActionItemService> _logger;

    public AnalyticsActionItemService(
        IAnalyticsDbContext db,
        ILogger<AnalyticsActionItemService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static bool IsOpenStatus(string? status)
        => status is AnalyticsActionConstants.Statuses.New
            or AnalyticsActionConstants.Statuses.Accepted
            or AnalyticsActionConstants.Statuses.Deferred;

    // ── Query ─────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<AnalyticsActionItem> Items, int TotalCount)> ListAsync(
        string? status,
        string? priority,
        string? sourceType,
        string? dataQualityStatus,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.AnalyticsActionItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            q = q.Where(x => x.Priority == priority);

        if (!string.IsNullOrWhiteSpace(sourceType))
            q = q.Where(x => x.SourceType == sourceType);

        if (!string.IsNullOrWhiteSpace(dataQualityStatus))
        {
            // Support legacy values by including any legacy keys that map to the requested canonical value
            var canonical = dataQualityStatus;
            var legacyKeys = AnalyticsActionConstants.DataQualityStatuses.LegacyMappings
                .Where(kv => string.Equals(kv.Value, canonical, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToArray();

            if (legacyKeys.Length > 0)
            {
                q = q.Where(x => x.DataQualityStatus == canonical || legacyKeys.Contains(x.DataQualityStatus));
            }
            else
            {
                q = q.Where(x => x.DataQualityStatus == canonical);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.Title.ToLower().Contains(term) ||
                (x.Description != null && x.Description.ToLower().Contains(term)) ||
                x.SourceType.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Priority == AnalyticsActionConstants.Priorities.P1)
            .ThenByDescending(x => x.Priority == AnalyticsActionConstants.Priorities.P2)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<AnalyticsActionItem?> GetByIdAsync(long id, bool includeNotes = false, CancellationToken ct = default)
    {
        var query = _db.AnalyticsActionItems.AsNoTracking();
        if (includeNotes)
        {
            query = query
                .Include(x => x.Notes.OrderBy(n => n.CreatedAtUtc));
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    // ── Counts for KPI bar ─────────────────────────────────────────────────

    public async Task<AnalyticsActionCountsDto> GetCountsAsync(CancellationToken ct = default)
    {
        var items = await _db.AnalyticsActionItems
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var p1Open = await _db.AnalyticsActionItems
            .AsNoTracking()
            .CountAsync(x =>
                x.Priority == AnalyticsActionConstants.Priorities.P1 &&
                (x.Status == AnalyticsActionConstants.Statuses.New ||
                 x.Status == AnalyticsActionConstants.Statuses.Accepted ||
                 x.Status == AnalyticsActionConstants.Statuses.Deferred), ct);

        return new AnalyticsActionCountsDto(
            New: items.FirstOrDefault(x => x.Status == AnalyticsActionConstants.Statuses.New)?.Count ?? 0,
            Accepted: items.FirstOrDefault(x => x.Status == AnalyticsActionConstants.Statuses.Accepted)?.Count ?? 0,
            Deferred: items.FirstOrDefault(x => x.Status == AnalyticsActionConstants.Statuses.Deferred)?.Count ?? 0,
            Rejected: items.FirstOrDefault(x => x.Status == AnalyticsActionConstants.Statuses.Rejected)?.Count ?? 0,
            Done: items.FirstOrDefault(x => x.Status == AnalyticsActionConstants.Statuses.Done)?.Count ?? 0,
            P1Open: p1Open
        );
    }

    // ── Upsert (idempotent by sourceType + sourceKey for open actions) ─────

    public async Task<AnalyticsActionItem> UpsertAsync(
        AnalyticsActionUpsertRequest request,
        string? userId,
        CancellationToken ct = default)
    {
        var result = await UpsertWithResultAsync(request, userId, ct);
        return result.Item;
    }

    public async Task<AnalyticsActionUpsertResult> UpsertWithResultAsync(
        AnalyticsActionUpsertRequest request,
        string? userId,
        CancellationToken ct = default)
    {
        if (!AnalyticsActionConstants.IsValidSourceType(request.SourceType))
        {
            throw new ArgumentException(
                $"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}",
                nameof(request.SourceType));
        }

        if (!AnalyticsActionConstants.IsValidPriority(request.Priority))
        {
            throw new ArgumentException(
                $"priority must be one of: {string.Join(", ", AnalyticsActionConstants.Priorities.AllValues)}",
                nameof(request.Priority));
        }

        // Normalize legacy data quality values (e.g., "fair" -> "warning", "poor" -> "critical")
        var normalizedDataQuality = AnalyticsActionConstants.NormalizeDataQualityStatus(request.DataQualityStatus);
        if (normalizedDataQuality is not null && !AnalyticsActionConstants.IsValidDataQualityStatus(normalizedDataQuality))
        {
            throw new ArgumentException(
                $"dataQualityStatus must be one of: {string.Join(", ", AnalyticsActionConstants.DataQualityStatuses.AllValues)}",
                nameof(request.DataQualityStatus));
        }

        // Check for existing open action with same sourceType + sourceKey
        var existing = await _db.AnalyticsActionItems
            .FirstOrDefaultAsync(x =>
                x.SourceType == request.SourceType &&
                x.SourceKey == request.SourceKey &&
            IsOpenStatus(x.Status),
                ct);

        if (existing is not null)
        {
            _logger.LogDebug("AnalyticsActionItem upsert: found existing open action {Id} for {SourceType}/{SourceKey}",
                existing.Id, request.SourceType, request.SourceKey);
            return new AnalyticsActionUpsertResult(
                Item: existing,
                Created: false,
                Existing: true,
                Status: existing.Status,
                SourceKey: existing.SourceKey);
        }

        var item = new AnalyticsActionItem
        {
            SourceType = request.SourceType,
            SourceKey = request.SourceKey,
            SourceId = request.SourceId,
            Title = request.Title,
            Description = request.Description,
            RecommendationStatus = request.RecommendationStatus,
            Priority = request.Priority,
            ImpactEstimateRsd = request.ImpactEstimateRsd,
            DueAtUtc = request.DueAtUtc,
            ExpectedImpactRsd = request.ExpectedImpactRsd,
            ConfidencePct = request.ConfidencePct,
            ReliabilityPct = request.ReliabilityPct,
            DataQualityStatus = normalizedDataQuality,
            Status = AnalyticsActionConstants.Statuses.New,
            ActionUrl = request.ActionUrl,
            MetadataJson = request.MetadataJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
        };

        _db.AnalyticsActionItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AnalyticsActionItem created: Id={Id} SourceType={SourceType} SourceKey={SourceKey} Priority={Priority}",
            item.Id, item.SourceType, item.SourceKey, item.Priority);

        return new AnalyticsActionUpsertResult(
            Item: item,
            Created: true,
            Existing: false,
            Status: item.Status,
            SourceKey: item.SourceKey);
    }

    public async Task<IReadOnlyList<AnalyticsActionSourceStatusDto>> GetSourceStatusesAsync(
        string sourceType,
        IReadOnlyCollection<string> sourceKeys,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType) || sourceKeys.Count == 0)
        {
            return Array.Empty<AnalyticsActionSourceStatusDto>();
        }

        var normalizedKeys = sourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedKeys.Length == 0)
        {
            return Array.Empty<AnalyticsActionSourceStatusDto>();
        }

        var candidates = await _db.AnalyticsActionItems
            .AsNoTracking()
            .Where(x => x.SourceType == sourceType && normalizedKeys.Contains(x.SourceKey))
            .Select(x => new
            {
                x.Id,
                x.SourceKey,
                x.Status,
                x.UpdatedAtUtc,
                OpenRank = IsOpenStatus(x.Status) ? 1 : 0,
            })
            .ToListAsync(ct);

        var bestByKey = candidates
            .GroupBy(x => x.SourceKey)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.OpenRank)
                    .ThenByDescending(x => x.UpdatedAtUtc)
                    .First(),
                StringComparer.Ordinal);

        var items = new List<AnalyticsActionSourceStatusDto>(normalizedKeys.Length);
        foreach (var key in normalizedKeys)
        {
            if (!bestByKey.TryGetValue(key, out var candidate))
            {
                items.Add(new AnalyticsActionSourceStatusDto(
                    SourceKey: key,
                    Exists: false,
                    Status: null,
                    ActionId: null));
                continue;
            }

            var exists = IsOpenStatus(candidate.Status);
            items.Add(new AnalyticsActionSourceStatusDto(
                SourceKey: key,
                Exists: exists,
                Status: candidate.Status,
                ActionId: candidate.Id));
        }

        return items;
    }

    // ── Status update ──────────────────────────────────────────────────────

    public async Task<AnalyticsActionItem?> UpdateStatusAsync(
        long id,
        string newStatus,
        string? note,
        string? userId,
        string? userName,
        CancellationToken ct = default)
    {
        if (!AnalyticsActionConstants.IsValidStatus(newStatus))
        {
            _logger.LogWarning(
                "AnalyticsActionItem status update rejected: invalid status {Status}. Allowed: {AllowedStatuses}",
                newStatus,
                string.Join(", ", AnalyticsActionConstants.Statuses.AllValues));
            throw new ArgumentException(
                $"status must be one of: {string.Join(", ", AnalyticsActionConstants.Statuses.AllValues)}",
                nameof(newStatus));
        }

        var item = await _db.AnalyticsActionItems
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (item is null)
            return null;

        var oldStatus = item.Status;
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (oldStatus == newStatus && normalizedNote is null)
            return item;

        var now = DateTime.UtcNow;
        item.Status = newStatus;
        item.UpdatedAtUtc = now;
        item.UpdatedByUserId = userId;
        item.UpdatedByUserName = userName;

        // Only terminal statuses are considered resolved.
        // rejected/done => resolved timestamp is set.
        // new/accepted/deferred => action is open (reopened) and resolved timestamp is cleared.
        if (newStatus is AnalyticsActionConstants.Statuses.Rejected or AnalyticsActionConstants.Statuses.Done)
        {
            item.ResolvedAtUtc ??= now;
        }
        else if (newStatus is AnalyticsActionConstants.Statuses.New
            or AnalyticsActionConstants.Statuses.Accepted
            or AnalyticsActionConstants.Statuses.Deferred)
        {
            item.ResolvedAtUtc = null;
        }

        if (oldStatus != newStatus)
        {
            _db.AnalyticsActionNotes.Add(new AnalyticsActionNote
            {
                ActionItemId = item.Id,
                StatusFrom = oldStatus,
                StatusTo = newStatus,
                Note = normalizedNote,
                CreatedAtUtc = now,
                CreatedByUserId = userId,
                CreatedByUserName = userName
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AnalyticsActionItem status updated: Id={Id} NewStatus={Status} UserId={UserId}",
            item.Id, newStatus, userId);

        return item;
    }

    public async Task<AnalyticsActionItem?> UpdateOutcomeAsync(
        long id,
        AnalyticsActionOutcomeUpdateRequest request,
        string? userId,
        string? userName,
        CancellationToken ct = default)
    {
        if (!AnalyticsActionConstants.IsValidOutcomeStatus(request.OutcomeStatus))
        {
            _logger.LogWarning(
                "AnalyticsActionItem outcome update rejected: invalid outcome {OutcomeStatus}. Allowed: {AllowedStatuses}",
                request.OutcomeStatus,
                string.Join(", ", AnalyticsActionConstants.OutcomeStatuses.AllValues));
            throw new ArgumentException(
                $"outcomeStatus must be one of: {string.Join(", ", AnalyticsActionConstants.OutcomeStatuses.AllValues)}",
                nameof(request.OutcomeStatus));
        }

        var item = await _db.AnalyticsActionItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return null;

        var now = DateTime.UtcNow;
        item.OutcomeStatus = request.OutcomeStatus;
        item.MeasuredImpactRsd = request.MeasuredImpactRsd;
        item.OutcomeMeasuredAtUtc = request.OutcomeMeasuredAtUtc ?? now;
        item.OutcomeNotes = string.IsNullOrWhiteSpace(request.OutcomeNotes) ? null : request.OutcomeNotes.Trim();
        item.UpdatedAtUtc = now;
        item.UpdatedByUserId = userId;
        item.UpdatedByUserName = userName;

        if (request.OutcomeStatus is AnalyticsActionConstants.OutcomeStatuses.Pending)
        {
            item.OutcomeMeasuredAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AnalyticsActionItem outcome updated: Id={Id} OutcomeStatus={OutcomeStatus} UserId={UserId}",
            item.Id,
            request.OutcomeStatus,
            userId);

        return item;
    }
}

// ── DTOs scoped to service (no separate file to keep it lean) ────────────────

public sealed record AnalyticsActionCountsDto(
    int New,
    int Accepted,
    int Deferred,
    int Rejected,
    int Done,
    int P1Open
);

public sealed record AnalyticsActionUpsertRequest(
    string SourceType,
    string SourceKey,
    int? SourceId,
    string Title,
    string? Description,
    string? RecommendationStatus,
    string Priority,
    decimal? ImpactEstimateRsd,
    DateTime? DueAtUtc,
    decimal? ExpectedImpactRsd,
    int? ConfidencePct,
    int? ReliabilityPct,
    string? DataQualityStatus,
    string? ActionUrl,
    string? MetadataJson
);

public sealed record AnalyticsActionUpsertResult(
    AnalyticsActionItem Item,
    bool Created,
    bool Existing,
    string Status,
    string SourceKey
);

public sealed record AnalyticsActionSourceStatusDto(
    string SourceKey,
    bool Exists,
    string? Status,
    long? ActionId
);

    public sealed record AnalyticsActionOutcomeUpdateRequest(
        string OutcomeStatus,
        decimal? MeasuredImpactRsd,
        DateTime? OutcomeMeasuredAtUtc,
        string? OutcomeNotes
    );
