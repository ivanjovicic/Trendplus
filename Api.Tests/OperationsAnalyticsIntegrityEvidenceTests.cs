using Application.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsIntegrityEvidenceTests
{
    [Fact]
    public void EvidenceModel_UsesEvidenceIdAsAppendOnlyIdentity_AndJsonColumns()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql("Host=localhost;Database=not-used")
            .Options;

        using var db = new TrendplusDbContext(options);
        var entity = db.Model.FindEntityType(typeof(Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord));

        Assert.NotNull(entity);
        Assert.Equal("operations_analytics_integrity_evidence", entity!.GetTableName());
        Assert.Equal(
            nameof(Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord.EvidenceId),
            entity.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Equal("jsonb", entity.FindProperty(nameof(Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord.DeltasJson))!.GetColumnType());
        Assert.Equal("jsonb", entity.FindProperty(nameof(Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord.CoverageJson))!.GetColumnType());
    }

    [Fact]
    public void EvidenceRecord_PreservesVerifiedHistoryFields_WithoutRawConnectionString()
    {
        var record = new Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord
        {
            EvidenceId = "probe-immutable-1",
            Status = OperationsAnalyticsIntegrityStates.Verified,
            CheckedAtUtc = DateTime.UtcNow,
            ContractVersion = "SST-ACCURACY-1.0",
            DatabaseFingerprint = "hashed-fingerprint",
            DeltasJson = "[{\"dimension\":\"supplier_shoe_live_aggregate\"}]",
            CoverageJson = "{\"costCoverage\":\"not_collected_by_bounded_probe\"}",
            BlocksDecisionSignals = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        Assert.Equal("probe-immutable-1", record.EvidenceId);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, record.Status);
        Assert.DoesNotContain("Host=", record.DatabaseFingerprint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not_collected_by_bounded_probe", record.CoverageJson, StringComparison.Ordinal);
        Assert.False(record.BlocksDecisionSignals);
    }
}
