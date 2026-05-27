using Application.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
                OutcomeNotes: "Ishod potvrđen"),
            userId: "u1",
            userName: "tester");

        Assert.NotNull(updated);
        Assert.Equal(AnalyticsActionConstants.OutcomeStatuses.Success, updated!.OutcomeStatus);
        Assert.Equal(777m, updated.MeasuredImpactRsd);
        Assert.Equal("Ishod potvrđen", updated.OutcomeNotes);
        Assert.Equal(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), updated.OutcomeMeasuredAtUtc);
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

    private static AnalyticsActionItemService CreateService(AnalyticsDbContext db)
        => new(db, NullLogger<AnalyticsActionItemService>.Instance);

    private static AnalyticsActionUpsertRequest CreateRequest(
        string sourceType,
        string sourceKey,
        string title = "Predlog akcije",
        string priority = AnalyticsActionConstants.Priorities.P2,
        DateTime? dueAtUtc = null,
        decimal? expectedImpactRsd = null)
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
            DataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning,
            ActionUrl: "/analytics/inventory",
            MetadataJson: null
        );
}
