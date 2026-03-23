using Api.Services;
using System.Data;
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

        var result = AccessImportService.ResolveTableName(null, schema);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveTableName_WithNullSchema_ReturnsNull()
    {
        var result = AccessImportService.ResolveTableName(new DataTable().Rows.Add(), null);

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
        schema.Columns["TABLE_TYPE"].AllowDBNull = true;
        schema.Rows.Add("master", "dbo", "TestTable", DBNull.Value);
        var row = schema.Rows[0];

        var result = AccessImportService.CheckIsUserTable(row, schema);

        Assert.True(result);
    }

    #endregion
}

