using Api.Services.DataSources;
using Xunit;

namespace Api.Tests;

[CollectionDefinition(SqlServerSourceDataSessionCollection.CollectionName)]
public sealed class SqlServerSourceDataSessionCollection : ICollectionFixture<SqlServerSourceDataSessionFixture>
{
    public const string CollectionName = "sqlserver-source-session";
}

public sealed class SqlServerSourceDataSessionFixture : IAsyncLifetime
{
    private readonly SqlServerContainerFixture _containerFixture = new();

    public bool IsAvailable => _containerFixture.IsAvailable;

    public string ConnectionString => _containerFixture.AdminConnectionString;

    public Task InitializeAsync() => _containerFixture.InitializeAsync();

    public Task DisposeAsync() => _containerFixture.DisposeAsync();
}

public sealed class SqlServerSourceDataSessionTests
{
    [Fact]
    public void TryGetQuotedTableIdentifier_QuotesMultipartReservedAndUnicodeNames()
    {
        var ok = SqlServerSourceDataSession.TryGetQuotedTableIdentifier("[sales].[Order]", out var quoted, out var failureReason);

        Assert.True(ok, failureReason);
        Assert.Equal("[sales].[Order]", quoted);
    }

    [Fact]
    public void TryGetQuotedTableIdentifier_DefaultsSinglePartToDbo()
    {
        var ok = SqlServerSourceDataSession.TryGetQuotedTableIdentifier("Каталог", out var quoted, out var failureReason);

        Assert.True(ok, failureReason);
        Assert.Equal("[dbo].[Каталог]", quoted);
    }

    [Fact]
    public void TryGetQuotedTableIdentifier_RejectsInvalidMultipartInput()
    {
        var ok = SqlServerSourceDataSession.TryGetQuotedTableIdentifier("dbo.orders.more", out _, out var failureReason);

        Assert.False(ok);
        Assert.Contains("one or two parts", failureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSelectSqlFromColumns_IdCursor_UsesDeterministicOrderingEvenWithoutCheckpoint()
    {
        var query = new SourceReadQuery
        {
            CursorMode = "id",
            IdAliases = ["id"]
        };

        var (sql, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["ID", "Naziv"],
            "[sales].[Order]",
            query);

        Assert.Equal("SELECT * FROM [sales].[Order] ORDER BY [ID]", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildSelectSqlFromColumns_TimestampThenId_BuildsCompositePredicateAndOrder()
    {
        var cursorUtc = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var query = new SourceReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = cursorUtc,
            CursorTieBreakerId = 700,
            OverlapSeconds = 30,
            TimestampAliases = ["updatedat"],
            IdAliases = ["id"]
        };

        var (sql, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Updated At", "ID", "Naziv"],
            "[sales].[Order]",
            query);

        Assert.Equal(
            "SELECT * FROM [sales].[Order] WHERE ([Updated At] > @p0 OR ([Updated At] = @p1 AND [ID] > @p2)) ORDER BY [Updated At], [ID]",
            sql);
        Assert.Equal(3, parameters.Count);
        Assert.Equal(cursorUtc.AddSeconds(-30), parameters[0]);
        Assert.Equal(cursorUtc.AddSeconds(-30), parameters[1]);
        Assert.Equal(700L, parameters[2]);
    }

    [Fact]
    public void BuildSelectSqlFromColumns_MissingAliases_FallsBackToFullScan()
    {
        var query = new SourceReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc),
            CursorTieBreakerId = 700,
            TimestampAliases = ["updatedat"],
            IdAliases = ["id"]
        };

        var (sql, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Naziv", "Cena"],
            "[sales].[Order]",
            query);

        Assert.Equal("SELECT * FROM [sales].[Order]", sql);
        Assert.Empty(parameters);
    }
}
