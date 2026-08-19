using Api.Models;
using Api.Services.DataSources;
using Xunit;

namespace Api.Tests;

public sealed class SourceMappingPreviewServiceTests
{
    [Fact]
    public void BuildPreview_ResolvesFieldMappingsAndProjectsRows()
    {
        var request = CreateRequest(take: 50);
        var sourceColumns = new List<string> { "Id", "Name", "Quantity" };
        var rows = CreateSampleRows(sourceColumns);

        var preview = SourceMappingPreviewService.BuildPreview(
            "live-sql",
            "sqlserver",
            request,
            sourceColumns,
            rows,
            truncated: true);

        Assert.Equal("live-sql", preview.ProfileName);
        Assert.Equal("sqlserver", preview.Provider);
        Assert.Equal("source_items", preview.CanonicalEntity);
        Assert.Equal("dbo.SourceItems", preview.SourceTable);
        Assert.Equal(50, preview.RequestedTake);
        Assert.Equal(2, preview.ReturnedRows);
        Assert.True(preview.Truncated);
        Assert.NotEmpty(preview.SchemaFingerprint);
        Assert.Empty(preview.Issues);

        var idField = Assert.Single(preview.FieldMappings, mapping => mapping.TargetField == "Id");
        Assert.Equal("matched", idField.Status);
        Assert.Equal("Id", idField.SourceColumn);

        var firstRow = Assert.Single(preview.Rows, row => row.RowIndex == 1);
        Assert.Equal(3, firstRow.Values.Count);
        Assert.Equal(1, firstRow.Values[0].Value);
        Assert.Equal("Alpha", firstRow.Values[1].Value);
        Assert.Equal(10, firstRow.Values[2].Value);

        var secondRow = Assert.Single(preview.Rows, row => row.RowIndex == 2);
        Assert.Equal(2, secondRow.Values[0].Value);
        Assert.Equal("Beta", secondRow.Values[1].Value);
        Assert.Equal(20, secondRow.Values[2].Value);
    }

    [Fact]
    public void BuildPreview_AddsValidationIssuesForMissingKeyCursorAndFieldAliases()
    {
        var request = new SourceMappingPreviewRequest
        {
            CanonicalEntity = "source_items",
            SourceTable = "dbo.SourceItems",
            ExternalKeyColumns = ["MissingKey"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "timestamp_then_id",
                TimestampAliases = ["MissingTimestamp"],
                IdAliases = ["MissingId"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "MissingField", Aliases = ["Nope"] }
            ],
            Take = 10
        };

        var preview = SourceMappingPreviewService.BuildPreview(
            "live-sql",
            "sqlserver",
            request,
            new List<string> { "Id", "Name" },
            [],
            truncated: false);

        Assert.Contains(preview.Issues, issue => issue.ReasonCode == "external_key_missing_column");
        Assert.Contains(preview.Issues, issue => issue.ReasonCode == "cursor_missing_timestamp_alias");
        Assert.Contains(preview.Issues, issue => issue.ReasonCode == "cursor_missing_id_alias");
        Assert.Contains(preview.Issues, issue => issue.ReasonCode == "source_column_not_found");

        var mapping = Assert.Single(preview.FieldMappings);
        Assert.Equal("missing", mapping.Status);
        Assert.Equal("source_column_not_found", mapping.ReasonCode);
    }

    [Fact]
    public void ComputeSchemaFingerprint_IsIndependentOfRequestedTake()
    {
        var baseRequest = CreateRequest(take: 5);
        var columns = new List<string> { "Id", "Name", "Quantity" };

        var fingerprintA = SourceMappingPreviewService.ComputeSchemaFingerprint(
            "live-sql",
            "sqlserver",
            baseRequest,
            columns);

        baseRequest.Take = 500;
        var fingerprintB = SourceMappingPreviewService.ComputeSchemaFingerprint(
            "live-sql",
            "sqlserver",
            baseRequest,
            columns);

        Assert.Equal(fingerprintA, fingerprintB);
    }

    private static SourceMappingPreviewRequest CreateRequest(int take)
        => new()
        {
            CanonicalEntity = "source_items",
            SourceTable = "dbo.SourceItems",
            ExternalKeyColumns = ["Id"],
            Cursor = new SourceReadQuery
            {
                CursorMode = "id",
                CursorId = 0,
                IdAliases = ["Id"]
            },
            FieldMappings =
            [
                new SourceMappingFieldRequest { TargetField = "Id", Aliases = ["Id"] },
                new SourceMappingFieldRequest { TargetField = "Name", Aliases = ["Name"] },
                new SourceMappingFieldRequest { TargetField = "Quantity", Aliases = ["Quantity"] }
            ],
            Take = take
        };

    private static List<SourceDataRow> CreateSampleRows(IReadOnlyList<string> columns)
    {
        var schema = new SourceDataSchema(columns);
        return
        [
            new SourceDataRow(schema, [1, "Alpha", 10]),
            new SourceDataRow(schema, [2, "Beta", 20])
        ];
    }
}
