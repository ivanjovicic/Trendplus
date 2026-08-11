using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class AnalyticsActionItemServiceTests
{
    [Theory]
    [InlineData(AnalyticsActionConstants.Statuses.Rejected)]
    [InlineData(AnalyticsActionConstants.Statuses.Done)]
    public async Task UpdateStatusAsync_ClosedStatuses_SetResolvedAtUtc(string closedStatus)
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_ClosedStatuses_SetResolvedAtUtc) + closedStatus);
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", $"k-{closedStatus}"), userId: "u1");

        var updated = await service.UpdateStatusAsync(created.Id, closedStatus, note: null, userId: "u1", userName: "tester");

        Assert.NotNull(updated);
        Assert.NotNull(updated!.ResolvedAtUtc);
    }

    [Theory]
    [InlineData(AnalyticsActionConstants.Statuses.New)]
    [InlineData(AnalyticsActionConstants.Statuses.Accepted)]
    [InlineData(AnalyticsActionConstants.Statuses.Deferred)]
    public async Task UpdateStatusAsync_OpenStatuses_ClearResolvedAtUtc(string openStatus)
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_OpenStatuses_ClearResolvedAtUtc) + openStatus);
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", $"k-{openStatus}"), userId: "u1");

        var closed = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Done, note: null, userId: "u1", userName: "tester");
        Assert.NotNull(closed);
        Assert.NotNull(closed!.ResolvedAtUtc);

        var reopened = await service.UpdateStatusAsync(created.Id, openStatus, note: null, userId: "u1", userName: "tester");

        Assert.NotNull(reopened);
        Assert.Null(reopened!.ResolvedAtUtc);
    }

    [Fact]
    public async Task UpdateStatusAsync_CreatesAuditNote_WhenStatusChanges_WithNote()
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_CreatesAuditNote_WhenStatusChanges_WithNote));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", "note-1"), userId: "u1");

        var updated = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, "ok", "u1", "tester");

        Assert.NotNull(updated);
        var notes = await db.AnalyticsActionNotes.Where(x => x.ActionItemId == created.Id).ToListAsync();
        Assert.Single(notes);
        Assert.Equal(AnalyticsActionConstants.Statuses.New, notes[0].StatusFrom);
        Assert.Equal(AnalyticsActionConstants.Statuses.Accepted, notes[0].StatusTo);
        Assert.Equal("ok", notes[0].Note);
    }

    [Fact]
    public async Task UpdateStatusAsync_CreatesAuditNote_WhenStatusChanges_WithoutNote()
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_CreatesAuditNote_WhenStatusChanges_WithoutNote));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", "note-2"), userId: "u1");

        var updated = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, null, "u1", "tester");

        Assert.NotNull(updated);
        var notes = await db.AnalyticsActionNotes.Where(x => x.ActionItemId == created.Id).ToListAsync();
        Assert.Single(notes);
        Assert.Null(notes[0].Note);
    }

    [Fact]
    public async Task UpdateStatusAsync_DoesNotAppendNoteToDescription()
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_DoesNotAppendNoteToDescription));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", "note-3", title: "A"), userId: "u1");
        var originalDescription = created.Description;

        var updated = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, "napomena", "u1", "tester");

        Assert.NotNull(updated);
        Assert.Equal(originalDescription, updated!.Description);
        var notes = await db.AnalyticsActionNotes.Where(x => x.ActionItemId == created.Id).ToListAsync();
        Assert.Single(notes);
        Assert.Equal("napomena", notes[0].Note);
    }

    [Fact]
    public async Task UpdateStatusAsync_Reopen_ClearsResolvedAtUtc_AndCreatesNote()
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_Reopen_ClearsResolvedAtUtc_AndCreatesNote));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", "note-4"), userId: "u1");

        var closed = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        Assert.NotNull(closed);
        Assert.NotNull(closed!.ResolvedAtUtc);

        var reopened = await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, null, "u1", "tester");
        Assert.NotNull(reopened);
        Assert.Null(reopened!.ResolvedAtUtc);

        var notes = await db.AnalyticsActionNotes
            .Where(x => x.ActionItemId == created.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, notes.Count);
        Assert.Equal(AnalyticsActionConstants.Statuses.New, notes[0].StatusFrom);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, notes[0].StatusTo);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, notes[1].StatusFrom);
        Assert.Equal(AnalyticsActionConstants.Statuses.Accepted, notes[1].StatusTo);
    }

    [Fact]
    public async Task UpsertAsync_PersistsOutcomePlanningFields()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_PersistsOutcomePlanningFields));
        var service = CreateService(db);

        var created = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "planned-1",
                dueAtUtc: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                expectedImpactRsd: 1234.56m),
            userId: "u1");

        Assert.Equal(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc), created.DueAtUtc);
        Assert.Equal(1234.56m, created.ExpectedImpactRsd);
    }

    [Fact]
    public async Task UpsertAsync_PersistsLedgerCreationSnapshotMetadata()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_PersistsLedgerCreationSnapshotMetadata));
        var service = CreateService(db);

        var created = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "ledger-create-1",
                expectedImpactRsd: 2500m,
                sourceRecommendationId: "inventory:101:replenish:2026-06",
                recommendationType: "REPLENISH",
                expectedImpactBasis: "sales_velocity + stock_risk",
                impactWindowDays: 14,
                confidenceLevel: "medium",
                warningCodes: new[] { "STALE_REFRESH", "STALE_REFRESH" },
                primaryDrivers: new[] { "sales_velocity", "stock_risk" },
                decisionReason: "Artikal ima ubrzanu prodaju.",
                recommendedAction: "Dopuni",
                generatedAtUtc: new DateTime(2026, 6, 21, 9, 0, 0, DateTimeKind.Utc),
                inputFreshnessStatus: "stale"),
            userId: "u1");

        Assert.NotNull(created.MetadataJson);
        using var doc = JsonDocument.Parse(created.MetadataJson!);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        var creation = root.GetProperty("ledger").GetProperty("creationSnapshot");
        Assert.Equal("inventory:101:replenish:2026-06", creation.GetProperty("sourceRecommendationId").GetString());
        Assert.Equal("REPLENISH", creation.GetProperty("recommendationType").GetString());
        Assert.Equal("sales_velocity + stock_risk", creation.GetProperty("expectedImpactBasis").GetString());
        Assert.Equal(14, creation.GetProperty("impactWindowDays").GetInt32());
        Assert.Equal("medium", creation.GetProperty("confidenceLevel").GetString());
        Assert.Equal("Dopuni", creation.GetProperty("recommendedAction").GetString());
        Assert.Equal("stale", creation.GetProperty("inputFreshnessStatus").GetString());
        Assert.Equal(1, creation.GetProperty("warningCodes").GetArrayLength());

        var snapshot = AnalyticsActionItemService.GetLedgerSnapshot(created.MetadataJson);
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot!.CreationSnapshot);
        Assert.Equal("REPLENISH", snapshot.CreationSnapshot!.RecommendationType);
    }

    [Fact]
    public async Task UpdateOutcomeAsync_PersistsOutcomeFields()
    {
        await using var db = CreateDbContext(nameof(UpdateOutcomeAsync_PersistsOutcomeFields));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("product", "outcome-1"), userId: "u1");

        var updated = await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 777m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Ishod potvrđen",
                EvidenceSource: "action_outcome_summary"
            ),
            userId: "u1",
            userName: "tester");

        Assert.NotNull(updated);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, updated!.OutcomeStatus);
        Assert.Equal(777m, updated.MeasuredImpactRsd);
        Assert.Equal("Ishod potvrđen", updated.OutcomeNotes);
        Assert.Equal(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), updated.OutcomeMeasuredAtUtc);
    }

    [Fact]
    public async Task UpdateOutcomeAsync_RequiresEvidenceSource_ForAuthoritativeOutcomes()
    {
        await using var db = CreateDbContext(nameof(UpdateOutcomeAsync_RequiresEvidenceSource_ForAuthoritativeOutcomes));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("product", "outcome-evidence-1"), userId: "u1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 777m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Ishod potvrdjen"),
            userId: "u1",
            userName: "tester"));

        Assert.Equal("EvidenceSource", ex.ParamName);
    }

    [Fact]
    public async Task UpdateOutcomeAsync_MergesResolutionSnapshot_WithoutOverwritingCreationSnapshot()
    {
        await using var db = CreateDbContext(nameof(UpdateOutcomeAsync_MergesResolutionSnapshot_WithoutOverwritingCreationSnapshot));
        var service = CreateService(db);
        var created = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Product,
                "ledger-outcome-1",
                expectedImpactRsd: 900m,
                sourceRecommendationId: "product:1001:markdown:2026-06",
                recommendationType: "MARKDOWN",
                expectedImpactBasis: "margin + aging_stock",
                confidenceLevel: "low",
                decisionReason: "Zaliha sporo izlazi.",
                recommendedAction: "Smanji cenu",
                generatedAtUtc: new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc),
                inputFreshnessStatus: "fresh"),
            userId: "u1");

        var updated = await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 777m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Potvrđen rezultat",
                MeasuredWindowDays: 7,
                EvidenceSource: "action_outcome_summary",
                EvidenceReference: "summary:product:1001:2026-06-28",
                ResolutionNote: null),
            userId: "u1",
            userName: "tester");

        Assert.NotNull(updated);
        Assert.NotNull(updated!.MetadataJson);

        var snapshot = AnalyticsActionItemService.GetLedgerSnapshot(updated.MetadataJson);
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot!.CreationSnapshot);
        Assert.NotNull(snapshot.ResolutionSnapshot);
        Assert.Equal("product:1001:markdown:2026-06", snapshot.CreationSnapshot!.SourceRecommendationId);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, snapshot.ResolutionSnapshot!.OutcomeStatus);
        Assert.Equal(777m, snapshot.ResolutionSnapshot.MeasuredImpactRsd);
        Assert.Equal(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc), snapshot.ResolutionSnapshot.OutcomeMeasuredAtUtc);
        Assert.Equal(7, snapshot.ResolutionSnapshot.MeasuredWindowDays);
        Assert.Equal("action_outcome_summary", snapshot.ResolutionSnapshot.EvidenceSource);
        Assert.Equal("summary:product:1001:2026-06-28", snapshot.ResolutionSnapshot.EvidenceReference);
        Assert.Equal("Potvrđen rezultat", snapshot.ResolutionSnapshot.ResolutionNote);
    }

    [Fact]
    public async Task UpdateOutcomeAsync_NotMeasuredClearsMeasuredFields_AndMeasuredDateFilterSkipsIt()
    {
        await using var db = CreateDbContext(nameof(UpdateOutcomeAsync_NotMeasuredClearsMeasuredFields_AndMeasuredDateFilterSkipsIt));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("product", "outcome-not-measured-1", expectedImpactRsd: 500m), userId: "u1");

        await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 120m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Ishod je potvrdjen.",
                EvidenceSource: "action_outcome_summary"),
            userId: "u1",
            userName: "tester");

        var updated = await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                MeasuredImpactRsd: 999m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Vise nema dovoljnog dokaza.",
                EvidenceSource: "manual_review"),
            userId: "u1",
            userName: "tester");

        Assert.NotNull(updated);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.NotMeasured, updated!.OutcomeStatus);
        Assert.Null(updated.MeasuredImpactRsd);
        Assert.Null(updated.OutcomeMeasuredAtUtc);

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            MeasuredTo: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal(0, summary.Meta.SampleSize);
        Assert.Equal("Nema akcija za izabrane filtere.", summary.Meta.EmptyReason);
        Assert.Equal(0, summary.Totals.MeasuredCount);
        Assert.Equal(0, summary.Totals.MeasuredOutcomeCount);
    }

    [Fact]
    public async Task UpdateOutcomeAsync_NotMeasuredClearsEvidenceFieldsFromLedgerSnapshot()
    {
        await using var db = CreateDbContext(nameof(UpdateOutcomeAsync_NotMeasuredClearsEvidenceFieldsFromLedgerSnapshot));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("product", "outcome-not-measured-2", expectedImpactRsd: 500m), userId: "u1");

        await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 120m,
                OutcomeMeasuredAtUtc: new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc),
                OutcomeNotes: "Ishod je potvrdjen.",
                EvidenceSource: "action_outcome_summary",
                EvidenceReference: "summary:product:1001:2026-06-18"),
            userId: "u1",
            userName: "tester");

        var updated = await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                MeasuredImpactRsd: null,
                OutcomeMeasuredAtUtc: null,
                OutcomeNotes: "Vise nema dovoljnog dokaza.",
                EvidenceSource: "manual_review",
                EvidenceReference: "manual:review",
                ResolutionNote: "Dokaz nije dovoljan."),
            userId: "u1",
            userName: "tester");

        var clearedSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(updated!.MetadataJson);
        Assert.NotNull(clearedSnapshot?.ResolutionSnapshot);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.NotMeasured, clearedSnapshot!.ResolutionSnapshot!.OutcomeStatus);
        Assert.Null(clearedSnapshot.ResolutionSnapshot.MeasuredImpactRsd);
        Assert.Null(clearedSnapshot.ResolutionSnapshot.OutcomeMeasuredAtUtc);
        Assert.Null(clearedSnapshot.ResolutionSnapshot.EvidenceSource);
        Assert.Null(clearedSnapshot.ResolutionSnapshot.EvidenceReference);
        Assert.Equal("Dokaz nije dovoljan.", clearedSnapshot.ResolutionSnapshot.ResolutionNote);
    }

    [Fact]
    public void GetLedgerSnapshot_LegacyMetadataWithoutLedgerEnvelope_ReturnsNull()
    {
        var snapshot = AnalyticsActionItemService.GetLedgerSnapshot("""{"source":"legacy","note":"plain metadata only"}""");
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_UsesClosedDenominator_AndPendingIsNotFailure()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_UsesClosedDenominator_AndPendingIsNotFailure));
        var service = CreateService(db);

        var doneSuccess = await service.UpsertAsync(CreateRequest("inventory", "summary-1", expectedImpactRsd: 200m), userId: "u1");
        await service.UpdateStatusAsync(doneSuccess.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(doneSuccess.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                100m,
                new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                "pozitivno",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var donePending = await service.UpsertAsync(CreateRequest("inventory", "summary-2", expectedImpactRsd: 150m), userId: "u1");
        await service.UpdateStatusAsync(donePending.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");

        var rejectedNegative = await service.UpsertAsync(CreateRequest("supplier", "summary-3", expectedImpactRsd: 100m), userId: "u1");
        await service.UpdateStatusAsync(rejectedNegative.Id, AnalyticsActionConstants.Statuses.Rejected, null, "u1", "tester");
        await service.UpdateOutcomeAsync(rejectedNegative.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                50m,
                new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
                "negativno",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var openMeasured = await service.UpsertAsync(CreateRequest("product", "summary-4", expectedImpactRsd: 60m), userId: "u1");
        await service.UpdateOutcomeAsync(openMeasured.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                30m,
                new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
                "mereno dok je otvoreno",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal(4, summary.Totals.CreatedCount);
        Assert.Equal(3, summary.Totals.ClosedCount);
        Assert.Equal(1, summary.Totals.OpenCount);
        Assert.Equal(3, summary.Totals.MeasuredCount);
        Assert.Equal(3, summary.Totals.MeasuredOutcomeCount);
        Assert.Equal(1, summary.Totals.PendingOutcomeCount);
        Assert.Equal(2, summary.Totals.SuccessCount);
        Assert.Equal(1, summary.Totals.NegativeCount);
        Assert.Equal(0.6667m, summary.Totals.OutcomeCoverageRate);
        Assert.Equal(summary.Totals.OutcomeCoverageRate, summary.Totals.ClosedOutcomeCoverageRate);
        Assert.Equal(0.6667m, summary.Totals.PositiveOutcomeRate);
        Assert.Equal(summary.Totals.PositiveOutcomeRate, summary.Totals.MeasuredPositiveOutcomeRate);
        Assert.Equal(0.3333m, summary.Totals.NegativeOutcomeRate);
        Assert.Equal(summary.Totals.NegativeOutcomeRate, summary.Totals.MeasuredNegativeOutcomeRate);
        Assert.Equal(360m, summary.Impact.ExpectedImpactRsd);
        Assert.Equal(180m, summary.Impact.MeasuredImpactRsd);
        Assert.Equal(0.5000m, summary.Impact.RealizationRatio);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_ReturnsNullRates_WhenDenominatorIsMissing()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_ReturnsNullRates_WhenDenominatorIsMissing));
        var service = CreateService(db);

        var openPending = await service.UpsertAsync(CreateRequest("inventory", "summary-null-1", expectedImpactRsd: 100m), userId: "u1");
        await service.UpdateOutcomeAsync(openPending.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Pending,
                null,
                null,
                null),
            "u1",
            "tester");

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal(0, summary.Totals.ClosedCount);
        Assert.Null(summary.Totals.OutcomeCoverageRate);
        Assert.Null(summary.Totals.PositiveOutcomeRate);
        Assert.Null(summary.Totals.NegativeOutcomeRate);
        Assert.Null(summary.Impact.RealizationRatio);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_FiltersByResolvedWindow_AndDoesNotTreatMeasuredDateAsResolvedDate()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_FiltersByResolvedWindow_AndDoesNotTreatMeasuredDateAsResolvedDate));
        var service = CreateService(db);

        var resolvedInRangeDone = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "resolved-range-1",
                expectedImpactRsd: 200m,
                priority: AnalyticsActionConstants.Priorities.P1,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Good),
            userId: "u1");
        await service.UpdateStatusAsync(resolvedInRangeDone.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            resolvedInRangeDone.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                120m,
                new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc),
                "izmereno kasnije",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");
        await SetResolvedAtUtcAsync(db, resolvedInRangeDone.Id, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var resolvedInRangeRejected = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Supplier,
                "resolved-range-2",
                expectedImpactRsd: 300m,
                priority: AnalyticsActionConstants.Priorities.P2,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning),
            userId: "u1");
        await service.UpdateStatusAsync(resolvedInRangeRejected.Id, AnalyticsActionConstants.Statuses.Rejected, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            resolvedInRangeRejected.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                30m,
                new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
                "odbijeno sa negativnim efektom",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");
        await SetResolvedAtUtcAsync(db, resolvedInRangeRejected.Id, new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc));

        var resolvedBeforeRange = await service.UpsertAsync(
            CreateRequest(AnalyticsActionConstants.SourceTypes.Product, "resolved-range-3", expectedImpactRsd: 180m),
            userId: "u1");
        await service.UpdateStatusAsync(resolvedBeforeRange.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            resolvedBeforeRange.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                60m,
                new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
                "zatvoreno pre opsega",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");
        await SetResolvedAtUtcAsync(db, resolvedBeforeRange.Id, new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var resolvedAfterRange = await service.UpsertAsync(
            CreateRequest(AnalyticsActionConstants.SourceTypes.Inventory, "resolved-range-4", expectedImpactRsd: 90m),
            userId: "u1");
        await service.UpdateStatusAsync(resolvedAfterRange.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            resolvedAfterRange.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                10m,
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                "zatvoreno posle opsega",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");
        await SetResolvedAtUtcAsync(db, resolvedAfterRange.Id, new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc));

        var unresolvedMeasured = await service.UpsertAsync(
            CreateRequest(AnalyticsActionConstants.SourceTypes.Inventory, "resolved-range-5", expectedImpactRsd: 110m),
            userId: "u1");
        await service.UpdateOutcomeAsync(
            unresolvedMeasured.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                40m,
                new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
                "izmereno bez zatvaranja",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            ResolvedTo: new DateTime(2026, 6, 20, 23, 59, 59, DateTimeKind.Utc),
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal("resolved", summary.Meta.PeriodMode);
        Assert.Equal(2, summary.Meta.SampleSize);
        Assert.Equal(2, summary.Totals.CreatedCount);
        Assert.Equal(2, summary.Totals.ClosedCount);
        Assert.Equal(0, summary.Totals.OpenCount);
        Assert.Equal(2, summary.Totals.MeasuredCount);
        Assert.Equal(0, summary.Totals.PendingOutcomeCount);
        Assert.Equal(1, summary.Totals.SuccessCount);
        Assert.Equal(1, summary.Totals.NegativeCount);
        Assert.Equal(1.0000m, summary.Totals.OutcomeCoverageRate);
        Assert.Equal(500m, summary.Impact.ExpectedImpactRsd);
        Assert.Equal(150m, summary.Impact.MeasuredImpactRsd);
        Assert.Equal(0.3000m, summary.Impact.RealizationRatio);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_ReturnsSupportedCohortBuckets_AndKeepsUnknownImpactNull()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_ReturnsSupportedCohortBuckets_AndKeepsUnknownImpactNull));
        var service = CreateService(db);

        var inventorySuccess = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "bucket-1",
                priority: AnalyticsActionConstants.Priorities.P1,
                expectedImpactRsd: 200m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Good),
            userId: "u1");
        await service.UpdateStatusAsync(inventorySuccess.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            inventorySuccess.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                100m,
                new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                "uspeh",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var supplierRejectedNegative = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Supplier,
                "bucket-2",
                priority: AnalyticsActionConstants.Priorities.P2,
                expectedImpactRsd: 300m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning),
            userId: "u1");
        await service.UpdateStatusAsync(supplierRejectedNegative.Id, AnalyticsActionConstants.Statuses.Rejected, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            supplierRejectedNegative.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                null,
                new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
                "negativan bez iznosa",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var productPending = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Product,
                "bucket-3",
                priority: AnalyticsActionConstants.Priorities.P3,
                expectedImpactRsd: 150m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Critical),
            userId: "u1");
        await service.UpdateStatusAsync(productPending.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");

        var inventoryNotMeasured = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "bucket-4",
                priority: AnalyticsActionConstants.Priorities.P2,
                expectedImpactRsd: 120m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.InsufficientData),
            userId: "u1");
        await service.UpdateOutcomeAsync(
            inventoryNotMeasured.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                null,
                null,
                "nije izmereno"),
            "u1",
            "tester");

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Contains("rejected_actions_present", summary.Meta.Warnings);

        var inventoryBucket = Assert.Single(summary.BySourceType.Where(x => x.Key == AnalyticsActionConstants.SourceTypes.Inventory));
        Assert.Equal(2, inventoryBucket.TotalCount);
        Assert.Equal(1, inventoryBucket.SuccessCount);
        Assert.Equal(1, inventoryBucket.NotMeasuredCount);

        var p2Bucket = Assert.Single(summary.ByPriority.Where(x => x.Key == AnalyticsActionConstants.Priorities.P2));
        Assert.Equal(2, p2Bucket.TotalCount);
        Assert.Equal(2, p2Bucket.MeasuredCount);
        Assert.Equal(0, p2Bucket.PendingOutcomeCount);
        Assert.Null(p2Bucket.MeasuredImpactRsd);
        Assert.Equal(0, p2Bucket.MeasuredImpactSampleCount);

        var pendingBucket = Assert.Single(summary.ByOutcomeStatus.Where(x => x.Key == AnalyticsActionConstants.OutcomeStatuses.Pending));
        Assert.Equal(1, pendingBucket.TotalCount);
        Assert.Equal(1, pendingBucket.ClosedCount);
        Assert.Equal(0, pendingBucket.MeasuredCount);
        Assert.Equal(1, pendingBucket.PendingOutcomeCount);
        Assert.Null(pendingBucket.PositiveOutcomeRate);
        Assert.Null(pendingBucket.NegativeOutcomeRate);

        var warningBucket = Assert.Single(summary.ByDataQuality.Where(x => x.Key == AnalyticsActionConstants.DataQualityStatuses.Warning));
        Assert.Equal(1, warningBucket.TotalCount);
        Assert.Equal(1, warningBucket.NegativeCount);
        Assert.Null(warningBucket.MeasuredImpactRsd);
        Assert.Equal(0, warningBucket.MeasuredImpactSampleCount);

        Assert.Contains(summary.ByDataQuality, x => x.Key == AnalyticsActionConstants.DataQualityStatuses.Good);
        Assert.Contains(summary.ByDataQuality, x => x.Key == AnalyticsActionConstants.DataQualityStatuses.Critical);
        Assert.Contains(summary.ByDataQuality, x => x.Key == AnalyticsActionConstants.DataQualityStatuses.InsufficientData);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_AddsMixedPeriodWarning_OnlyWhenPeriodsAreCombined()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_AddsMixedPeriodWarning_OnlyWhenPeriodsAreCombined));
        var service = CreateService(db);

        var action = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "mixed-period-1",
                expectedImpactRsd: 200m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Good),
            userId: "u1");
        await service.UpdateStatusAsync(action.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            action.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                90m,
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                "izmereno",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");
        await SetResolvedAtUtcAsync(db, action.Id, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var mixedSummary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            MeasuredTo: new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        var measuredOnlySummary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            MeasuredTo: new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal("measured", mixedSummary.Meta.PeriodMode);
        Assert.Contains("mixed_period_filters", mixedSummary.Meta.Warnings);
        Assert.DoesNotContain("mixed_period_filters", measuredOnlySummary.Meta.Warnings);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_AddsCoverageAndMissingImpactWarnings_WhenClosedCoverageIsLow()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_AddsCoverageAndMissingImpactWarnings_WhenClosedCoverageIsLow));
        var service = CreateService(db);

        var measuredSuccess = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "warning-coverage-1",
                expectedImpactRsd: 200m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Good),
            userId: "u1");
        await service.UpdateStatusAsync(measuredSuccess.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            measuredSuccess.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                90m,
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                "pozitivno",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var measuredWithoutImpact = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Supplier,
                "warning-coverage-2",
                expectedImpactRsd: 150m,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning),
            userId: "u1");
        await service.UpdateStatusAsync(measuredWithoutImpact.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            measuredWithoutImpact.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                null,
                new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
                "negativno bez iznosa",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        for (var index = 0; index < 3; index++)
        {
            var pending = await service.UpsertAsync(
                CreateRequest(AnalyticsActionConstants.SourceTypes.Product, $"warning-coverage-pending-{index}", expectedImpactRsd: 80m + index),
                userId: "u1");
            await service.UpdateStatusAsync(pending.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        }

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal(5, summary.Totals.ClosedCount);
        Assert.Equal(2, summary.Totals.MeasuredCount);
        Assert.Equal(0.4000m, summary.Totals.OutcomeCoverageRate);
        Assert.Equal(1, summary.Impact.MeasuredImpactSampleCount);
        Assert.Contains("outcome_coverage_low", summary.Meta.Warnings);
        Assert.Contains("measured_impact_missing", summary.Meta.Warnings);
        Assert.DoesNotContain("expected_impact_denominator_missing", summary.Meta.Warnings);
    }

    [Fact]
    public async Task GetOutcomeSummaryAsync_AddsExpectedImpactDenominatorWarning_WhenMeasuredImpactExistsWithoutExpectedImpact()
    {
        await using var db = CreateDbContext(nameof(GetOutcomeSummaryAsync_AddsExpectedImpactDenominatorWarning_WhenMeasuredImpactExistsWithoutExpectedImpact));
        var service = CreateService(db);

        var measuredWithoutExpected = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "warning-expected-impact-1",
                expectedImpactRsd: null,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Good),
            userId: "u1");
        await service.UpdateStatusAsync(measuredWithoutExpected.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            measuredWithoutExpected.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Success,
                120m,
                new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc),
                "izmereno bez plana",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var secondMeasuredWithoutExpected = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Supplier,
                "warning-expected-impact-2",
                expectedImpactRsd: null,
                dataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning),
            userId: "u1");
        await service.UpdateStatusAsync(secondMeasuredWithoutExpected.Id, AnalyticsActionConstants.Statuses.Done, null, "u1", "tester");
        await service.UpdateOutcomeAsync(
            secondMeasuredWithoutExpected.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                AnalyticsActionConstants.OutcomeStatuses.Negative,
                40m,
                new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc),
                "izmereno bez denominatora",
                EvidenceSource: "action_outcome_summary"
            ),
            "u1",
            "tester");

        var summary = await service.GetOutcomeSummaryAsync(new AnalyticsActionOutcomeSummaryQuery(
            CreatedFrom: null,
            CreatedTo: null,
            ResolvedFrom: null,
            ResolvedTo: null,
            MeasuredFrom: null,
            MeasuredTo: null,
            SourceType: null,
            Priority: null,
            DataQualityStatus: null));

        Assert.Equal(2, summary.Totals.MeasuredCount);
        Assert.Equal(2, summary.Impact.MeasuredImpactSampleCount);
        Assert.Null(summary.Impact.ExpectedImpactRsd);
        Assert.Null(summary.Impact.RealizationRatio);
        Assert.Contains("expected_impact_denominator_missing", summary.Meta.Warnings);
        Assert.DoesNotContain("measured_impact_missing", summary.Meta.Warnings);
    }

    [Fact]
    public async Task ProjectTimeline_ReadsSnapshotHistory_InChronologicalOrder()
    {
        await using var db = CreateDbContext(nameof(ProjectTimeline_ReadsSnapshotHistory_InChronologicalOrder));
        var service = CreateService(db);

        var created = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Inventory,
                "timeline-happy-1",
                expectedImpactRsd: 500m,
                sourceRecommendationId: "inventory:timeline-happy-1:replenish:2026-06-01",
                recommendationType: "REPLENISH",
                expectedImpactBasis: "sales_velocity + stock_risk",
                impactWindowDays: 14,
                confidenceLevel: "medium",
                warningCodes: new[] { "STOCK_RISK" },
                primaryDrivers: new[] { "sales_velocity", "stock_risk" },
                decisionReason: "Brz rast prodaje.",
                recommendedAction: "Dopuni",
                generatedAtUtc: new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                inputFreshnessStatus: "fresh"),
            userId: "u1");

        await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, "Prihvaceno", "u1", "tester");
        await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Done, "Zavrseno", "u1", "tester");
        var measuredAtUtc = DateTime.UtcNow.AddMinutes(5);
        await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.Success,
                MeasuredImpactRsd: 180m,
                OutcomeMeasuredAtUtc: measuredAtUtc,
                OutcomeNotes: "Rezultat je potvrden.",
                MeasuredWindowDays: 14,
                EvidenceSource: "action_outcome_summary",
                EvidenceReference: "summary:timeline-happy-1:measured",
                ResolutionNote: "Prodaja je porasla nakon dopune."),
            userId: "u1",
            userName: "tester");

        var item = await service.GetByIdAsync(created.Id, includeNotes: true);
        Assert.NotNull(item);
        item!.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(item.MetadataJson);

        var projection = AnalyticsActionTimelineProjection.Project(item);

        Assert.Equal(AnalyticsActionConstants.Statuses.Done, projection.ProjectionState);
        Assert.Equal("inventory:timeline-happy-1:replenish:2026-06-01", projection.SourceRecommendationId);
        Assert.Equal("metadata.sourceRecommendationId", projection.SourceRecommendationIdDerivation);
        Assert.Equal(projection.SourceRecommendationId, projection.CorrelationId);
        Assert.Equal(new[]
        {
            "recommendation_issued",
            "action_accepted",
            "action_executed",
            "outcome_measured"
        }, projection.Events.Select(x => x.EventType).ToArray());
        Assert.Empty(projection.Gaps);
        Assert.Equal("action_outcome_summary", projection.Events[3].EvidenceSource);
        Assert.Equal("summary:timeline-happy-1:measured", projection.Events[3].EvidenceReference);
        Assert.Equal(14, projection.Events[3].MeasurementWindowDays);
        Assert.Equal(measuredAtUtc, projection.Events[3].OccurredAtUtc);
    }

    [Fact]
    public async Task ProjectTimeline_LeavesMissingStagesExplicit_AndKeepsNotMeasuredDistinct()
    {
        await using var db = CreateDbContext(nameof(ProjectTimeline_LeavesMissingStagesExplicit_AndKeepsNotMeasuredDistinct));
        var service = CreateService(db);

        var created = await service.UpsertAsync(
            CreateRequest(
                AnalyticsActionConstants.SourceTypes.Product,
                "timeline-gap-1",
                expectedImpactRsd: 300m,
                recommendationType: "REPLENISH",
                sourceRecommendationId: null,
                expectedImpactBasis: "sales_velocity + stock_risk",
                impactWindowDays: 14,
                confidenceLevel: "low",
                warningCodes: new[] { "STALE_REFRESH" },
                primaryDrivers: new[] { "sales_velocity" },
                decisionReason: "Sporiji ciklus prodaje.",
                recommendedAction: "Dopuni",
                generatedAtUtc: new DateTime(2026, 6, 2, 8, 30, 0, DateTimeKind.Utc),
                inputFreshnessStatus: "stale"),
            userId: "u1");

        await service.UpdateStatusAsync(created.Id, AnalyticsActionConstants.Statuses.Accepted, "Prihvaceno bez izvrsenja", "u1", "tester");
        await service.UpdateOutcomeAsync(
            created.Id,
            new AnalyticsActionOutcomeUpdateRequest(
                OutcomeStatus: AnalyticsActionConstants.OutcomeStatuses.NotMeasured,
                MeasuredImpactRsd: null,
                OutcomeMeasuredAtUtc: null,
                OutcomeNotes: "Nema dovoljno dokaza.",
                EvidenceSource: null,
                EvidenceReference: null,
                ResolutionNote: "Nedostaje dokaz."),
            userId: "u1",
            userName: "tester");

        var item = await service.GetByIdAsync(created.Id, includeNotes: true);
        Assert.NotNull(item);
        item!.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(item.MetadataJson);

        var projection = AnalyticsActionTimelineProjection.Project(item);

        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.NotMeasured, projection.ProjectionState);
        Assert.Equal("derived.sourceType.sourceKey.recommendationType.issuedAtUtc", projection.SourceRecommendationIdDerivation);
        Assert.Equal("product:timeline-gap-1:REPLENISH:20260602T083000Z", projection.SourceRecommendationId);
        Assert.Equal(new[]
        {
            "recommendation_issued",
            "action_accepted",
            "outcome_not_measured"
        }, projection.Events.Select(x => x.EventType).ToArray());
        Assert.Contains(projection.Gaps, gap => gap.GapReason == "no_execution_proof");
        Assert.Contains(projection.Gaps, gap => gap.GapReason == "no_measurement_evidence");
        Assert.DoesNotContain(projection.Gaps, gap => gap.GapReason == "no_acceptance_record");
        Assert.Equal("outcome_not_measured", projection.Events[2].EventType);
    }

    [Fact]
    public async Task UpsertAsync_SameSourceWhileOpen_ReturnsExistingAction()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_SameSourceWhileOpen_ReturnsExistingAction));
        var service = CreateService(db);
        var sourceType = AnalyticsActionConstants.SourceTypes.Inventory;
        var sourceKey = "dup-open";

        var first = await service.UpsertAsync(CreateRequest(sourceType, sourceKey, title: "Prvi"), userId: "u1");
        var second = await service.UpsertAsync(CreateRequest(sourceType, sourceKey, title: "Drugi"), userId: "u2");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.AnalyticsActionItems.CountAsync());
    }

    [Fact]
    public async Task UpsertWithResultAsync_SameSourceWhileOpen_ReturnsExistingFlag()
    {
        await using var db = CreateDbContext(nameof(UpsertWithResultAsync_SameSourceWhileOpen_ReturnsExistingFlag));
        var service = CreateService(db);
        var sourceType = AnalyticsActionConstants.SourceTypes.Inventory;
        var sourceKey = "dup-open-flags";

        var first = await service.UpsertWithResultAsync(CreateRequest(sourceType, sourceKey, title: "Prvi"), userId: "u1");
        var second = await service.UpsertWithResultAsync(CreateRequest(sourceType, sourceKey, title: "Drugi"), userId: "u2");

        Assert.True(first.Created);
        Assert.False(first.Existing);
        Assert.False(second.Created);
        Assert.True(second.Existing);
        Assert.Equal(first.Item.Id, second.Item.Id);
        Assert.Equal(sourceKey, second.SourceKey);
        Assert.Equal(1, await db.AnalyticsActionItems.CountAsync());
    }

    [Fact]
    public async Task UpsertWithResultAsync_WhenConcurrentInsertWins_ReturnsExistingInsteadOfThrowing()
    {
        var databaseName = nameof(UpsertWithResultAsync_WhenConcurrentInsertWins_ReturnsExistingInsteadOfThrowing);
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using var db = new RaceOnSaveAnalyticsDbContext(options);
        var service = CreateService(db);

        var result = await service.UpsertWithResultAsync(
            CreateRequest(AnalyticsActionConstants.SourceTypes.Product, "race-key", title: "Race title"),
            userId: "u1");

        Assert.False(result.Created);
        Assert.True(result.Existing);
        Assert.Equal("race-key", result.SourceKey);
        Assert.Equal("race-key", result.Item.SourceKey);
        Assert.Equal(1, await db.AnalyticsActionItems.CountAsync());
    }

    [Fact]
    public async Task UpsertWithResultAsync_WhenDbUpdateExceptionIsUnrelated_Rethrows()
    {
        var databaseName = nameof(UpsertWithResultAsync_WhenDbUpdateExceptionIsUnrelated_Rethrows);
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using var db = new NonUniqueFailureAnalyticsDbContext(options);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.UpsertWithResultAsync(CreateRequest(AnalyticsActionConstants.SourceTypes.Inventory, "unrelated-db-error"), userId: "u1"));

        Assert.Contains("Simulated unrelated database failure", ex.Message);
        Assert.Empty(await db.AnalyticsActionItems.ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_SameSourceKeyDifferentSourceType_DoesNotCollide()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_SameSourceKeyDifferentSourceType_DoesNotCollide));
        var service = CreateService(db);
        const string sourceKey = "same-key";

        var inventory = await service.UpsertAsync(CreateRequest(AnalyticsActionConstants.SourceTypes.Inventory, sourceKey, title: "Inventory"), userId: "u1");
        var supplier = await service.UpsertAsync(CreateRequest(AnalyticsActionConstants.SourceTypes.Supplier, sourceKey, title: "Supplier"), userId: "u1");

        Assert.NotEqual(inventory.Id, supplier.Id);
        Assert.Equal(2, await db.AnalyticsActionItems.CountAsync());
    }

    [Theory]
    [InlineData(AnalyticsActionConstants.Statuses.Rejected)]
    [InlineData(AnalyticsActionConstants.Statuses.Done)]
    public async Task UpsertAsync_AfterClosedStatus_CreatesNewAction(string closedStatus)
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_AfterClosedStatus_CreatesNewAction) + closedStatus);
        var service = CreateService(db);
        var sourceType = AnalyticsActionConstants.SourceTypes.Inventory;
        var sourceKey = "dup-closed";

        var first = await service.UpsertAsync(CreateRequest(sourceType, sourceKey), userId: "u1");
        await service.UpdateStatusAsync(first.Id, closedStatus, note: null, userId: "u1", userName: "tester");

        var second = await service.UpsertAsync(CreateRequest(sourceType, sourceKey, title: "Nova"), userId: "u2");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.AnalyticsActionItems.CountAsync());
    }

    [Fact]
    public async Task UpsertAsync_InvalidSourceType_Throws()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_InvalidSourceType_Throws));
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(CreateRequest("invalid_source", "k1"), userId: "u1"));
    }

    [Fact]
    public async Task UpsertAsync_InvalidPriority_Throws()
    {
        await using var db = CreateDbContext(nameof(UpsertAsync_InvalidPriority_Throws));
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(CreateRequest("inventory", "k1", priority: "P9"), userId: "u1"));
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidStatus_Throws()
    {
        await using var db = CreateDbContext(nameof(UpdateStatusAsync_InvalidStatus_Throws));
        var service = CreateService(db);
        var created = await service.UpsertAsync(CreateRequest("inventory", "k1"), userId: "u1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateStatusAsync(created.Id, "invalid_status", note: null, userId: "u1", userName: "tester"));
    }

    [Fact]
    public async Task GetSourceStatusesAsync_ReturnsExistsForMultipleKeys()
    {
        await using var db = CreateDbContext(nameof(GetSourceStatusesAsync_ReturnsExistsForMultipleKeys));
        var service = CreateService(db);

        await service.UpsertAsync(CreateRequest("inventory", "exists-open"), userId: "u1");

        var closed = await service.UpsertAsync(CreateRequest("inventory", "exists-closed"), userId: "u1");
        await service.UpdateStatusAsync(closed.Id, AnalyticsActionConstants.Statuses.Done, note: null, userId: "u1", userName: "tester");

        var statuses = await service.GetSourceStatusesAsync(new[]
        {
            new AnalyticsActionSourceStatusLookupInput("inventory", "exists-open"),
            new AnalyticsActionSourceStatusLookupInput("inventory", "exists-closed"),
            new AnalyticsActionSourceStatusLookupInput("inventory", "missing-key"),
        });

        var open = statuses.Single(x => x.SourceType == "inventory" && x.SourceKey == "exists-open");
        Assert.True(open.Exists);
        Assert.Equal(AnalyticsActionConstants.Statuses.New, open.Status);
        Assert.NotNull(open.ActionId);
        Assert.False(open.CanCreateNew);

        var closedStatus = statuses.Single(x => x.SourceType == "inventory" && x.SourceKey == "exists-closed");
        Assert.False(closedStatus.Exists);
        Assert.Equal(AnalyticsActionConstants.Statuses.Done, closedStatus.Status);
        Assert.NotNull(closedStatus.ActionId);
        Assert.True(closedStatus.CanCreateNew);

        var missing = statuses.Single(x => x.SourceType == "inventory" && x.SourceKey == "missing-key");
        Assert.False(missing.Exists);
        Assert.Null(missing.Status);
        Assert.Null(missing.ActionId);
        Assert.True(missing.CanCreateNew);
    }

    [Fact]
    public void AnalyticsAction_ModelHasUniqueOpenSourceIndex()
    {
        using var db = CreateDbContext(nameof(AnalyticsAction_ModelHasUniqueOpenSourceIndex));
        var entityType = db.Model.FindEntityType(typeof(Domain.Model.Analytics.AnalyticsActionItem));
        Assert.NotNull(entityType);

        var hasUniqueOpenIndex = entityType!
            .GetIndexes()
            .Any(index =>
                index.IsUnique
                && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceType", "SourceKey" })
                && (index.GetFilter()?.Contains("new") ?? false));

        Assert.True(hasUniqueOpenIndex);
    }

    private static AnalyticsDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AnalyticsDbContext(options);
    }

    private sealed class RaceOnSaveAnalyticsDbContext : AnalyticsDbContext
    {
        private readonly DbContextOptions<AnalyticsDbContext> _options;
        private bool _raceInjected;

        public RaceOnSaveAnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pendingAction = ChangeTracker
                .Entries<AnalyticsActionItem>()
                .FirstOrDefault(entry => entry.State == EntityState.Added)
                ?.Entity;

            if (!_raceInjected && pendingAction is not null)
            {
                _raceInjected = true;

                await using (var competingContext = new AnalyticsDbContext(_options))
                {
                    competingContext.AnalyticsActionItems.Add(new AnalyticsActionItem
                    {
                        SourceType = pendingAction.SourceType,
                        SourceKey = pendingAction.SourceKey,
                        SourceId = pendingAction.SourceId,
                        Title = pendingAction.Title,
                        Description = pendingAction.Description,
                        RecommendationStatus = pendingAction.RecommendationStatus,
                        Priority = pendingAction.Priority,
                        ImpactEstimateRsd = pendingAction.ImpactEstimateRsd,
                        DueAtUtc = pendingAction.DueAtUtc,
                        ExpectedImpactRsd = pendingAction.ExpectedImpactRsd,
                        ConfidencePct = pendingAction.ConfidencePct,
                        ReliabilityPct = pendingAction.ReliabilityPct,
                        DataQualityStatus = pendingAction.DataQualityStatus,
                        Status = AnalyticsActionConstants.Statuses.New,
                        ActionUrl = pendingAction.ActionUrl,
                        MetadataJson = pendingAction.MetadataJson,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow,
                        CreatedByUserId = "race-winner",
                        UpdatedByUserId = "race-winner",
                    });

                    await competingContext.SaveChangesAsync(cancellationToken);
                }

                throw new DbUpdateException(
                    "Simulated concurrent insert for analytics action upsert.",
                    new FakeSqlStateException("23505"));
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class NonUniqueFailureAnalyticsDbContext : AnalyticsDbContext
    {
        public NonUniqueFailureAnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException(
                "Simulated unrelated database failure.",
                new FakeSqlStateException("40001"));
    }

    private sealed class FakeSqlStateException : Exception
    {
        public FakeSqlStateException(string sqlState)
            : base($"Fake SQLSTATE {sqlState}")
        {
            SqlState = sqlState;
        }

        public string SqlState { get; }
    }

    private static AnalyticsActionItemService CreateService(AnalyticsDbContext db)
        => new(db, NullLogger<AnalyticsActionItemService>.Instance);

    private static AnalyticsActionUpsertRequest CreateRequest(
        string sourceType,
        string sourceKey,
        string title = "Predlog akcije",
        string priority = AnalyticsActionConstants.Priorities.P2,
        DateTime? dueAtUtc = null,
        decimal? expectedImpactRsd = null,
        string dataQualityStatus = AnalyticsActionConstants.DataQualityStatuses.Warning,
        string? sourceRecommendationId = null,
        string? recommendationType = null,
        string? expectedImpactBasis = null,
        int? impactWindowDays = null,
        string? confidenceLevel = null,
        IReadOnlyList<string>? warningCodes = null,
        IReadOnlyList<string>? primaryDrivers = null,
        string? decisionReason = null,
        string? recommendedAction = null,
        DateTime? generatedAtUtc = null,
        string? inputFreshnessStatus = null)
        => new(
            SourceType: sourceType,
            SourceKey: sourceKey,
            SourceId: 1001,
            Title: title,
            Description: "Opis predloga",
            RecommendationStatus: "dopuna",
            Priority: priority,
            ImpactEstimateRsd: 12345m,
            DueAtUtc: dueAtUtc,
            ExpectedImpactRsd: expectedImpactRsd,
            ConfidencePct: 80,
            ReliabilityPct: 75,
            DataQualityStatus: dataQualityStatus,
            ActionUrl: "/analytics/inventory",
            SourceRecommendationId: sourceRecommendationId,
            RecommendationType: recommendationType,
            ExpectedImpactBasis: expectedImpactBasis,
            ImpactWindowDays: impactWindowDays,
            ConfidenceLevel: confidenceLevel,
            WarningCodes: warningCodes,
            PrimaryDrivers: primaryDrivers,
            DecisionReason: decisionReason,
            RecommendedAction: recommendedAction,
            GeneratedAtUtc: generatedAtUtc,
            InputFreshnessStatus: inputFreshnessStatus,
            MetadataJson: null
        );

    private static async Task SetResolvedAtUtcAsync(AnalyticsDbContext db, long actionId, DateTime resolvedAtUtc)
    {
        var entity = await db.AnalyticsActionItems.SingleAsync(x => x.Id == actionId);
        entity.ResolvedAtUtc = resolvedAtUtc;
        entity.UpdatedAtUtc = resolvedAtUtc;
        await db.SaveChangesAsync();
    }
}
