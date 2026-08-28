using Api.Services.DataSources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Supplemental deterministic proof for SQL Server quoting, cursor SQL and secret redaction.
/// Live-engine proof lives in <see cref="SqlServerSourceDataSessionIntegrationTests"/>.
/// </summary>
public sealed class SqlServerSourceDataSessionSqlTests
{
    [Fact]
    public void Quote_ReservedAndBracketIdentifiers_AreEscaped()
    {
        Assert.Equal("[Order]", SqlServerIdentifier.Quote("Order"));
        Assert.Equal("[Select]", SqlServerIdentifier.Quote("Select"));
        Assert.Equal("[User]]Name]", SqlServerIdentifier.Quote("User]Name"));
    }

    [Fact]
    public void TryQuoteTable_SchemaQualifiedReservedName_QuotesBothParts()
    {
        Assert.True(SqlServerIdentifier.TryQuoteTable("dbo.Order", out var quoted, out var failure));
        Assert.Equal("[dbo].[Order]", quoted);
        Assert.Equal(string.Empty, failure);
    }

    [Fact]
    public void TryQuoteTable_AlreadyBracketed_ParsesAndRequotes()
    {
        Assert.True(SqlServerIdentifier.TryQuoteTable("[Order]", out var quoted, out _));
        Assert.Equal("[dbo].[Order]", quoted);
    }

    [Fact]
    public void TryQuoteTable_RejectsInjectionAndMultipartNames()
    {
        Assert.False(SqlServerIdentifier.TryQuoteTable("Order; DROP TABLE dbo.X", out _, out var semicolon));
        Assert.Contains("prohibited", semicolon, StringComparison.OrdinalIgnoreCase);

        Assert.False(SqlServerIdentifier.TryQuoteTable("Retail.dbo.Order", out _, out var multipart));
        Assert.Contains("three-part", multipart, StringComparison.OrdinalIgnoreCase);

        Assert.False(SqlServerIdentifier.TryQuoteTable(" ", out _, out var empty));
        Assert.Contains("empty", empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSelectSql_IdCursor_UsesNamedParametersAndQuotedIdentifiers()
    {
        var query = new SourceReadQuery
        {
            CursorMode = "id",
            CursorId = 42,
            IdAliases = ["id"]
        };

        var (commandText, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Id", "Select"],
            "[dbo].[Order]",
            query);

        Assert.Equal("SELECT * FROM [dbo].[Order] WHERE [Id] > @p0 ORDER BY [Id]", commandText);
        Assert.Equal(("@p0", 42L), Assert.Single(parameters));
    }

    [Fact]
    public void BuildSelectSql_TimestampThenId_IsDeterministic()
    {
        var cursorUtc = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var query = new SourceReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = cursorUtc,
            CursorTieBreakerId = 1,
            OverlapSeconds = 60,
            TimestampAliases = ["updatedat"],
            IdAliases = ["id"]
        };

        var (commandText, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Id", "UpdatedAt", "Price"],
            "[dbo].[Order]",
            query);

        Assert.Equal(
            "SELECT * FROM [dbo].[Order] WHERE ([UpdatedAt] > @p0 OR ([UpdatedAt] = @p1 AND [Id] > @p2)) ORDER BY [UpdatedAt], [Id]",
            commandText);
        Assert.Equal(3, parameters.Count);
        Assert.Equal(cursorUtc.AddSeconds(-60), parameters[0]);
        Assert.Equal(1L, parameters[2]);
    }

    [Fact]
    public void BuildSelectSql_MissingAlias_FallsBackToFullScanWithoutFakePredicate()
    {
        var query = new SourceReadQuery
        {
            CursorMode = "id",
            CursorId = 10,
            IdAliases = ["id"]
        };

        var (commandText, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Naziv", "Cena"],
            "[dbo].[NoId]",
            query);

        Assert.Equal("SELECT * FROM [dbo].[NoId] ORDER BY [Naziv]", commandText);
        Assert.Empty(parameters);
    }

    [Fact]
    public void BuildSelectSql_MaxRows_UsesParameterizedTop()
    {
        var query = new SourceReadQuery { MaxRows = 25 };
        var (commandText, parameters) = SqlServerSourceDataSession.BuildSelectSqlFromColumns(
            ["Id"],
            "[dbo].[Order]",
            query);

        Assert.StartsWith("SELECT TOP (@maxRows) * FROM [dbo].[Order]", commandText, StringComparison.Ordinal);
        Assert.Contains(25, parameters);
    }

    [Fact]
    public async Task SourceIdentity_OmitsCredentials()
    {
        await using var session = new SqlServerSourceDataSession(
            "Server=tcp:example,1433;User Id=sa;Password=SuperSecret_Qdb03!;Initial Catalog=Retail;Encrypt=True;",
            NullLogger.Instance);

        Assert.Equal("sqlserver", session.Provider);
        Assert.Equal("read-only", session.Mode);
        Assert.DoesNotContain("SuperSecret_Qdb03!", session.SourceIdentity, StringComparison.Ordinal);
        Assert.DoesNotContain("sa", session.SourceIdentity, StringComparison.Ordinal);
        Assert.Contains("example", session.SourceIdentity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retail", session.SourceIdentity, StringComparison.OrdinalIgnoreCase);
        Assert.True(session.Capabilities.PredicatePushdown);
        Assert.False(session.Capabilities.Cdc);
    }

    [Fact]
    public async Task TestConnection_DoesNotLogPassword()
    {
        var logs = new List<string>();
        await using var session = new SqlServerSourceDataSession(
            "Server=tcp:127.0.0.1,1;User Id=sa;Password=SuperSecret_Qdb03!;Initial Catalog=Retail;Encrypt=False;Connection Timeout=1;",
            new CollectorLogger(logs),
            commandTimeoutSeconds: 1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.TestConnectionAsync());
        Assert.Contains("failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SuperSecret_Qdb03!", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperSecret_Qdb03!", error.ToString(), StringComparison.Ordinal);
        Assert.All(logs, message => Assert.DoesNotContain("SuperSecret_Qdb03!", message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-2, SqlServerConnectionDiagnostics.CategoryTimeout)]
    [InlineData(18456, SqlServerConnectionDiagnostics.CategoryAuthentication)]
    [InlineData(53, SqlServerConnectionDiagnostics.CategoryNetwork)]
    [InlineData(4060, SqlServerConnectionDiagnostics.CategoryUnavailable)]
    public void Categorize_MapsSafeFailureCategories(int number, string expected)
    {
        Assert.Equal(expected, SqlServerConnectionDiagnostics.CategorizeNumber(number));
    }

    private sealed class CollectorLogger : ILogger
    {
        private readonly List<string> _entries;

        public CollectorLogger(List<string> entries) => _entries = entries;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(formatter(state, exception));
            if (exception is not null)
                _entries.Add(exception.ToString());
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
