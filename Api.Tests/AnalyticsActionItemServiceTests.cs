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
        string priority = AnalyticsActionConstants.Priorities.P2)
        => new(
            SourceType: sourceType,
            SourceKey: sourceKey,
            SourceId: 1001,
            Title: title,
            Description: "Opis predloga",
            RecommendationStatus: "dopuna",
            Priority: priority,
            ImpactEstimateRsd: 12345m,
            ConfidencePct: 80,
            ReliabilityPct: 75,
            DataQualityStatus: AnalyticsActionConstants.DataQualityStatuses.Warning,
            ActionUrl: "/analytics/inventory",
            MetadataJson: null
        );
}
