using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PilotImportBatchStatusContractTests
{
    [Theory]
    [InlineData("completed", "completed")]
    [InlineData("Succeeded", "completed")]
    [InlineData("success", "completed")]
    [InlineData("failed", "failed")]
    [InlineData("ERROR", "failed")]
    [InlineData("blocked", "failed")]
    [InlineData("cancelled", "cancelled")]
    [InlineData("canceled", "cancelled")]
    [InlineData("running", "running")]
    [InlineData("in_progress", "running")]
    [InlineData("pending", "queued")]
    [InlineData("queued", "queued")]
    [InlineData("partial", "partial")]
    [InlineData("warning", "partial")]
    public void NormalizeImportBatchStatus_MapsCanonicalVocabulary(string raw, string expected)
    {
        Assert.Equal(expected, DataQualityEndpoints.NormalizeImportBatchStatus(raw));
    }

    [Fact]
    public void NormalizeImportBatchStatus_PreservesUnknownRawValue()
    {
        Assert.Equal("weird_state", DataQualityEndpoints.NormalizeImportBatchStatus("weird_state"));
        Assert.Null(DataQualityEndpoints.NormalizeImportBatchStatus(null));
        Assert.Null(DataQualityEndpoints.NormalizeImportBatchStatus("  "));
    }

    [Theory]
    [InlineData("failed", true)]
    [InlineData("cancelled", true)]
    [InlineData("error", true)]
    [InlineData("completed", false)]
    [InlineData("running", false)]
    public void IsFailedImportStatus_MatchesHardBlockers(string raw, bool expected)
    {
        Assert.Equal(expected, DataQualityEndpoints.IsFailedImportStatus(raw));
    }

    [Theory]
    [InlineData("running", true)]
    [InlineData("queued", true)]
    [InlineData("pending", true)]
    [InlineData("partial", true)]
    [InlineData("completed", false)]
    [InlineData("failed", false)]
    public void IsInFlightImportStatus_MatchesWarningBucket(string raw, bool expected)
    {
        Assert.Equal(expected, DataQualityEndpoints.IsInFlightImportStatus(raw));
    }

    [Fact]
    public void IntakeReportDto_ExposesAdditiveImportProvenanceFields()
    {
        var report = new DataQualityEndpoints.PilotDataQualityIntakeReportDto(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            "all",
            "3",
            null,
            DateTime.UtcNow.AddHours(-2),
            DataQualityEndpoints.NormalizeImportBatchStatus("failed"),
            "global",
            99,
            DateTime.UtcNow.AddHours(-1),
            "fresh",
            88,
            "excellent",
            "Spremno",
            new DataQualityEndpoints.PilotDataQualityIntakeLoadedDataDto(1, 1, 1, 1, 1, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1)),
            new DataQualityEndpoints.PilotDataQualityIntakeIssuesDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new DataQualityEndpoints.PilotDataQualityIntakeImpactDto(0, 0, 0, 0, 0),
            ["Proveri import"]);

        Assert.Equal("failed", report.LastImportStatus);
        Assert.Equal("global", report.LastImportScope);
        Assert.Equal(99, report.LastImportBatchId);

        var durable = DataQualityEndpoints.BuildPilotIntakeReportResponse(
            report,
            (report.PeriodFromUtc!.Value, report.PeriodToUtc!.Value, report.PeriodToUtc.Value.AddDays(1)),
            storeId: 3,
            supplierId: null,
            dataScope: "all");

        Assert.Contains(durable.Warnings, static w => w.Contains("nije uspeo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(durable.Warnings, static w => w.Contains("globalan", StringComparison.OrdinalIgnoreCase));
    }
}
