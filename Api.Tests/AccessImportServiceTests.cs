using Api.Services;
using Api.Services.Access;
using Api.Config;
using Api.Models;
using System.Data;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportServiceTests
{
    [Fact]
    public void BuildAccessOdbcConnectionString_UsesDbq_ForLinuxRegisteredDriver()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            "/tmp/trendplus-access-import/test.accdb",
            isWindows: false);

        Assert.Contains("Driver={MDBTools};", result);
        Assert.Contains("DBQ=/tmp/trendplus-access-import/test.accdb;", result);
        Assert.DoesNotContain("Database=", result);
    }

    [Fact]
    public void BuildAccessOdbcConnectionString_UsesDbq_ForLinuxDriverPath()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            "/tmp/trendplus-access-import/test.accdb",
            isWindows: false,
            driverPath: "/usr/lib/x86_64-linux-gnu/odbc/libmdbodbc.so");

        Assert.Contains("Driver=/usr/lib/x86_64-linux-gnu/odbc/libmdbodbc.so;", result);
        Assert.Contains("DBQ=/tmp/trendplus-access-import/test.accdb;", result);
        Assert.DoesNotContain("Database=", result);
    }

    [Fact]
    public void BuildAccessOdbcConnectionString_UsesWindowsDriver_WhenRequested()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            @"C:\temp\trendplus.accdb",
            isWindows: true);

        Assert.Contains("Driver={Microsoft Access Driver (*.mdb, *.accdb)};", result);
        Assert.Contains(@"Dbq=C:\temp\trendplus.accdb;", result);
        Assert.Contains("ReadOnly=1;", result);
    }

    #region Schema Robustness Tests

    // ─────────────────────────────────────────────────────────────
    // Test helpers to create DataTable mock objects with different schema variants
    // ─────────────────────────────────────────────────────────────

    private static DataTable CreateStandardSchema(params string[] tables)
    {
        var dt = new DataTable("Tables");
        dt.Columns.Add("TABLE_CATALOG");
        dt.Columns.Add("TABLE_SCHEMA");
        dt.Columns.Add("TABLE_NAME");
        dt.Columns.Add("TABLE_TYPE");

        foreach (var table in tables)
        {
            dt.Rows.Add("master", "dbo", table, "TABLE");
        }
        return dt;
    }

    private static DataTable CreateNonStandardSchema_MissingTableName(params string[] tables)
    {
        // Some ODBC providers might not include TABLE_NAME column
        var dt = new DataTable("Tables");
        dt.Columns.Add("TABLE_CATALOG");
        dt.Columns.Add("TABLE_SCHEMA");
        // TABLE_NAME is missing!
        dt.Columns.Add("TABLE_TYPE");
        dt.Columns.Add("TABLE");  // Alternative column name

        foreach (var table in tables)
        {
            dt.Rows.Add("master", "dbo", "TABLE", table);
        }
        return dt;
    }

    private static DataTable CreateNonStandardSchema_MissingTableType(params string[] tables)
    {
        // Some ODBC providers might not include TABLE_TYPE column
        var dt = new DataTable("Tables");
        dt.Columns.Add("TABLE_CATALOG");
        dt.Columns.Add("TABLE_SCHEMA");
        dt.Columns.Add("TABLE_NAME");
        // TABLE_TYPE is missing!

        foreach (var table in tables)
        {
            dt.Rows.Add("master", "dbo", table);
        }
        return dt;
    }

    private static DataTable CreateNonStandardSchema_RenamedColumns(params string[] tables)
    {
        // Some ODBC providers might use completely different column names
        var dt = new DataTable("Tables");
        dt.Columns.Add("TABLE_CAT");
        dt.Columns.Add("TABLE_SCHEM");
        dt.Columns.Add("NAME");  // Instead of TABLE_NAME
        dt.Columns.Add("TABLE_TYPE");

        foreach (var table in tables)
        {
            dt.Rows.Add("master", "dbo", table, "TABLE");
        }
        return dt;
    }

    private static DataTable CreateEmptySchema()
    {
        // No rows
        var dt = new DataTable("Tables");
        dt.Columns.Add("TABLE_NAME");
        return dt;
    }

    [Fact]
    public void ResolveTableName_WithStandardSchema_TableNameExists()
    {
        var schema = CreateStandardSchema("test_table");
        var row = schema.Rows[0];

        var result = AccessImportService.ResolveTableName(row, schema);

        Assert.NotNull(result);
        Assert.Equal("test_table", result);
    }

    [Fact]
    public void ResolveTableName_WithMissingTableNameColumn_FallsBackToTableColumn()
    {
        var schema = CreateNonStandardSchema_MissingTableName("Products", "Orders");
        var row = schema.Rows[0];

        var result = AccessImportService.ResolveTableName(row, schema);

        Assert.NotNull(result);
        Assert.Equal("Products", result);
    }

    [Fact]
    public void ResolveTableName_WithRenamedColumns_FindsNameColumn()
    {
        var schema = CreateNonStandardSchema_RenamedColumns("Customers", "Invoices");
        var row = schema.Rows[0];

        var result = AccessImportService.ResolveTableName(row, schema);

        Assert.NotNull(result);
        Assert.Equal("Customers", result);
    }

    [Fact]
    public void ResolveTableName_WithNullRow_ReturnsNull()
    {
        var schema = CreateStandardSchema("test_table");

        var result = AccessImportService.ResolveTableName((DataRow)null!, schema);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTableName_WithNullSchema_ReturnsNull()
    {
        var schema = new DataTable("Tables");
        schema.Columns.Add("Any");
        var row = schema.NewRow();
        row[0] = "value";
        schema.Rows.Add(row);

        var result = AccessImportService.ResolveTableName(row, null!);

        Assert.Null(result);
    }

    [Fact]
    public void CheckIsUserTable_WithStandardSchema_TableType_Returns_True()
    {
        var schema = CreateStandardSchema("UserTable");
        var row = schema.Rows[0];

        var result = AccessImportService.CheckIsUserTable(row, schema);

        Assert.True(result);
    }

    [Fact]
    public void CheckIsUserTable_WithMissingTableTypeColumn_DefaultsTrue()
    {
        // When TABLE_TYPE is missing, should default to true (fail-open)
        var schema = CreateNonStandardSchema_MissingTableType("SomeTable");
        var row = schema.Rows[0];

        var result = AccessImportService.CheckIsUserTable(row, schema);

        Assert.True(result);
    }

    [Fact]
    public void CheckIsUserTable_WithNullTableTypeValue_DefaultsTrue()
    {
        var schema = CreateStandardSchema();
        schema.Columns["TABLE_TYPE"]!.AllowDBNull = true;
        schema.Rows.Add("master", "dbo", "TestTable", DBNull.Value);
        var row = schema.Rows[0];

        var result = AccessImportService.CheckIsUserTable(row, schema);

        Assert.True(result);
    }

    [Fact]
    public void ToFirstDictionary_WithDuplicateKeys_KeepsFirstItem()
    {
        var source = new[]
        {
            (Id: 1, Name: "first"),
            (Id: 1, Name: "second"),
            (Id: 2, Name: "third")
        };

        var result = AccessImportService.ToFirstDictionary(source, x => x.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[1].Name);
        Assert.Equal("third", result[2].Name);
    }

    [Fact]
    public void ToFirstDictionary_WithLargeInput_DoesNotThrow_AndPreservesFirstOccurrence()
    {
        var source = Enumerable.Range(1, 100_000)
            .Select(i => (Id: i % 50, Value: i))
            .ToList();

        var result = AccessImportService.ToFirstDictionary(source, x => x.Id);

        Assert.Equal(50, result.Count);
        Assert.Equal(50, result[0].Value);
        Assert.Equal(1, result[1].Value);
    }

    [Fact]
    public async Task PreviewAsync_WhenFileDoesNotExist_ReturnsFailSoftResponse()
    {
        var service = new AccessImportService(
            trendDb: null!,
            analyticsDb: null!,
            logger: NullLogger<AccessImportService>.Instance);

        var response = await service.PreviewAsync(@"C:\definitely-missing\missing.accdb");

        Assert.False(response.CanImport);
        Assert.Empty(response.Tables);
        Assert.Contains(response.Warnings, warning => warning.StartsWith("Preview failed:", StringComparison.Ordinal));
    }

    [Fact]
    public void AccessCsvParser_PreservesEscapedQuotes()
    {
        const string csv = "\"id\",\"name\"\n\"1\",\"Model \"\"Air\"\"\"";

        var records = MdbToolsCliSession.ReadCsvRecords(new StringReader(csv)).ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "id", "name" }, records[0]);
        Assert.Equal(new[] { "1", "Model \"Air\"" }, records[1]);
    }

    [Fact]
    public void AccessCsvParser_PreservesMultilineFields()
    {
        const string csv = "\"id\",\"description\"\n\"1\",\"red 1\nred 2\"";

        var records = MdbToolsCliSession.ReadCsvRecords(new StringReader(csv)).ToList();

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "id", "description" }, records[0]);
        Assert.Equal("red 1\nred 2", records[1][1]);
    }

    [Fact]
    public void AccessDataSchema_NormalizesAliases()
    {
        var schema = new AccessDataSchema(["ID Artikal", "Prodajna-Cena", "Kolicina"]);
        var row = new AccessDataRow(schema, [42, 999.5m, 3]);

        Assert.True(row.TryGetValue("id_artikal", out var id));
        Assert.True(row.TryGetValue("prodajnacena", out var cena));
        Assert.True(row.TryGetValue("kolicina", out var kolicina));
        Assert.Equal(42, id);
        Assert.Equal(999.5m, cena);
        Assert.Equal(3, kolicina);
    }

    [Fact]
    public void AccessDataRow_ToDictionary_PreservesOriginalColumns()
    {
        var schema = new AccessDataSchema(["IDProdaja", "Cena", "Komentar"]);
        var row = new AccessDataRow(schema, [101, 55.5m, "test"]);

        var snapshot = row.ToDictionary();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(101, snapshot["IDProdaja"]);
        Assert.Equal(55.5m, snapshot["Cena"]);
        Assert.Equal("test", snapshot["Komentar"]);
    }

    [Fact]
    public void ResolveProdajaLineNabavnaCena_PrefersRsdAlias()
    {
        var schema = new AccessDataSchema(["NabavnaCenaDin", "NabavnaCena", "PurchasePrice"]);
        var row = new AccessDataRow(schema, [123.45m, 99m, 88m]);

        var result = AccessImportService.ResolveProdajaLineNabavnaCena(row);

        Assert.Equal(123.45m, result);
    }

    [Fact]
    public void ResolveProdajaLineNabavnaCena_UsesLegacyAliases_WhenRsdMissing()
    {
        var schema = new AccessDataSchema(["PurchasePrice"]);
        var row = new AccessDataRow(schema, [77.5m]);

        var result = AccessImportService.ResolveProdajaLineNabavnaCena(row);

        Assert.Equal(77.5m, result);
    }

    [Fact]
    public void ResolveProdajaLineNabavnaCena_ReturnsNull_WhenPurchaseAliasesMissing()
    {
        var schema = new AccessDataSchema(["ProdajnaCena", "Kolicina"]);
        var row = new AccessDataRow(schema, [1999m, 2]);

        var result = AccessImportService.ResolveProdajaLineNabavnaCena(row);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveProdajaLineNabavnaCena_ReturnsNull_WhenValueIsZero()
    {
        var schema = new AccessDataSchema(["NabavnaCenaDin"]);
        var row = new AccessDataRow(schema, [0m]);

        var result = AccessImportService.ResolveProdajaLineNabavnaCena(row);

        Assert.Null(result);
    }

    [Fact]
    public void SourceColumnsContainProdajaLineNabavnaCena_ReturnsTrue_WhenAliasExists()
    {
        var columns = new List<string> { "IDDnevnik", "IDArtikal", "NabavnaCenaDin" };

        var result = AccessImportService.SourceColumnsContainProdajaLineNabavnaCena(columns);

        Assert.True(result);
    }

    [Fact]
    public void SourceColumnsContainProdajaLineNabavnaCena_ReturnsFalse_WhenAliasMissing()
    {
        var columns = new List<string> { "IDDnevnik", "IDArtikal", "ProdajnaCena" };

        var result = AccessImportService.SourceColumnsContainProdajaLineNabavnaCena(columns);

        Assert.False(result);
    }

    [Fact]
    public void BuildFieldMappingsPreview_ForProdajaStavke_MapsNabavnaCenaAlias()
    {
        var method = typeof(AccessImportService).GetMethod(
            "BuildFieldMappingsPreview",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var columns = new List<string> { "IDDnevnik", "IDArtikal", "Kolicina", "ProdajnaCena", "NabavnaCenaDin" };
        var mappings = Assert.IsType<List<AccessImportFieldMappingPreview>>(
            method!.Invoke(null, new object[] { "prodaja_stavke", columns }));

        var nabavnaMapping = Assert.Single(
            mappings,
            m => m.TargetField.Equals("NabavnaCena", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("NabavnaCenaDin", nabavnaMapping.SourceColumn);
        Assert.Equal("matched", nabavnaMapping.Status, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessRowCountResult_UnknownDefaultsToUnknownMode()
    {
        var result = AccessRowCountResult.Unknown();

        Assert.Equal(0, result.Count);
        Assert.Equal("unknown", result.Mode);
        Assert.False(result.IsExact);
    }

    [Fact]
    public void AccessImportOptions_SkipInvalidForeignKeys_DefaultsToTrue()
    {
        var options = new AccessImportOptions();

        Assert.True(options.SkipInvalidForeignKeys);
    }

    [Fact]
    public void AccessImportOptions_AutoInsertMissingParents_DefaultsToFalse()
    {
        var options = new AccessImportOptions();

        Assert.False(options.AutoInsertMissingParents);
    }

    [Fact]
    public void AccessImportOptions_RunningBatchStaleMinutes_DefaultsToFourHours()
    {
        var options = new AccessImportOptions();

        Assert.Equal(240, options.RunningBatchStaleMinutes);
    }

    [Fact]
    public void AccessImportOptions_RegisterWorkerInWebProcess_DefaultsToTrue()
    {
        var options = new AccessImportOptions();

        Assert.True(options.RegisterWorkerInWebProcess);
    }

    [Fact]
    public void AccessImportOptions_PendingBatchStaleMinutes_DefaultsToTenMinutes()
    {
        var options = new AccessImportOptions();

        Assert.Equal(10, options.PendingBatchStaleMinutes);
    }

    [Fact]
    public void AccessImportOptions_EnableRuntimeBatchSchemaBootstrap_DefaultsToFalse()
    {
        var options = new AccessImportOptions();

        Assert.False(options.EnableRuntimeBatchSchemaBootstrap);
    }

    [Fact]
    public void AccessImportOptions_PreventConcurrentRuns_DefaultsToTrue()
    {
        var options = new AccessImportOptions();

        Assert.True(options.PreventConcurrentRuns);
    }

    [Fact]
    public void IsRunningBatchStale_ReturnsTrue_WhenBatchExceededRecoveryWindow()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var startedAtUtc = utcNow.AddMinutes(-241);

        var result = AccessImportService.IsRunningBatchStale(startedAtUtc, utcNow, 240);

        Assert.True(result);
    }

    [Fact]
    public void IsRunningBatchStale_ReturnsFalse_WhenBatchIsStillWithinRecoveryWindow()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var startedAtUtc = utcNow.AddMinutes(-30);

        var result = AccessImportService.IsRunningBatchStale(startedAtUtc, utcNow, 240);

        Assert.False(result);
    }

    [Fact]
    public void IsRunningBatchStale_UsesLastHeartbeat_WhenAvailable()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var startedAtUtc = utcNow.AddMinutes(-500);
        var lastHeartbeatUtc = utcNow.AddMinutes(-10);

        var result = AccessImportService.IsRunningBatchStale(startedAtUtc, lastHeartbeatUtc, utcNow, 240);

        Assert.False(result);
    }

    [Fact]
    public void IsRunningBatchStale_ReturnsTrue_WhenLastHeartbeatExceededRecoveryWindow()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var startedAtUtc = utcNow.AddMinutes(-500);
        var lastHeartbeatUtc = utcNow.AddMinutes(-241);

        var result = AccessImportService.IsRunningBatchStale(startedAtUtc, lastHeartbeatUtc, utcNow, 240);

        Assert.True(result);
    }

    [Fact]
    public void IsPendingBatchStale_ReturnsTrue_WhenQueuedPendingBatchExceededRecoveryWindow()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var queuedAtUtc = utcNow.AddMinutes(-11);

        var result = AccessImportService.IsPendingBatchStale(queuedAtUtc, lastHeartbeatUtc: null, utcNow, 10);

        Assert.True(result);
    }

    [Fact]
    public void IsPendingBatchStale_UsesLastHeartbeat_WhenAvailable()
    {
        var utcNow = new DateTime(2026, 03, 25, 8, 0, 0, DateTimeKind.Utc);
        var queuedAtUtc = utcNow.AddMinutes(-60);
        var lastHeartbeatUtc = utcNow.AddMinutes(-2);

        var result = AccessImportService.IsPendingBatchStale(queuedAtUtc, lastHeartbeatUtc, utcNow, 10);

        Assert.False(result);
    }

    #endregion
}

