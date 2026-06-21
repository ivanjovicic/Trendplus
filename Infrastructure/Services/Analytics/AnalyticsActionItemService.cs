using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Infrastructure.Services.Analytics;

public sealed class AnalyticsActionItemService
{
    private const int MaxOutcomeNotesLength = 4000;
    private const int LedgerSchemaVersion = 1;
    private static readonly JsonSerializerOptions LedgerJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    private async Task<AnalyticsActionItem?> FindExistingOpenActionAsync(
        string sourceType,
        string sourceKey,
        CancellationToken ct)
    {
        return await _db.AnalyticsActionItems
            .FirstOrDefaultAsync(
                x => x.SourceType == sourceType
                    && x.SourceKey == sourceKey
                    && IsOpenStatus(x.Status),
                ct);
    }

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

    public async Task<AnalyticsActionOutcomeSummaryDto> GetOutcomeSummaryAsync(
        AnalyticsActionOutcomeSummaryQuery query,
        CancellationToken ct = default)
    {
        var normalizedDataQualityStatus = AnalyticsActionConstants.NormalizeDataQualityStatus(query.DataQualityStatus);
        var generatedAtUtc = DateTime.UtcNow;

        var q = _db.AnalyticsActionItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SourceType))
            q = q.Where(x => x.SourceType == query.SourceType);

        if (!string.IsNullOrWhiteSpace(query.Priority))
            q = q.Where(x => x.Priority == query.Priority);

        if (!string.IsNullOrWhiteSpace(normalizedDataQualityStatus))
        {
            var legacyKeys = AnalyticsActionConstants.DataQualityStatuses.LegacyMappings
                .Where(kv => string.Equals(kv.Value, normalizedDataQualityStatus, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key)
                .ToArray();

            if (legacyKeys.Length > 0)
            {
                q = q.Where(x => x.DataQualityStatus == normalizedDataQualityStatus || legacyKeys.Contains(x.DataQualityStatus!));
            }
            else
            {
                q = q.Where(x => x.DataQualityStatus == normalizedDataQualityStatus);
            }
        }

        if (query.CreatedFrom.HasValue)
            q = q.Where(x => x.CreatedAtUtc >= query.CreatedFrom.Value);

        if (query.CreatedTo.HasValue)
            q = q.Where(x => x.CreatedAtUtc <= query.CreatedTo.Value);

        if (query.ResolvedFrom.HasValue)
            q = q.Where(x => x.ResolvedAtUtc.HasValue && x.ResolvedAtUtc.Value >= query.ResolvedFrom.Value);

        if (query.ResolvedTo.HasValue)
            q = q.Where(x => x.ResolvedAtUtc.HasValue && x.ResolvedAtUtc.Value <= query.ResolvedTo.Value);

        if (query.MeasuredFrom.HasValue)
            q = q.Where(x => x.OutcomeMeasuredAtUtc.HasValue && x.OutcomeMeasuredAtUtc.Value >= query.MeasuredFrom.Value);

        if (query.MeasuredTo.HasValue)
            q = q.Where(x => x.OutcomeMeasuredAtUtc.HasValue && x.OutcomeMeasuredAtUtc.Value <= query.MeasuredTo.Value);

        var items = await q.ToListAsync(ct);
        var periodMode = ResolvePeriodMode(query);
        var warningCodes = BuildSummaryWarningCodes(items, periodMode, query);

        if (items.Count == 0)
        {
            return new AnalyticsActionOutcomeSummaryDto(
                Meta: new AnalyticsActionOutcomeSummaryMetaDto(
                    Success: true,
                    PeriodMode: periodMode,
                    CreatedFrom: query.CreatedFrom,
                    CreatedTo: query.CreatedTo,
                    ResolvedFrom: query.ResolvedFrom,
                    ResolvedTo: query.ResolvedTo,
                    MeasuredFrom: query.MeasuredFrom,
                    MeasuredTo: query.MeasuredTo,
                    GeneratedAtUtc: generatedAtUtc,
                    SampleSize: 0,
                    MeasuredSampleSize: 0,
                    Warnings: warningCodes,
                    EmptyReason: "Nema akcija za izabrane filtere."
                ),
                Totals: new AnalyticsActionOutcomeSummaryTotalsDto(
                    CreatedCount: 0,
                    ClosedCount: 0,
                    OpenCount: 0,
                    MeasuredCount: 0,
                    PendingOutcomeCount: 0,
                    SuccessCount: 0,
                    NeutralCount: 0,
                    NegativeCount: 0,
                    NotMeasuredCount: 0,
                    OutcomeCoverageRate: null,
                    PositiveOutcomeRate: null,
                    NegativeOutcomeRate: null
                ),
                Impact: new AnalyticsActionOutcomeSummaryImpactDto(
                    ExpectedImpactRsd: null,
                    MeasuredImpactRsd: null,
                    RealizationRatio: null,
                    MeasuredImpactSampleCount: 0
                ),
                BySourceType: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>(),
                ByPriority: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>(),
                ByOutcomeStatus: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>(),
                ByDataQuality: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>(),
                ByConfidenceBucket: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>(),
                ByReliabilityBucket: Array.Empty<AnalyticsActionOutcomeSummaryBucketDto>()
            );
        }

        var totals = BuildSummaryAggregate("__all__", "Ukupno", items);
        return new AnalyticsActionOutcomeSummaryDto(
            Meta: new AnalyticsActionOutcomeSummaryMetaDto(
                Success: true,
                PeriodMode: periodMode,
                CreatedFrom: query.CreatedFrom,
                CreatedTo: query.CreatedTo,
                ResolvedFrom: query.ResolvedFrom,
                ResolvedTo: query.ResolvedTo,
                MeasuredFrom: query.MeasuredFrom,
                MeasuredTo: query.MeasuredTo,
                GeneratedAtUtc: generatedAtUtc,
                SampleSize: items.Count,
                MeasuredSampleSize: totals.MeasuredCount,
                Warnings: warningCodes,
                EmptyReason: null
            ),
            Totals: new AnalyticsActionOutcomeSummaryTotalsDto(
                CreatedCount: items.Count,
                ClosedCount: totals.ClosedCount,
                OpenCount: totals.TotalCount - totals.ClosedCount,
                MeasuredCount: totals.MeasuredCount,
                PendingOutcomeCount: totals.PendingOutcomeCount,
                SuccessCount: totals.SuccessCount,
                NeutralCount: totals.NeutralCount,
                NegativeCount: totals.NegativeCount,
                NotMeasuredCount: totals.NotMeasuredCount,
                OutcomeCoverageRate: totals.OutcomeCoverageRate,
                PositiveOutcomeRate: totals.PositiveOutcomeRate,
                NegativeOutcomeRate: totals.NegativeOutcomeRate
            ),
            Impact: new AnalyticsActionOutcomeSummaryImpactDto(
                ExpectedImpactRsd: totals.ExpectedImpactRsd,
                MeasuredImpactRsd: totals.MeasuredImpactRsd,
                RealizationRatio: totals.RealizationRatio,
                MeasuredImpactSampleCount: totals.MeasuredImpactSampleCount
            ),
            BySourceType: BuildGroupedBuckets(items, x => x.SourceType, x => x),
            ByPriority: BuildGroupedBuckets(items, x => x.Priority, x => x),
            ByOutcomeStatus: BuildGroupedBuckets(items, x => NormalizeOutcomeStatus(x.OutcomeStatus), GetOutcomeLabel),
            ByDataQuality: BuildGroupedBuckets(items, x => NormalizeDataQualityBucket(x.DataQualityStatus), GetDataQualityLabel),
            ByConfidenceBucket: BuildGroupedBuckets(items, x => GetPercentBucketKey(x.ConfidencePct), GetPercentBucketLabel),
            ByReliabilityBucket: BuildGroupedBuckets(items, x => GetPercentBucketKey(x.ReliabilityPct), GetPercentBucketLabel)
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
        var existing = await FindExistingOpenActionAsync(request.SourceType, request.SourceKey, ct);

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
            MetadataJson = MergeLedgerMetadata(
                request.MetadataJson,
                BuildCreationSnapshot(request),
                resolutionSnapshot: null,
                preserveExistingResolutionSnapshot: true),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
        };

        _db.AnalyticsActionItems.Add(item);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            DetachPendingEntity(item);

            if (!IsUniqueOpenActionConflict(ex))
            {
                throw;
            }

            var racedExisting = await FindExistingOpenActionAsync(request.SourceType, request.SourceKey, ct);
            if (racedExisting is not null)
            {
                _logger.LogWarning(
                    ex,
                    "AnalyticsActionItem upsert detected concurrent open action for {SourceType}/{SourceKey}. Returning existing action {Id}.",
                    request.SourceType,
                    request.SourceKey,
                    racedExisting.Id);

                return new AnalyticsActionUpsertResult(
                    Item: racedExisting,
                    Created: false,
                    Existing: true,
                    Status: racedExisting.Status,
                    SourceKey: racedExisting.SourceKey);
            }

            throw;
        }

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
        IReadOnlyCollection<AnalyticsActionSourceStatusLookupInput> inputs,
        CancellationToken ct = default)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<AnalyticsActionSourceStatusDto>();
        }

        var normalizedInputs = inputs
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceType) && !string.IsNullOrWhiteSpace(x.SourceKey))
            .Select(x => new AnalyticsActionSourceStatusLookupInput(x.SourceType.Trim(), x.SourceKey.Trim()))
            .Distinct()
            .ToArray();

        if (normalizedInputs.Length == 0)
        {
            return Array.Empty<AnalyticsActionSourceStatusDto>();
        }

        var sourceTypes = normalizedInputs
            .Select(x => x.SourceType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var sourceKeys = normalizedInputs
            .Select(x => x.SourceKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requestedPairs = normalizedInputs
            .Select(x => (x.SourceType, x.SourceKey))
            .ToHashSet();

        var candidates = await _db.AnalyticsActionItems
            .AsNoTracking()
            .Where(x => sourceTypes.Contains(x.SourceType) && sourceKeys.Contains(x.SourceKey))
            .Select(x => new
            {
                x.Id,
                x.SourceType,
                x.SourceKey,
                x.Status,
                x.OutcomeStatus,
                x.UpdatedAtUtc,
                OpenRank = IsOpenStatus(x.Status) ? 1 : 0,
            })
            .ToListAsync(ct);

        var filteredCandidates = candidates
            .Where(x => requestedPairs.Contains((x.SourceType, x.SourceKey)))
            .ToArray();

        var bestByPair = filteredCandidates
            .GroupBy(x => (x.SourceType, x.SourceKey))
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.OpenRank)
                    .ThenByDescending(x => x.UpdatedAtUtc)
                    .First());

        var items = new List<AnalyticsActionSourceStatusDto>(normalizedInputs.Length);
        foreach (var input in normalizedInputs)
        {
            if (!bestByPair.TryGetValue((input.SourceType, input.SourceKey), out var candidate))
            {
                items.Add(new AnalyticsActionSourceStatusDto(
                    SourceType: input.SourceType,
                    SourceKey: input.SourceKey,
                    Exists: false,
                    ActionId: null,
                    Status: null,
                    OutcomeStatus: null,
                    CanCreateNew: true));
                continue;
            }

            var existsOpen = IsOpenStatus(candidate.Status);
            items.Add(new AnalyticsActionSourceStatusDto(
                SourceType: input.SourceType,
                SourceKey: input.SourceKey,
                Exists: existsOpen,
                ActionId: candidate.Id,
                Status: candidate.Status,
                OutcomeStatus: candidate.OutcomeStatus,
                CanCreateNew: !existsOpen));
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

        var normalizedOutcomeNotes = NormalizeOutcomeNotes(request.OutcomeNotes);
        var oldOutcomeStatus = item.OutcomeStatus;
        var oldMeasuredImpactRsd = item.MeasuredImpactRsd;
        var oldOutcomeMeasuredAtUtc = item.OutcomeMeasuredAtUtc;
        var oldOutcomeNotes = item.OutcomeNotes;
        var now = DateTime.UtcNow;
        item.OutcomeStatus = request.OutcomeStatus;
        item.MeasuredImpactRsd = request.MeasuredImpactRsd;
        item.OutcomeMeasuredAtUtc = request.OutcomeMeasuredAtUtc ?? now;
        item.OutcomeNotes = normalizedOutcomeNotes;
        item.UpdatedAtUtc = now;
        item.UpdatedByUserId = userId;
        item.UpdatedByUserName = userName;

        if (request.OutcomeStatus is AnalyticsActionConstants.OutcomeStatuses.Pending)
        {
            item.MeasuredImpactRsd = null;
            item.OutcomeMeasuredAtUtc = null;
        }

        var preserveExistingResolutionSnapshot = HasExistingLedgerEnvelope(item.MetadataJson);
        var resolutionSnapshot = BuildResolutionSnapshot(request, normalizedOutcomeNotes, preserveExistingResolutionSnapshot);
        item.MetadataJson = MergeLedgerMetadata(
            item.MetadataJson,
            creationSnapshot: null,
            resolutionSnapshot,
            preserveExistingResolutionSnapshot);

        var outcomeChanged = !string.Equals(oldOutcomeStatus, item.OutcomeStatus, StringComparison.Ordinal)
            || oldMeasuredImpactRsd != item.MeasuredImpactRsd
            || oldOutcomeMeasuredAtUtc != item.OutcomeMeasuredAtUtc
            || !string.Equals(oldOutcomeNotes, item.OutcomeNotes, StringComparison.Ordinal);

        if (outcomeChanged)
        {
            _db.AnalyticsActionNotes.Add(new AnalyticsActionNote
            {
                ActionItemId = item.Id,
                StatusFrom = item.Status,
                StatusTo = item.Status,
                Note = BuildOutcomeAuditNote(item),
                CreatedAtUtc = now,
                CreatedByUserId = userId,
                CreatedByUserName = userName
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AnalyticsActionItem outcome updated: Id={Id} OutcomeStatus={OutcomeStatus} UserId={UserId}",
            item.Id,
            request.OutcomeStatus,
            userId);

        return item;
    }

    public static AnalyticsActionLedgerSnapshot? GetLedgerSnapshot(string? metadataJson)
    {
        if (!TryGetMetadataRoot(metadataJson, out var root))
        {
            return null;
        }

        if (root["ledger"] is not JsonObject ledger)
        {
            return null;
        }

        var schemaVersion = root["schemaVersion"]?.GetValue<int?>() ?? LedgerSchemaVersion;
        var creationSnapshot = ParseCreationSnapshot(ledger["creationSnapshot"] as JsonObject);
        var resolutionSnapshot = ParseResolutionSnapshot(ledger["resolutionSnapshot"] as JsonObject);

        return creationSnapshot is null && resolutionSnapshot is null
            ? null
            : new AnalyticsActionLedgerSnapshot(schemaVersion, creationSnapshot, resolutionSnapshot);
    }

    private static string? NormalizeOutcomeNotes(string? outcomeNotes)
    {
        if (string.IsNullOrWhiteSpace(outcomeNotes))
        {
            return null;
        }

        var trimmed = outcomeNotes.Trim();
        if (trimmed.Length > MaxOutcomeNotesLength)
        {
            throw new ArgumentException($"outcomeNotes must be {MaxOutcomeNotesLength} characters or fewer", nameof(outcomeNotes));
        }

        return trimmed;
    }

    private static string BuildOutcomeAuditNote(AnalyticsActionItem item)
    {
        var segments = new List<string>
        {
            $"Outcome: {item.OutcomeStatus ?? AnalyticsActionConstants.OutcomeStatuses.Pending}"
        };

        if (item.MeasuredImpactRsd.HasValue)
        {
            segments.Add($"MeasuredImpactRsd={item.MeasuredImpactRsd.Value:0.##}");
        }

        if (item.OutcomeMeasuredAtUtc.HasValue)
        {
            segments.Add($"MeasuredAtUtc={item.OutcomeMeasuredAtUtc.Value:O}");
        }

        if (!string.IsNullOrWhiteSpace(item.OutcomeNotes))
        {
            segments.Add($"Note={item.OutcomeNotes}");
        }

        var note = string.Join(" | ", segments);
        return note.Length <= MaxOutcomeNotesLength ? note : note[..MaxOutcomeNotesLength];
    }

    private static AnalyticsActionCreationSnapshot? BuildCreationSnapshot(AnalyticsActionUpsertRequest request)
    {
        var hasLedgerFields =
            !string.IsNullOrWhiteSpace(request.SourceRecommendationId)
            || !string.IsNullOrWhiteSpace(request.RecommendationType)
            || !string.IsNullOrWhiteSpace(request.ExpectedImpactBasis)
            || request.ImpactWindowDays.HasValue
            || !string.IsNullOrWhiteSpace(request.ConfidenceLevel)
            || HasNonEmptyValues(request.WarningCodes)
            || HasNonEmptyValues(request.PrimaryDrivers)
            || !string.IsNullOrWhiteSpace(request.DecisionReason)
            || !string.IsNullOrWhiteSpace(request.RecommendedAction)
            || request.GeneratedAtUtc.HasValue
            || !string.IsNullOrWhiteSpace(request.InputFreshnessStatus);

        if (!hasLedgerFields)
        {
            return null;
        }

        return new AnalyticsActionCreationSnapshot(
            SourceRecommendationId: request.SourceRecommendationId?.Trim() ?? string.Empty,
            RecommendationType: request.RecommendationType?.Trim() ?? string.Empty,
            ExpectedImpactBasis: NormalizeOptionalText(request.ExpectedImpactBasis),
            ImpactWindowDays: request.ImpactWindowDays,
            ConfidenceLevel: request.ConfidenceLevel?.Trim() ?? string.Empty,
            WarningCodes: NormalizeStringList(request.WarningCodes),
            PrimaryDrivers: NormalizeStringList(request.PrimaryDrivers),
            DecisionReason: request.DecisionReason?.Trim() ?? string.Empty,
            RecommendedAction: request.RecommendedAction?.Trim() ?? string.Empty,
            GeneratedAtUtc: request.GeneratedAtUtc,
            InputFreshnessStatus: request.InputFreshnessStatus?.Trim() ?? string.Empty);
    }

    private static AnalyticsActionResolutionSnapshot? BuildResolutionSnapshot(
        AnalyticsActionOutcomeUpdateRequest request,
        string? normalizedOutcomeNotes,
        bool preserveExistingResolutionSnapshot)
    {
        var hasLedgerFields =
            request.MeasuredWindowDays.HasValue
            || !string.IsNullOrWhiteSpace(request.EvidenceSource)
            || !string.IsNullOrWhiteSpace(request.EvidenceReference)
            || !string.IsNullOrWhiteSpace(request.ResolutionNote);

        if (!hasLedgerFields && !preserveExistingResolutionSnapshot)
        {
            return null;
        }

        return new AnalyticsActionResolutionSnapshot(
            MeasuredWindowDays: request.MeasuredWindowDays,
            EvidenceSource: NormalizeOptionalText(request.EvidenceSource),
            EvidenceReference: NormalizeOptionalText(request.EvidenceReference),
            ResolutionNote: NormalizeOptionalText(request.ResolutionNote) ?? normalizedOutcomeNotes);
    }

    private static string? MergeLedgerMetadata(
        string? existingMetadataJson,
        AnalyticsActionCreationSnapshot? creationSnapshot,
        AnalyticsActionResolutionSnapshot? resolutionSnapshot,
        bool preserveExistingResolutionSnapshot)
    {
        if (creationSnapshot is null && resolutionSnapshot is null && string.IsNullOrWhiteSpace(existingMetadataJson))
        {
            return existingMetadataJson;
        }

        JsonObject root;
        if (string.IsNullOrWhiteSpace(existingMetadataJson))
        {
            root = new JsonObject();
        }
        else if (!TryParseMetadataRoot(existingMetadataJson, out root))
        {
            throw new ArgumentException("metadataJson must be a valid JSON object when ledger metadata is provided", nameof(existingMetadataJson));
        }

        var changed = false;
        JsonObject? ledger = root["ledger"] as JsonObject;

        if (creationSnapshot is not null || resolutionSnapshot is not null || preserveExistingResolutionSnapshot)
        {
            root["schemaVersion"] = LedgerSchemaVersion;
            changed = true;

            ledger ??= new JsonObject();
            root["ledger"] = ledger;
        }

        if (creationSnapshot is not null)
        {
            ledger!["creationSnapshot"] = JsonSerializer.SerializeToNode(creationSnapshot, LedgerJsonOptions);
            changed = true;
        }

        if (resolutionSnapshot is not null)
        {
            ledger!["resolutionSnapshot"] = JsonSerializer.SerializeToNode(resolutionSnapshot, LedgerJsonOptions);
            changed = true;
        }

        if (!changed)
        {
            return existingMetadataJson;
        }

        return root.ToJsonString(LedgerJsonOptions);
    }

    private static bool TryParseMetadataRoot(string metadataJson, out JsonObject root)
    {
        root = null!;

        try
        {
            var parsed = JsonNode.Parse(metadataJson);
            if (parsed is JsonObject obj)
            {
                root = obj;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static bool TryGetMetadataRoot(string? metadataJson, out JsonObject root)
    {
        root = null!;
        return !string.IsNullOrWhiteSpace(metadataJson) && TryParseMetadataRoot(metadataJson, out root);
    }

    private static bool HasExistingLedgerEnvelope(string? metadataJson)
        => TryGetMetadataRoot(metadataJson, out var root) && root["ledger"] is JsonObject;

    private static AnalyticsActionCreationSnapshot? ParseCreationSnapshot(JsonObject? creationNode)
    {
        if (creationNode is null)
        {
            return null;
        }

        var sourceRecommendationId = creationNode["sourceRecommendationId"]?.GetValue<string>()?.Trim();
        var recommendationType = creationNode["recommendationType"]?.GetValue<string>()?.Trim();
        var confidenceLevel = creationNode["confidenceLevel"]?.GetValue<string>()?.Trim();
        var decisionReason = creationNode["decisionReason"]?.GetValue<string>()?.Trim();
        var recommendedAction = creationNode["recommendedAction"]?.GetValue<string>()?.Trim();
        var inputFreshnessStatus = creationNode["inputFreshnessStatus"]?.GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(sourceRecommendationId)
            || string.IsNullOrWhiteSpace(recommendationType)
            || string.IsNullOrWhiteSpace(confidenceLevel)
            || string.IsNullOrWhiteSpace(decisionReason)
            || string.IsNullOrWhiteSpace(recommendedAction)
            || string.IsNullOrWhiteSpace(inputFreshnessStatus))
        {
            return null;
        }

        return new AnalyticsActionCreationSnapshot(
            sourceRecommendationId,
            recommendationType,
            NormalizeOptionalText(creationNode["expectedImpactBasis"]?.GetValue<string>()),
            creationNode["impactWindowDays"]?.GetValue<int?>(),
            confidenceLevel,
            ReadStringArray(creationNode["warningCodes"]),
            ReadStringArray(creationNode["primaryDrivers"]),
            decisionReason,
            recommendedAction,
            creationNode["generatedAtUtc"]?.GetValue<DateTime?>(),
            inputFreshnessStatus);
    }

    private static AnalyticsActionResolutionSnapshot? ParseResolutionSnapshot(JsonObject? resolutionNode)
    {
        if (resolutionNode is null)
        {
            return null;
        }

        var snapshot = new AnalyticsActionResolutionSnapshot(
            resolutionNode["measuredWindowDays"]?.GetValue<int?>(),
            NormalizeOptionalText(resolutionNode["evidenceSource"]?.GetValue<string>()),
            NormalizeOptionalText(resolutionNode["evidenceReference"]?.GetValue<string>()),
            NormalizeOptionalText(resolutionNode["resolutionNote"]?.GetValue<string>()));

        return snapshot.MeasuredWindowDays.HasValue
            || snapshot.EvidenceSource is not null
            || snapshot.EvidenceReference is not null
            || snapshot.ResolutionNote is not null
            ? snapshot
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
        => node is JsonArray array
            ? array
                .Select(x => NormalizeOptionalText(x?.GetValue<string>()))
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values)
        => values is null
            ? Array.Empty<string>()
            : values
                .Select(NormalizeOptionalText)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    private static bool HasNonEmptyValues(IReadOnlyList<string>? values)
        => values is not null && values.Any(x => !string.IsNullOrWhiteSpace(x));

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolvePeriodMode(AnalyticsActionOutcomeSummaryQuery query)
    {
        if (query.MeasuredFrom.HasValue || query.MeasuredTo.HasValue)
            return "measured";
        if (query.ResolvedFrom.HasValue || query.ResolvedTo.HasValue)
            return "resolved";
        return "created";
    }

    private static string NormalizeOutcomeStatus(string? outcomeStatus)
    {
        if (string.IsNullOrWhiteSpace(outcomeStatus))
            return AnalyticsActionConstants.OutcomeStatuses.Pending;

        var normalized = outcomeStatus.Trim().ToLowerInvariant();
        return AnalyticsActionConstants.IsValidOutcomeStatus(normalized)
            ? normalized
            : AnalyticsActionConstants.OutcomeStatuses.Pending;
    }

    private static string NormalizeDataQualityBucket(string? dataQualityStatus)
        => AnalyticsActionConstants.NormalizeDataQualityStatus(dataQualityStatus) ?? "unknown";

    private static bool IsUniqueOpenActionConflict(DbUpdateException exception)
        => GetSqlState(exception) == PostgresErrorCodes.UniqueViolation;

    private static string? GetSqlState(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState;
            }

            var sqlStateProperty = current.GetType().GetProperty(
                "SqlState",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (sqlStateProperty?.PropertyType == typeof(string) &&
                sqlStateProperty.GetValue(current) is string sqlState &&
                !string.IsNullOrWhiteSpace(sqlState))
            {
                return sqlState;
            }
        }

        return null;
    }

    private void DetachPendingEntity(AnalyticsActionItem item)
    {
        if (_db is DbContext efDb)
        {
            efDb.Entry(item).State = EntityState.Detached;
        }
    }

    private static string GetOutcomeLabel(string key)
        => key switch
        {
            AnalyticsActionConstants.OutcomeStatuses.Pending => "Pending",
            AnalyticsActionConstants.OutcomeStatuses.Success => "Success",
            AnalyticsActionConstants.OutcomeStatuses.Neutral => "Neutral",
            AnalyticsActionConstants.OutcomeStatuses.Negative => "Negative",
            AnalyticsActionConstants.OutcomeStatuses.NotMeasured => "NotMeasured",
            _ => key
        };

    private static string GetDataQualityLabel(string key)
        => key switch
        {
            AnalyticsActionConstants.DataQualityStatuses.Good => "Good",
            AnalyticsActionConstants.DataQualityStatuses.Warning => "Warning",
            AnalyticsActionConstants.DataQualityStatuses.Critical => "Critical",
            AnalyticsActionConstants.DataQualityStatuses.InsufficientData => "InsufficientData",
            "unknown" => "Unknown",
            _ => key
        };

    private static string GetPercentBucketKey(int? value)
        => value switch
        {
            null => "unknown",
            < 50 => "lt50",
            < 70 => "50_69",
            < 85 => "70_84",
            _ => "85_plus"
        };

    private static string GetPercentBucketLabel(string key)
        => key switch
        {
            "lt50" => "<50",
            "50_69" => "50-69",
            "70_84" => "70-84",
            "85_plus" => "85+",
            "unknown" => "Unknown",
            _ => key
        };

    private static string[] BuildSummaryWarningCodes(
        IReadOnlyList<AnalyticsActionItem> items,
        string periodMode,
        AnalyticsActionOutcomeSummaryQuery query)
    {
        if (items.Count == 0)
            return Array.Empty<string>();

        var warnings = new List<string>();
        var aggregate = BuildSummaryAggregate("__warnings__", "Warnings", items);
        var hasCreatedFilters = query.CreatedFrom.HasValue || query.CreatedTo.HasValue;
        var hasResolvedFilters = query.ResolvedFrom.HasValue || query.ResolvedTo.HasValue;
        var hasMeasuredFilters = query.MeasuredFrom.HasValue || query.MeasuredTo.HasValue;
        var measuredFiltersMixed = (hasMeasuredFilters && hasCreatedFilters)
            || (hasMeasuredFilters && hasResolvedFilters)
            || (hasCreatedFilters && hasResolvedFilters);

        if (items.Count < 10)
            warnings.Add("small_sample");
        if (aggregate.MeasuredCount < 10)
            warnings.Add("small_measured_sample");
        if (aggregate.OutcomeCoverageRate.HasValue && aggregate.OutcomeCoverageRate.Value < 0.5m)
            warnings.Add("outcome_coverage_low");
        if (aggregate.MeasuredImpactSampleCount > 0 && !aggregate.ExpectedImpactRsd.HasValue)
            warnings.Add("expected_impact_denominator_missing");
        if (aggregate.MeasuredCount > 0 && aggregate.MeasuredImpactSampleCount < aggregate.MeasuredCount)
            warnings.Add("measured_impact_missing");
        if (items.Any(x => string.Equals(x.Status, AnalyticsActionConstants.Statuses.Rejected, StringComparison.Ordinal)))
            warnings.Add("rejected_actions_present");
        if (measuredFiltersMixed || periodMode == "measured" && (hasCreatedFilters || hasResolvedFilters))
            warnings.Add("mixed_period_filters");

        return warnings.ToArray();
    }

    private static AnalyticsActionOutcomeSummaryBucketDto[] BuildGroupedBuckets(
        IReadOnlyList<AnalyticsActionItem> items,
        Func<AnalyticsActionItem, string> keySelector,
        Func<string, string> labelSelector)
        => items
            .GroupBy(keySelector)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => BuildSummaryAggregate(g.Key, labelSelector(g.Key), g.ToList()))
            .ToArray();

    private static AnalyticsActionOutcomeSummaryBucketDto BuildSummaryAggregate(
        string key,
        string label,
        IReadOnlyList<AnalyticsActionItem> items)
    {
        var totalCount = items.Count;
        var closedItems = items
            .Where(x => string.Equals(x.Status, AnalyticsActionConstants.Statuses.Done, StringComparison.Ordinal)
                || string.Equals(x.Status, AnalyticsActionConstants.Statuses.Rejected, StringComparison.Ordinal))
            .ToArray();
        var closedCount = closedItems.Length;

        var normalizedOutcomes = items.Select(item => NormalizeOutcomeStatus(item.OutcomeStatus)).ToArray();
        var measuredItems = items
            .Where(x => NormalizeOutcomeStatus(x.OutcomeStatus) != AnalyticsActionConstants.OutcomeStatuses.Pending)
            .ToArray();
        var measuredCount = measuredItems.Length;
        var pendingOutcomeCount = totalCount - measuredCount;
        var successCount = normalizedOutcomes.Count(x => x == AnalyticsActionConstants.OutcomeStatuses.Success);
        var neutralCount = normalizedOutcomes.Count(x => x == AnalyticsActionConstants.OutcomeStatuses.Neutral);
        var negativeCount = normalizedOutcomes.Count(x => x == AnalyticsActionConstants.OutcomeStatuses.Negative);
        var notMeasuredCount = normalizedOutcomes.Count(x => x == AnalyticsActionConstants.OutcomeStatuses.NotMeasured);
        var measuredImpactItems = measuredItems.Where(x => x.MeasuredImpactRsd.HasValue).ToArray();
        var measuredImpactSampleCount = measuredImpactItems.Length;
        decimal? measuredImpactRsd = measuredImpactSampleCount > 0 ? measuredImpactItems.Sum(x => x.MeasuredImpactRsd!.Value) : null;
        var expectedImpactSampleItems = measuredImpactItems.Where(x => x.ExpectedImpactRsd.HasValue).ToArray();
        decimal? expectedImpactRsd = expectedImpactSampleItems.Length > 0 ? expectedImpactSampleItems.Sum(x => x.ExpectedImpactRsd!.Value) : null;
        var closedMeasuredCount = closedItems.Count(x => NormalizeOutcomeStatus(x.OutcomeStatus) != AnalyticsActionConstants.OutcomeStatuses.Pending);
        decimal? outcomeCoverageRate = closedCount > 0 ? Math.Round((decimal)closedMeasuredCount / closedCount, 4, MidpointRounding.AwayFromZero) : null;
        decimal? positiveOutcomeRate = measuredCount > 0 ? Math.Round((decimal)successCount / measuredCount, 4, MidpointRounding.AwayFromZero) : null;
        decimal? negativeOutcomeRate = measuredCount > 0 ? Math.Round((decimal)negativeCount / measuredCount, 4, MidpointRounding.AwayFromZero) : null;
        decimal? realizationRatio = null;
        if (measuredImpactRsd.HasValue && expectedImpactRsd.HasValue && expectedImpactRsd.Value > 0)
        {
            realizationRatio = Math.Round(measuredImpactRsd.Value / expectedImpactRsd.Value, 4, MidpointRounding.AwayFromZero);
        }

        var warningCodes = new List<string>();
        if (totalCount < 5)
            warningCodes.Add("small_sample");
        if (measuredCount > 0 && measuredImpactSampleCount < measuredCount)
            warningCodes.Add("measured_impact_missing");
        if (measuredImpactSampleCount > 0 && !expectedImpactRsd.HasValue)
            warningCodes.Add("expected_impact_denominator_missing");

        return new AnalyticsActionOutcomeSummaryBucketDto(
            Key: key,
            Label: label,
            TotalCount: totalCount,
            ClosedCount: closedCount,
            MeasuredCount: measuredCount,
            PendingOutcomeCount: pendingOutcomeCount,
            SuccessCount: successCount,
            NeutralCount: neutralCount,
            NegativeCount: negativeCount,
            NotMeasuredCount: notMeasuredCount,
            ExpectedImpactRsd: expectedImpactRsd,
            MeasuredImpactRsd: measuredImpactRsd,
            OutcomeCoverageRate: outcomeCoverageRate,
            PositiveOutcomeRate: positiveOutcomeRate,
            NegativeOutcomeRate: negativeOutcomeRate,
            RealizationRatio: realizationRatio,
            MeasuredImpactSampleCount: measuredImpactSampleCount,
            WarningCodes: warningCodes.ToArray()
        );
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
    string? SourceRecommendationId,
    string? RecommendationType,
    string? ExpectedImpactBasis,
    int? ImpactWindowDays,
    string? ConfidenceLevel,
    IReadOnlyList<string>? WarningCodes,
    IReadOnlyList<string>? PrimaryDrivers,
    string? DecisionReason,
    string? RecommendedAction,
    DateTime? GeneratedAtUtc,
    string? InputFreshnessStatus,
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
    string SourceType,
    string SourceKey,
    bool Exists,
    long? ActionId,
    string? Status,
    string? OutcomeStatus,
    bool CanCreateNew
);

public sealed record AnalyticsActionSourceStatusLookupInput(
    string SourceType,
    string SourceKey
);

public sealed record AnalyticsActionOutcomeUpdateRequest(
    string OutcomeStatus,
    decimal? MeasuredImpactRsd,
    DateTime? OutcomeMeasuredAtUtc,
    string? OutcomeNotes,
    int? MeasuredWindowDays,
    string? EvidenceSource,
    string? EvidenceReference,
    string? ResolutionNote
);

public sealed record AnalyticsActionOutcomeSummaryQuery(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    DateTime? ResolvedFrom,
    DateTime? ResolvedTo,
    DateTime? MeasuredFrom,
    DateTime? MeasuredTo,
    string? SourceType,
    string? Priority,
    string? DataQualityStatus
);

public sealed record AnalyticsActionOutcomeSummaryDto(
    AnalyticsActionOutcomeSummaryMetaDto Meta,
    AnalyticsActionOutcomeSummaryTotalsDto Totals,
    AnalyticsActionOutcomeSummaryImpactDto Impact,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> BySourceType,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> ByPriority,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> ByOutcomeStatus,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> ByDataQuality,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> ByConfidenceBucket,
    IReadOnlyList<AnalyticsActionOutcomeSummaryBucketDto> ByReliabilityBucket
);

public sealed record AnalyticsActionOutcomeSummaryMetaDto(
    bool Success,
    string PeriodMode,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    DateTime? ResolvedFrom,
    DateTime? ResolvedTo,
    DateTime? MeasuredFrom,
    DateTime? MeasuredTo,
    DateTime GeneratedAtUtc,
    int SampleSize,
    int MeasuredSampleSize,
    IReadOnlyList<string> Warnings,
    string? EmptyReason
);

public sealed record AnalyticsActionOutcomeSummaryTotalsDto(
    int CreatedCount,
    int ClosedCount,
    int OpenCount,
    int MeasuredCount,
    int PendingOutcomeCount,
    int SuccessCount,
    int NeutralCount,
    int NegativeCount,
    int NotMeasuredCount,
    decimal? OutcomeCoverageRate,
    decimal? PositiveOutcomeRate,
    decimal? NegativeOutcomeRate
);

public sealed record AnalyticsActionOutcomeSummaryImpactDto(
    decimal? ExpectedImpactRsd,
    decimal? MeasuredImpactRsd,
    decimal? RealizationRatio,
    int MeasuredImpactSampleCount
);

public sealed record AnalyticsActionOutcomeSummaryBucketDto(
    string Key,
    string Label,
    int TotalCount,
    int ClosedCount,
    int MeasuredCount,
    int PendingOutcomeCount,
    int SuccessCount,
    int NeutralCount,
    int NegativeCount,
    int NotMeasuredCount,
    decimal? ExpectedImpactRsd,
    decimal? MeasuredImpactRsd,
    decimal? OutcomeCoverageRate,
    decimal? PositiveOutcomeRate,
    decimal? NegativeOutcomeRate,
    decimal? RealizationRatio,
    int MeasuredImpactSampleCount,
    IReadOnlyList<string> WarningCodes
);
