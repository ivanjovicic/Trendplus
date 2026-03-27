using Api.Services.Access;
using Xunit;

namespace Api.Tests;

public sealed class AccessReadQueryPushdownTests
{
    [Fact]
    public void BuildSelectSqlFromColumns_IdCursor_BuildsWhereAndOrderBy()
    {
        var query = new AccessReadQuery
        {
            CursorMode = "id",
            CursorId = 42,
            IdAliases = ["id", "idartikal"]
        };

        var (sql, parameters) = WindowsAccessSession.BuildSelectSqlFromColumns(
            columns: ["IDArtikal", "Naziv"],
            quotedTable: "[tblArtikli]",
            query: query);

        Assert.Equal("SELECT * FROM [tblArtikli] WHERE [IDArtikal] > ? ORDER BY [IDArtikal]", sql);
        Assert.Single(parameters);
        Assert.Equal(42L, parameters[0]);
    }

    [Fact]
    public void BuildSelectSqlFromColumns_TimestampThenId_BuildsCompositePredicate()
    {
        var cursorUtc = new DateTime(2026, 03, 27, 10, 0, 0, DateTimeKind.Utc);
        var query = new AccessReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = cursorUtc,
            CursorTieBreakerId = 777,
            OverlapSeconds = 60,
            TimestampAliases = ["updatedat", "lastmodified"],
            IdAliases = ["id", "idartikal"]
        };

        var (sql, parameters) = WindowsAccessSession.BuildSelectSqlFromColumns(
            columns: ["UpdatedAt", "IDArtikal", "Naziv"],
            quotedTable: "[tblArtikli]",
            query: query);

        Assert.Equal(
            "SELECT * FROM [tblArtikli] WHERE ([UpdatedAt] > ? OR ([UpdatedAt] = ? AND [IDArtikal] > ?)) ORDER BY [UpdatedAt], [IDArtikal]",
            sql);
        Assert.Equal(3, parameters.Count);
        Assert.Equal(cursorUtc.AddSeconds(-60), parameters[0]);
        Assert.Equal(cursorUtc.AddSeconds(-60), parameters[1]);
        Assert.Equal(777L, parameters[2]);
    }

    [Fact]
    public void BuildSelectSqlFromColumns_MissingAliases_FallsBackToFullScan()
    {
        var query = new AccessReadQuery
        {
            CursorMode = "id",
            CursorId = 10,
            IdAliases = ["id", "idartikal"]
        };

        var (sql, parameters) = WindowsAccessSession.BuildSelectSqlFromColumns(
            columns: ["Naziv", "Cena"],
            quotedTable: "[tblNoId]",
            query: query);

        Assert.Equal("SELECT * FROM [tblNoId]", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void CanApplyAccessReadPushdown_WithMatchingIdAlias_ReturnsTrue()
    {
        var canApply = AccessImportService.CanApplyAccessReadPushdown(
            cursorMode: "id",
            cursorTimestampUtc: null,
            cursorId: 11,
            cursorTieBreakerId: null,
            timestampAliases: [],
            idAliases: ["id", "idartikal"],
            sourceColumns: ["Naziv", "IDArtikal", "Cena"]);

        Assert.True(canApply);
    }

    [Fact]
    public void CanApplyAccessReadPushdown_WithoutMatchingAliases_ReturnsFalse()
    {
        var canApply = AccessImportService.CanApplyAccessReadPushdown(
            cursorMode: "timestamp_then_id",
            cursorTimestampUtc: new DateTime(2026, 03, 27, 10, 0, 0, DateTimeKind.Utc),
            cursorId: 22,
            cursorTieBreakerId: 22,
            timestampAliases: ["updatedat", "modifiedat"],
            idAliases: ["id", "idartikal"],
            sourceColumns: ["Naziv", "Cena"]);

        Assert.False(canApply);
    }
}
